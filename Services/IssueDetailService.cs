using Leafy_Library.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
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

    // ──────────────────────────────────────────────
    //  Queries
    // ──────────────────────────────────────────────

    /// <summary>
    /// Gets a single issue detail by its ID.
    /// </summary>
    public async Task<IssueDetail?> GetByIdAsync(string id)
    {
        return await _issueDetails
            .Find(i => i.Id == id)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Gets all borrow records for a specific user.
    /// </summary>
    public async Task<List<IssueDetail>> GetByUserIdAsync(string userId)
    {
        var filter = Builders<IssueDetail>.Filter.Eq(i => i.User.Id, userId);
        return await _issueDetails
            .Find(filter)
            .SortByDescending(i => i.BorrowDate)
            .ToListAsync();
    }

    /// <summary>
    /// Gets all active (unreturned) borrows for a user.
    /// </summary>
    public async Task<List<IssueDetail>> GetActiveBorrowsAsync(string userId)
    {
        var filter = Builders<IssueDetail>.Filter.And(
            Builders<IssueDetail>.Filter.Eq(i => i.User.Id, userId),
            Builders<IssueDetail>.Filter.Eq(i => i.RecordType, IssueDetailType.BorrowedBook),
            Builders<IssueDetail>.Filter.Eq(i => i.Returned, false)
        );
        return await _issueDetails
            .Find(filter)
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

    // ──────────────────────────────────────────────
    //  Dashboard aggregations
    // ──────────────────────────────────────────────

    /// <summary>
    /// Gets loan summary stats (active, overdue, due soon, returned, avg reading time) via $facet.
    /// </summary>
    public async Task<LoanSummaryStats> GetLoanSummaryStatsAsync(string userId)
    {
        var matchFilter = Builders<IssueDetail>.Filter.Eq("user._id", new ObjectId(userId));

        const string daysToDueStage = """
        {
          "$addFields": {
            "daysToDue": {
              "$dateDiff": {
                "startDate": "$$NOW",
                "endDate": "$dueDate",
                "unit": "day"
              }
            }
          }
        }
        """;

        const string facetStage = """
        {
          "$facet": {
            "activeLoans": [
              { "$match": { "returned": false } },
              { "$count": "count" }
            ],
            "overdue": [
              { "$match": { "returned": false, "daysToDue": { "$lt": 0 } } },
              { "$count": "count" }
            ],
            "dueSoon": [
              { "$match": { "returned": false, "daysToDue": { "$gte": 0, "$lte": 3 } } },
              { "$count": "count" }
            ],
            "returned": [
              { "$match": { "returned": true } },
              { "$count": "count" }
            ],
            "avgReadingTime": [
              { "$match": { "returned": true } },
              {
                "$project": {
                  "readingDays": {
                    "$dateDiff": {
                      "startDate": "$borrowDate",
                      "endDate": "$returnedDate",
                      "unit": "day"
                    }
                  }
                }
              },
              {
                "$group": {
                  "_id": null,
                  "avgDays": { "$avg": "$readingDays" }
                }
              }
            ]
          }
        }
        """;

        var result = await _issueDetails
            .Aggregate()
            .Match(matchFilter)
            .AppendStage<BsonDocument>(daysToDueStage)
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
        var matchFilter = Builders<IssueDetail>.Filter.And(
            Builders<IssueDetail>.Filter.Eq("user._id", new ObjectId(userId)),
            Builders<IssueDetail>.Filter.Eq(x => x.RecordType, IssueDetailType.BorrowedBook),
            Builders<IssueDetail>.Filter.Eq(x => x.Returned, false)
        );

        const string daysToDueStage = """
        {
          "$addFields": {
            "daysToDue": {
              "$dateDiff": {
                "startDate": "$$NOW",
                "endDate": "$dueDate",
                "unit": "day"
              }
            }
          }
        }
        """;

        const string statusStage = """
        {
          "$addFields": {
            "status": {
              "$switch": {
                "branches": [
                  { "case": { "$lt": ["$daysToDue", 0] }, "then": "Overdue" },
                  { "case": { "$lte": ["$daysToDue", 3] }, "then": "Due soon" }
                ],
                "default": "OK"
              }
            }
          }
        }
        """;

        var results = await _issueDetails
            .Aggregate()
            .Match(matchFilter)
            .AppendStage<LoanSummaryIntermediate>(daysToDueStage)
            .AppendStage<LoanSummaryIntermediate>(statusStage)
            .Project(x => new LoanSummary
            {
                Title = x.Book.Title,
                BorrowDate = x.BorrowDate,
                DueDate = x.DueDate,
                DaysToDue = x.DaysToDue,
                Status = x.Status
            })
            .SortBy(x => x.DueDate)
            .ToListAsync();

        return results;
    }

    /// <summary>
    /// Gets the top 5 genres a user has borrowed, by count.
    /// </summary>
    public async Task<List<GenreCount>> GetTopGenresAsync(string userId)
    {
        var matchFilter = Builders<IssueDetail>.Filter.Eq("user._id", new ObjectId(userId));

        const string lookupStage = """
        {
          "$lookup": {
            "from": "books",
            "localField": "book._id",
            "foreignField": "_id",
            "as": "bookDetails"
          }
        }
        """;

        const string unwindBookDetails = """
        { "$unwind": "$bookDetails" }
        """;

        const string unwindGenres = """
        { "$unwind": "$bookDetails.genres" }
        """;

        const string groupStage = """
        {
          "$group": {
            "_id": "$bookDetails.genres",
            "count": { "$sum": 1 }
          }
        }
        """;

        var results = await _issueDetails
            .Aggregate()
            .Match(matchFilter)
            .AppendStage<BsonDocument>(lookupStage)
            .AppendStage<BsonDocument>(unwindBookDetails)
            .AppendStage<BsonDocument>(unwindGenres)
            .AppendStage<GenreCount>(groupStage)
            .SortByDescending(x => x.Count)
            .Limit(5)
            .ToListAsync();

        return results;
    }

    /// <summary>
    /// Gets loan history grouped by month (YYYY-MM), sorted chronologically.
    /// </summary>
    public async Task<List<MonthlyLoanCount>> GetLoanHistoryByMonthAsync(string userId)
    {
        var matchFilter = Builders<IssueDetail>.Filter.Eq("user._id", new ObjectId(userId));

        const string groupStage = """
        {
          "$group": {
            "_id": {
              "$dateToString": {
                "format": "%Y-%m",
                "date": "$borrowDate"
              }
            },
            "count": { "$sum": 1 }
          }
        }
        """;

        var results = await _issueDetails
            .Aggregate()
            .Match(matchFilter)
            .AppendStage<MonthlyLoanCount>(groupStage)
            .SortBy(x => x.Month)
            .ToListAsync();

        return results;
    }

    /// <summary>
    /// Gets the average reading time in days for returned books.
    /// </summary>
    public async Task<double?> GetAverageReadingTimeAsync(string userId)
    {
        var matchFilter = Builders<IssueDetail>.Filter.And(
            Builders<IssueDetail>.Filter.Eq("user._id", new ObjectId(userId)),
            Builders<IssueDetail>.Filter.Eq(x => x.Returned, true)
        );

        const string projectStage = """
        {
          "$project": {
            "readingDays": {
              "$dateDiff": {
                "startDate": "$borrowDate",
                "endDate": "$returnedDate",
                "unit": "day"
              }
            }
          }
        }
        """;

        const string groupStage = """
        {
          "$group": {
            "_id": null,
            "avgReadingTime": { "$avg": "$readingDays" }
          }
        }
        """;

        var result = await _issueDetails
            .Aggregate()
            .Match(matchFilter)
            .AppendStage<BsonDocument>(projectStage)
            .AppendStage<BsonDocument>(groupStage)
            .FirstOrDefaultAsync();

        return result?["avgReadingTime"]?.AsNullableDouble;
    }

    /// <summary>
    /// Counts loans due soon (0–3 days remaining) for a user.
    /// </summary>
    public async Task<int> GetDueSoonCountAsync(string userId)
    {
        var matchFilter = Builders<IssueDetail>.Filter.And(
            Builders<IssueDetail>.Filter.Eq("user._id", new ObjectId(userId)),
            Builders<IssueDetail>.Filter.Eq(x => x.Returned, false)
        );

        const string daysToDueStage = """
        {
          "$addFields": {
            "daysToDue": {
              "$dateDiff": {
                "startDate": "$$NOW",
                "endDate": "$dueDate",
                "unit": "day"
              }
            }
          }
        }
        """;

        const string dueSoonMatch = """
        {
          "$match": {
            "daysToDue": { "$lte": 3, "$gte": 0 }
          }
        }
        """;

        var result = await _issueDetails
            .Aggregate()
            .Match(matchFilter)
            .AppendStage<BsonDocument>(daysToDueStage)
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
        var book = await _bookService.GetByIdAsync(bookId);
        if (book is null || book.Available <= 0)
            return null;

        // Check if user already has this book borrowed
        var existingFilter = Builders<IssueDetail>.Filter.And(
            Builders<IssueDetail>.Filter.Eq(i => i.User.Id, userId),
            Builders<IssueDetail>.Filter.Eq(i => i.Book.Id, bookId),
            Builders<IssueDetail>.Filter.Eq(i => i.RecordType, IssueDetailType.BorrowedBook),
            Builders<IssueDetail>.Filter.Eq(i => i.Returned, false)
        );
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
        var book = await _bookService.GetByIdAsync(bookId);
        if (book is null) return null;

        var now = DateTime.UtcNow;

        // Check for existing active borrow (renewal case)
        var existingFilter = Builders<IssueDetail>.Filter.And(
            Builders<IssueDetail>.Filter.Eq(i => i.User.Id, userId),
            Builders<IssueDetail>.Filter.Eq(i => i.Book.Id, bookId),
            Builders<IssueDetail>.Filter.Eq(i => i.RecordType, IssueDetailType.BorrowedBook),
            Builders<IssueDetail>.Filter.Ne(i => i.Returned, true)
        );

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
        var filter = Builders<IssueDetail>.Filter.And(
            Builders<IssueDetail>.Filter.Eq(i => i.User.Id, userId),
            Builders<IssueDetail>.Filter.Eq(i => i.Book.Id, bookId),
            Builders<IssueDetail>.Filter.Eq(i => i.RecordType, IssueDetailType.BorrowedBook),
            Builders<IssueDetail>.Filter.Ne(i => i.Returned, true)
        );

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

    // ──────────────────────────────────────────────
    //  Private types (aggregation intermediates)
    // ──────────────────────────────────────────────

    [BsonIgnoreExtraElements]
    private sealed class LoanSummaryIntermediate
    {
        [BsonElement("book")]
        public BookReference Book { get; set; } = null!;

        [BsonElement("borrowDate")]
        public DateTime BorrowDate { get; set; }

        [BsonElement("dueDate")]
        public DateTime DueDate { get; set; }

        [BsonElement("daysToDue")]
        public int DaysToDue { get; set; }

        [BsonElement("status")]
        public string Status { get; set; } = string.Empty;
    }

    [BsonIgnoreExtraElements]
    private sealed class BookReference
    {
        [BsonElement("title")]
        public string Title { get; set; } = string.Empty;
    }
}
