using Leafy_Library.Models;
using MongoDB.Driver;

namespace Leafy_Library.Services;

public class IssueDetailService
{
    private readonly IMongoCollection<IssueDetail> _issueDetails;
    private readonly BookService _bookService;

    private const int BorrowDurationDays = 21;

    public IssueDetailService(DatabaseService db, BookService bookService)
    {
        _issueDetails = db.IssueDetails;
        _bookService = bookService;
    }

    /// <summary>
    /// Gets all borrow records for a specific user.
    /// </summary>
    public async Task<List<IssueDetail>> GetByUserIdAsync(string userId)
    {
        return await _issueDetails
            .Find(i => i.User.Id == userId)
            .SortByDescending(i => i.BorrowDate)
            .ToListAsync();
    }

    /// <summary>
    /// Gets all active (unreturned) borrows for a user.
    /// </summary>
    public async Task<List<IssueDetail>> GetActiveBorrowsAsync(string userId)
    {
        return await _issueDetails
            .Find(i => i.User.Id == userId && !i.Returned)
            .ToListAsync();
    }

    /// <summary>
    /// Gets all borrow records for a specific book.
    /// </summary>
    public async Task<List<IssueDetail>> GetByBookIdAsync(string bookId)
    {
        return await _issueDetails
            .Find(i => i.Book.Id == bookId)
            .SortByDescending(i => i.BorrowDate)
            .ToListAsync();
    }

    /// <summary>
    /// Borrows a book for a user. Decrements available count and creates an issue record.
    /// Returns null if the book is not available or doesn't exist.
    /// </summary>
    public async Task<IssueDetail?> BorrowBookAsync(string bookId, string userId, string userName)
    {
        var book = await _bookService.GetByIdAsync(bookId);
        if (book is null || book.Available <= 0)
            return null;

        // Check if user already has this book borrowed
        var existingBorrow = await _issueDetails
            .Find(i => i.Book.Id == bookId && i.User.Id == userId && !i.Returned)
            .FirstOrDefaultAsync();

        if (existingBorrow is not null)
            return null;

        var decremented = await _bookService.DecrementAvailableAsync(bookId);
        if (!decremented)
            return null;

        var now = DateTime.UtcNow;
        var issueDetail = new IssueDetail
        {
            Id = $"{userId}B{bookId}",
            Book = new Book { Id = bookId, Title = book.Title },
            User = new User { Id = userId, Username = userName },
            BorrowDate = now,
            DueDate = now.AddDays(BorrowDurationDays),
            RecordType = "borrowedBook",
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
        var issue = await _issueDetails
            .Find(i => i.Id == issueId && !i.Returned)
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

    /// <summary>
    /// Gets a single issue detail by its ID.
    /// </summary>
    public async Task<IssueDetail?> GetByIdAsync(string id)
    {
        return await _issueDetails
            .Find(i => i.Id == id)
            .FirstOrDefaultAsync();
    }
}
