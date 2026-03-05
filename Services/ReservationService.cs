using Leafy_Library.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Leafy_Library.Services;

public class ReservationService
{
    private readonly IMongoCollection<IssueDetail> _issueDetails;
    private readonly BookService _bookService;

    private const int ReservationDurationHours = 12;

    public ReservationService(DatabaseService db, BookService bookService)
    {
        _issueDetails = db.IssueDetails;
        _bookService = bookService;
    }

    /// <summary>
    /// Creates a reservation for a book. Decrements available count.
    /// Composite _id format: {userId}R{bookId}
    /// </summary>
    public async Task<IssueDetail> CreateReservationAsync(string bookId, string userId, string userName)
    {
        var book = await _bookService.GetByIdAsync(bookId);
        if (book is null)
            throw new InvalidOperationException("Book not found");

        if (book.Available <= 0)
            throw new InvalidOperationException("Book not available");

        // Check if user already has a reservation for this book
        var reservationId = GetReservationId(bookId, userId);
        var existing = await _issueDetails
            .Find(i => i.Id == reservationId)
            .FirstOrDefaultAsync();

        if (existing is not null)
            throw new InvalidOperationException("Reservation already exists");

        var reservation = new IssueDetail
        {
            Id = reservationId,
            Book = new IssueDetailBook { Id = bookId, Title = book.Title },
            User = new IssueDetailUser { Id = userId, Name = userName },
            RecordType = IssueDetailType.Reservation,
            ExpirationDate = DateTime.UtcNow.AddHours(ReservationDurationHours)
        };

        await _issueDetails.InsertOneAsync(reservation);
        await _bookService.DecrementAvailableAsync(bookId);

        return reservation;
    }

    /// <summary>
    /// Cancels a reservation. Increments available count.
    /// </summary>
    public async Task<bool> CancelReservationAsync(string bookId, string userId)
    {
        var reservationId = GetReservationId(bookId, userId);
        var result = await _issueDetails.DeleteOneAsync(i => i.Id == reservationId);

        if (result.DeletedCount == 0)
            return false;

        await _bookService.IncrementAvailableAsync(bookId);
        return true;
    }

    /// <summary>
    /// Gets all reservations for the current user.
    /// </summary>
    public async Task<List<IssueDetail>> GetUserReservationsAsync(string userId)
    {
        var filter = Builders<IssueDetail>.Filter.And(
            Builders<IssueDetail>.Filter.Regex(i => i.Id, new BsonRegularExpression($"^{userId}")),
            Builders<IssueDetail>.Filter.Eq(i => i.RecordType, IssueDetailType.Reservation)
        );
        return await _issueDetails
            .Find(filter)
            .ToListAsync();
    }

    /// <summary>
    /// Gets a specific reservation for a user and book, if it exists.
    /// </summary>
    public async Task<IssueDetail?> GetReservationAsync(string bookId, string userId)
    {
        var reservationId = GetReservationId(bookId, userId);
        return await _issueDetails
            .Find(i => i.Id == reservationId)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Gets a paginated list of all reservations (admin).
    /// </summary>
    public async Task<PagedResult<IssueDetail>> GetReservationsPageAsync(int limit, int skip)
    {
        var filter = Builders<IssueDetail>.Filter.Eq(i => i.RecordType, IssueDetailType.Reservation);
        var totalCount = await _issueDetails.CountDocumentsAsync(filter);
        var data = await _issueDetails.Find(filter)
            .Skip(skip)
            .Limit(limit)
            .ToListAsync();
        return new PagedResult<IssueDetail> { Data = data, TotalCount = totalCount };
    }

    /// <summary>
    /// Composite _id: {userId}R{bookId}
    /// </summary>
    private static string GetReservationId(string bookId, string userId)
    {
        return $"{userId}R{bookId}";
    }
}
