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

    public async Task<IssueDetail?> GetByIdAsync(string id) =>
        await _issueDetails.Find(i => i.Id == id).FirstOrDefaultAsync();

    public Task<List<IssueDetail>> GetByUserIdAsync(string userId) =>
        _issueDetails.Find(UserMatch(userId))
            .SortByDescending(i => i.BorrowDate)
            .ToListAsync();

    public Task<List<IssueDetail>> GetActiveBorrowsAsync(string userId) =>
        _issueDetails.Find(
            UserMatch(userId)
            & Builders<IssueDetail>.Filter.Eq(i => i.RecordType, IssueDetailType.BorrowedBook)
            & Builders<IssueDetail>.Filter.Eq(i => i.Returned, false))
            .ToListAsync();

    public Task<List<IssueDetail>> GetByBookIdAsync(string bookId) =>
        _issueDetails.Find(i => i.Book.Id == bookId)
            .SortByDescending(i => i.BorrowDate)
            .ToListAsync();

    // ──────────────────────────────────────────────
    //  Dashboard aggregations
    // ──────────────────────────────────────────────

    /// <summary>
    /// Gets loan summary stats (active, overdue, due soon, returned, avg reading time) via $facet.
    /// </summary>
    public async Task<LoanSummaryStats> GetLoanSummaryStatsAsync(string userId)
    {
        var facetStage = new BsonDocument("$facet", new BsonDocument
        {
            { "activeLoans", new BsonArray
                {
                    new BsonDocument("$match", new BsonDocument("returned", false)),
                    new BsonDocument("$count", "count")
                }
            },
            { "overdue", new BsonArray
                {
                    new BsonDocument("$match", new BsonDocument
                    {
                        { "returned", false },
                        { "daysToDue", new BsonDocument("$lt", 0) }
                    }),
                    new BsonDocument("$count", "count")
                }
            },
            { "dueSoon", new BsonArray
                {
                    new BsonDocument("$match", new BsonDocument
                    {
                        { "returned", false },
                        { "daysToDue", new BsonDocument { { "$gte", 0 }, { "$lte", DueSoonDays } } }
                    }),
                    new BsonDocument("$count", "count")
                }
            },
            { "returned", new BsonArray
                {
                    new BsonDocument("$match", new BsonDocument("returned", true)),
                    new BsonDocument("$count", "count")
                }
            },
            { "avgReadingTime", new BsonArray
                {
                    new BsonDocument("$match", new BsonDocument("returned", true)),
                    new BsonDocument("$project", new BsonDocument("readingDays",
                        new BsonDocument("$dateDiff", new BsonDocument
                        {
                            { "startDate", "$borrowDate" },
                            { "endDate", "$returnedDate" },
                            { "unit", "day" }
                        }))),
                    new BsonDocument("$group", new BsonDocument
                    {
                        { "_id", BsonNull.Value },
                        { "avgDays", new BsonDocument("$avg", "$readingDays") }
                    })
                }
            }
        });

        var result = await _issueDetails
            .Aggregate()
            .Match(UserMatch(userId))
            .AppendStage<BsonDocument>(DaysToDueStage)
            .AppendStage<BsonDocument>(facetStage)
            .FirstOrDefaultAsync();

        if (result is null)
            return new LoanSummaryStats();

        static int ExtractCount(BsonDocument doc, string field)
        {
            var arr = doc[field].AsBsonArray;
            return arr.Count > 0 ? arr[0].AsBsonDocument["count"].AsInt32 : 0;
        }

        return new LoanSummaryStats
        {
            ActiveLoans = ExtractCount(result, "activeLoans"),
            Overdue = ExtractCount(result, "overdue"),
            DueSoon = ExtractCount(result, "dueSoon"),
            Returned = ExtractCount(result, "returned"),
            AvgReadingDays = result["avgReadingTime"].AsBsonArray is { Count: > 0 } arr
                ? arr[0].AsBsonDocument["avgDays"].AsNullableDouble
                : null
        };
    }

    /// <summary>
    /// Gets active loans for a user with computed daysToDue and status via aggregation.
    /// </summary>
    public async Task<List<LoanSummary>> GetLoanSummariesAsync(string userId)
    {
        var matchFilter = UserMatch(userId)
            & Builders<IssueDetail>.Filter.Eq(x => x.RecordType, IssueDetailType.BorrowedBook)
            & Builders<IssueDetail>.Filter.Eq(x => x.Returned, false);

        var statusStage = new BsonDocument("$addFields",
            new BsonDocument("status", new BsonDocument("$switch", new BsonDocument
            {
                { "branches", new BsonArray
                    {
                        new BsonDocument { { "case", new BsonDocument("$lt", new BsonArray { "$daysToDue", 0 }) }, { "then", "Overdue" } },
                        new BsonDocument { { "case", new BsonDocument("$lte", new BsonArray { "$daysToDue", DueSoonDays }) }, { "then", "Due soon" } }
                    }
                },
                { "default", "OK" }
            })));

        var projectStage = new BsonDocument("$project", new BsonDocument
        {
            { "title", "$book.title" },
            { "borrowDate", 1 },
            { "dueDate", 1 },
            { "daysToDue", 1 },
            { "status", 1 }
        });

        return await _issueDetails
            .Aggregate()
            .Match(matchFilter)
            .AppendStage<BsonDocument>(DaysToDueStage)
            .AppendStage<BsonDocument>(statusStage)
            .AppendStage<LoanSummary>(projectStage)
            .SortBy(x => x.DueDate)
            .ToListAsync();
    }

    /// <summary>
    /// Gets the top 5 genres a user has borrowed, by count.
    /// </summary>
    public async Task<List<GenreCount>> GetTopGenresAsync(string userId)
    {
        var groupStage = new BsonDocument("$group", new BsonDocument
        {
            { "_id", "$bookDetails.genres" },
            { "count", new BsonDocument("$sum", 1) }
        });

        return await _issueDetails
            .Aggregate()
            .Match(UserMatch(userId))
            .Lookup("books", "book._id", "_id", "bookDetails")
            .Unwind("bookDetails")
            .Unwind("bookDetails.genres")
            .AppendStage<GenreCount>(groupStage)
            .SortByDescending(x => x.Count)
            .Limit(5)
            .ToListAsync();
    }

    /// <summary>
    /// Gets loan history grouped by month (YYYY-MM), sorted chronologically.
    /// </summary>
    public async Task<List<MonthlyLoanCount>> GetLoanHistoryByMonthAsync(string userId)
    {
        var groupStage = new BsonDocument("$group", new BsonDocument
        {
            { "_id", new BsonDocument("$dateToString", new BsonDocument
                {
                    { "format", "%Y-%m" },
                    { "date", "$borrowDate" }
                })
            },
            { "count", new BsonDocument("$sum", 1) }
        });

        return await _issueDetails
            .Aggregate()
            .Match(UserMatch(userId))
            .AppendStage<MonthlyLoanCount>(groupStage)
            .SortBy(x => x.Month)
            .ToListAsync();
    }

    /// <summary>
    /// Gets the average reading time in days for returned books.
    /// </summary>
    public async Task<double?> GetAverageReadingTimeAsync(string userId)
    {
        var matchFilter = UserMatch(userId)
            & Builders<IssueDetail>.Filter.Eq(x => x.Returned, true);

        var projectStage = new BsonDocument("$project", new BsonDocument("readingDays",
            new BsonDocument("$dateDiff", new BsonDocument
            {
                { "startDate", "$borrowDate" },
                { "endDate", "$returnedDate" },
                { "unit", "day" }
            })));

        var groupStage = new BsonDocument("$group", new BsonDocument
        {
            { "_id", BsonNull.Value },
            { "avgReadingTime", new BsonDocument("$avg", "$readingDays") }
        });

        var result = await _issueDetails
            .Aggregate()
            .Match(matchFilter)
            .AppendStage<BsonDocument>(projectStage)
            .AppendStage<BsonDocument>(groupStage)
            .FirstOrDefaultAsync();

        return result?["avgReadingTime"]?.AsNullableDouble;
    }

    /// <summary>
    /// Counts loans due soon (0–DueSoonDays days remaining) for a user.
    /// </summary>
    public async Task<int> GetDueSoonCountAsync(string userId)
    {
        var matchFilter = UserMatch(userId)
            & Builders<IssueDetail>.Filter.Eq(x => x.Returned, false);

        var dueSoonMatch = new BsonDocument("$match", new BsonDocument("daysToDue",
            new BsonDocument { { "$gte", 0 }, { "$lte", DueSoonDays } }));

        var result = await _issueDetails
            .Aggregate()
            .Match(matchFilter)
            .AppendStage<BsonDocument>(DaysToDueStage)
            .AppendStage<BsonDocument>(dueSoonMatch)
            .Count()
            .FirstOrDefaultAsync();

        return (int)(result?.Count ?? 0);
    }

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

        // Check if user already has this book borrowed
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

        // Check for existing active borrow (renewal case)
        var existingFilter = UserMatch(userId)
            & Builders<IssueDetail>.Filter.Eq(i => i.Book.Id, bookId)
            & Builders<IssueDetail>.Filter.Eq(i => i.RecordType, IssueDetailType.BorrowedBook)
            & Builders<IssueDetail>.Filter.Ne(i => i.Returned, true);

        var existingBorrow = await _issueDetails.Find(existingFilter).FirstOrDefaultAsync();
        bool isRenewal = false;
        IssueDetail result;

        if (existingBorrow is not null)
        {
            // Renewal — update existing borrow record
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
            // New borrow
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

        // Delete matching reservation if one exists
        var reservationId = $"{userId}R{bookId}";
        var deleteResult = await _issueDetails.DeleteOneAsync(i => i.Id == reservationId);
        var borrowReplacesReservation = deleteResult.DeletedCount == 1;

        // Only decrement inventory if no reservation was deleted AND not a renewal
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
