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
    /// as an embedded document into the book's reviews array.
    /// </summary>
    public async Task<Review> CreateAsync(string bookId, string reviewerName, string? text, int rating)
    {
        var review = new Review
        {
            Id = ObjectId.GenerateNewId().ToString(),
            BookId = bookId,
            Name = reviewerName,
            Text = text,
            Rating = Math.Clamp(rating, 1, 5),
            Timestamp = DateTime.UtcNow
        };

        await _reviews.InsertOneAsync(review);

        // Also embed into the book document
        var embeddedReview = new BsonDocument
        {
            { "_id", new ObjectId(review.Id) },
            { "text", review.Text != null ? BsonValue.Create(review.Text) : BsonNull.Value },
            { "name", review.Name },
            { "rating", review.Rating },
            { "timestamp", new BsonInt64(new DateTimeOffset(review.Timestamp).ToUnixTimeMilliseconds()) }
        };

        var update = Builders<Book>.Update.Push("reviews", embeddedReview);
        await _books.UpdateOneAsync(b => b.Id == bookId, update);

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
