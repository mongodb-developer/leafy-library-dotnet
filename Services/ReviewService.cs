using Leafy_Library.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Leafy_Library.Services;

public class ReviewService
{
    private readonly IMongoCollection<Review> _reviews;
    private readonly IMongoCollection<Book> _books;

    public ReviewService(DatabaseService db)
    {
        _reviews = db.Reviews;
        _books = db.Books;
    }

    public async Task<List<Review>> GetByBookIdAsync(string bookId)
    {
        return await _reviews
            .Find(r => r.BookId == bookId)
            .SortByDescending(r => r.Timestamp)
            .ToListAsync();
    }

    public async Task<Review?> GetByIdAsync(string id)
    {
        return await _reviews
            .Find(r => r.Id == id)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Creates a review in the reviews collection and also pushes it
    /// into the book's embedded reviews array (subset pattern: sorted
    /// by timestamp descending, sliced to the 5 most recent).
    /// </summary>
    public async Task<Review> CreateAsync(string bookId, string reviewerName, string text, int? rating)
    {
        var review = new Review
        {
            Id = ObjectId.GenerateNewId().ToString(),
            BookId = bookId,
            Name = reviewerName,
            Text = text,
            Rating = rating.HasValue ? Math.Clamp(rating.Value, 1, 5) : null,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        await _reviews.InsertOneAsync(review);

        // Build the embedded review (without bookId — it's implied by the parent document)
        var embeddedReview = new BsonDocument
        {
            { "_id", new ObjectId(review.Id) },
            { "text", review.Text },
            { "name", review.Name },
            { "rating", review.Rating.HasValue ? BsonValue.Create(review.Rating.Value) : BsonNull.Value },
            { "timestamp", new BsonInt64(review.Timestamp) }
        };

        // Subset pattern: push with $each, $sort by timestamp desc, $slice to 5
        var rawUpdate = new BsonDocument
        {
            { "$push", new BsonDocument
                {
                    { "reviews", new BsonDocument
                        {
                            { "$each", new BsonArray { embeddedReview } },
                            { "$sort", new BsonDocument("timestamp", -1) },
                            { "$slice", 5 }
                        }
                    }
                }
            }
        };

        var booksCollection = _books.Database.GetCollection<BsonDocument>(_books.CollectionNamespace.CollectionName);
        await booksCollection.UpdateOneAsync(
            new BsonDocument("_id", bookId),
            rawUpdate);

        return review;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var review = await GetByIdAsync(id);
        if (review is null) return false;

        await _reviews.DeleteOneAsync(r => r.Id == id);

        // Remove from the book's embedded reviews
        var pull = Builders<Book>.Update.PullFilter("reviews",
            Builders<BsonDocument>.Filter.Eq("_id", new ObjectId(id)));
        await _books.UpdateOneAsync(b => b.Id == review.BookId, pull);

        return true;
    }
}
