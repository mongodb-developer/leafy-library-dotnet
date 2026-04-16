using Leafy_Library.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Leafy_Library.Services;

public class IssueDetailService
{
    private readonly IMongoCollection<IssueDetail> _issueDetails;
    private readonly BookService _bookService;

    private const int BorrowDurationDays = 21;
    private const int DueSoonDays = 3;

    private static readonly BsonDocument DaysToDueStage = new("$addFields",
        new BsonDocument("daysToDue", new BsonDocument("$dateDiff", new BsonDocument
        {
            { "startDate", "$$NOW" },
            { "endDate", "$dueDate" },
            { "unit", "day" }
        })));
        
    public IssueDetailService(DatabaseService db, BookService bookService)
    {
        _issueDetails = db.IssueDetails;
        _bookService = bookService;
    }

    // ──────────────────────────────────────────────
    //  Helpers
    // ──────────────────────────────────────────────

    private static FilterDefinition<IssueDetail> UserMatch(string userId) =>
        Builders<IssueDetail>.Filter.Eq(i => i.User.Id, userId);

    // ──────────────────────────────────────────────
    //  Queries
    // ──────────────────────────────────────────────

    /// <summary>
    /// Gets a single issue detail by its ID.
    /// </summary>
    public async Task<IssueDetail?> GetByIdAsync(string id) =>
        await _issueDetails.Find(i => i.Id == id).FirstOrDefaultAsync();

    /// <summary>
    /// Gets all borrow records for a specific user.
    /// </summary>
    public Task<List<IssueDetail>> GetByUserIdAsync(string userId) =>
        _issueDetails.Find(UserMatch(userId))
            .SortByDescending(i => i.BorrowDate)
            .ToListAsync();

    /// <summary>
    /// Gets all active (unreturned) borrows for a user.
    /// </summary>
    public Task<List<IssueDetail>> GetActiveBorrowsAsync(string userId) =>
        _issueDetails.Find(
                UserMatch(userId)
                & Builders<IssueDetail>.Filter.Eq(i => i.RecordType, IssueDetailType.BorrowedBook)
                & Builders<IssueDetail>.Filter.Eq(i => i.Returned, false))
            .ToListAsync();

    /// <summary>
    /// Gets all borrow records for a specific book.
    /// </summary>
    public Task<List<IssueDetail>> GetByBookIdAsync(string bookId) =>
        _issueDetails.Find(i => i.Book.Id == bookId)
            .SortByDescending(i => i.BorrowDate)
            .ToListAsync();

    // ──────────────────────────────────────────────
    //  Borrow / Return operations
    // ──────────────────────────────────────────────

    /// <summary>
    /// Borrows a book for a user. Decrements available count and creates an issue record.
    /// Returns null if the book is not available or doesn't exist.
    /// </summary>
    public async Task<IssueDetail?> BorrowBookAsync(string bookId, string userId, string userName)
    {
        ArgumentException.ThrowIfNullOrEmpty(bookId);
        ArgumentException.ThrowIfNullOrEmpty(userId);
        ArgumentException.ThrowIfNullOrEmpty(userName);

        var book = await _bookService.GetByIdAsync(bookId);
        if (book is null || book.Available <= 0)
            return null;

        var existingFilter = UserMatch(userId)
            & Builders<IssueDetail>.Filter.Eq(i => i.Book.Id, bookId)
            & Builders<IssueDetail>.Filter.Eq(i => i.RecordType, IssueDetailType.BorrowedBook)
            & Builders<IssueDetail>.Filter.Eq(i => i.Returned, false);

        var existingBorrow = await _issueDetails
            .Find(existingFilter)
            .FirstOrDefaultAsync();

        if (existingBorrow is not null)
            return null;

        var decremented = await _bookService.DecrementAvailableAsync(bookId);
        if (!decremented)
            return null;

        var now = DateTime.UtcNow;
        var issueDetail = new IssueDetail
        {
            Id = $"{userId}_{ObjectId.GenerateNewId()}",
            Book = new IssueDetailBook { Id = bookId, Title = book.Title },
            User = new IssueDetailUser { Id = userId, Name = userName },
            BorrowDate = now,
            DueDate = now.AddDays(BorrowDurationDays),
            RecordType = IssueDetailType.BorrowedBook,
            Returned = false
        };

        await _issueDetails.InsertOneAsync(issueDetail);
        return issueDetail;
    }

    /// <summary>
    /// Returns a borrowed book. Marks the issue as returned and increments available count.
    /// </summary>
    public async Task<bool> ReturnBookAsync(string issueId)
    {
        ArgumentException.ThrowIfNullOrEmpty(issueId);

        var issue = await _issueDetails
            .Find(i => i.Id == issueId && i.Returned == false)
            .FirstOrDefaultAsync();

        if (issue is null)
            return false;

        var update = Builders<IssueDetail>.Update
            .Set(i => i.Returned, true)
            .Set(i => i.ReturnedDate, DateTime.UtcNow);

        var result = await _issueDetails.UpdateOneAsync(i => i.Id == issueId, update);

        if (result.ModifiedCount > 0)
        {
            await _bookService.IncrementAvailableAsync(issue.Book.Id);
            return true;
        }

        return false;
    }

    // ──────────────────────────────────────────────
    //  Admin operations
    // ──────────────────────────────────────────────

    /// <summary>
    /// Gets a paginated list of all borrowed books (admin).
    /// </summary>
    public async Task<PagedResult<IssueDetail>> GetBorrowedBooksPageAsync(int limit, int skip)
    {
        var filter = Builders<IssueDetail>.Filter.Eq(i => i.RecordType, IssueDetailType.BorrowedBook);
        var totalCount = await _issueDetails.CountDocumentsAsync(filter);
        var data = await _issueDetails.Find(filter)
            .SortByDescending(i => i.BorrowDate)
            .Skip(skip)
            .Limit(limit)
            .ToListAsync();

        return new PagedResult<IssueDetail> { Data = data, TotalCount = totalCount };
    }

    /// <summary>
    /// Admin: Lend a book to a user. Converts a reservation to a borrow if one exists.
    /// Only decrements inventory if the borrow doesn't replace an existing reservation
    /// and is not a renewal of an existing borrow.
    /// </summary>
    public async Task<IssueDetail?> AdminBorrowBookAsync(string bookId, string userId, string userName)
    {
        ArgumentException.ThrowIfNullOrEmpty(bookId);
        ArgumentException.ThrowIfNullOrEmpty(userId);
        ArgumentException.ThrowIfNullOrEmpty(userName);

        var book = await _bookService.GetByIdAsync(bookId);
        if (book is null) return null;

        var now = DateTime.UtcNow;

        var existingFilter = UserMatch(userId)
            & Builders<IssueDetail>.Filter.Eq(i => i.Book.Id, bookId)
            & Builders<IssueDetail>.Filter.Eq(i => i.RecordType, IssueDetailType.BorrowedBook)
            & Builders<IssueDetail>.Filter.Ne(i => i.Returned, true);

        var existingBorrow = await _issueDetails.Find(existingFilter).FirstOrDefaultAsync();
        bool isRenewal = false;
        IssueDetail result;

        if (existingBorrow is not null)
        {
            var update = Builders<IssueDetail>.Update
                .Set(i => i.BorrowDate, now)
                .Set(i => i.DueDate, now.AddDays(BorrowDurationDays));

            await _issueDetails.UpdateOneAsync(existingFilter, update);

            isRenewal = true;
            existingBorrow.BorrowDate = now;
            existingBorrow.DueDate = now.AddDays(BorrowDurationDays);
            result = existingBorrow;
        }
        else
        {
            result = new IssueDetail
            {
                Id = $"{userId}_{ObjectId.GenerateNewId()}",
                Book = new IssueDetailBook { Id = bookId, Title = book.Title },
                User = new IssueDetailUser { Id = userId, Name = userName },
                BorrowDate = now,
                DueDate = now.AddDays(BorrowDurationDays),
                RecordType = IssueDetailType.BorrowedBook,
                Returned = false
            };

            await _issueDetails.InsertOneAsync(result);
        }

        var reservationId = $"{userId}R{bookId}";
        var deleteResult = await _issueDetails.DeleteOneAsync(i => i.Id == reservationId);
        var borrowReplacesReservation = deleteResult.DeletedCount == 1;

        if (!borrowReplacesReservation && !isRenewal)
        {
            await _bookService.DecrementAvailableAsync(bookId);
        }

        return result;
    }

    /// <summary>
    /// Admin: Return a borrowed book by bookId and userId.
    /// Finds the active borrow, marks as returned, increments inventory.
    /// </summary>
    public async Task<bool> AdminReturnBookAsync(string bookId, string userId)
    {
        ArgumentException.ThrowIfNullOrEmpty(bookId);
        ArgumentException.ThrowIfNullOrEmpty(userId);

        var filter = UserMatch(userId)
            & Builders<IssueDetail>.Filter.Eq(i => i.Book.Id, bookId)
            & Builders<IssueDetail>.Filter.Eq(i => i.RecordType, IssueDetailType.BorrowedBook)
            & Builders<IssueDetail>.Filter.Ne(i => i.Returned, true);

        var update = Builders<IssueDetail>.Update
            .Set(i => i.Returned, true)
            .Set(i => i.ReturnedDate, DateTime.UtcNow);

        var result = await _issueDetails.UpdateOneAsync(filter, update);

        if (result.ModifiedCount > 0)
        {
            await _bookService.IncrementAvailableAsync(bookId);
            return true;
        }

        return false;
    }
}