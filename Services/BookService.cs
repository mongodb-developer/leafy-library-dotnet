using Leafy_Library.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Leafy_Library.Services;

public class BookService
{
    private readonly IMongoCollection<Book> _books;

    public BookService(DatabaseService db)
    {
        _books = db.Books;
    }

    public async Task<List<Book>> GetAllAsync(int page = 1, int pageSize = 20)
    {
        return await _books
            .Find(_ => true)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync();
    }

    public async Task<long> GetCountAsync()
    {
        return await _books.CountDocumentsAsync(_ => true);
    }

    public async Task<Book?> GetByIdAsync(string id)
    {
        return await _books
            .Find(b => b.Id == id)
            .FirstOrDefaultAsync();
    }

    public async Task<List<Book>?> SearchAsync(string query, int page = 1, int pageSize = 20)
    {
        var pipeline = new BsonDocument("$search", new BsonDocument
        {
            { "index", "fulltextsearch" },
            { "text", new BsonDocument
                {
                    { "query", query },
                    { "path", new BsonArray { "title", "authors.name", "genres" } }
                }
            }
        });

        return await _books.Aggregate()
            .AppendStage<Book>(pipeline)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync();
    }

    public async Task<long> SearchCountAsync(string query)
    {
        var pipeline = new BsonDocument("$search", new BsonDocument
        {
            { "index", "fulltextsearch" },
            { "text", new BsonDocument
                {
                    { "query", query },
                    { "path", new BsonArray { "title", "authors.name", "genres" } }
                }
            }
        });

        var results = await _books.Aggregate()
            .AppendStage<Book>(pipeline)
            .ToListAsync();

        return results.Count;
    }

    public async Task CreateAsync(Book book)
    {
        await _books.InsertOneAsync(book);
    }

    public async Task<bool> UpdateAsync(string id, Book updatedBook)
    {
        var result = await _books.ReplaceOneAsync(b => b.Id == id, updatedBook);
        return result.ModifiedCount > 0;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var result = await _books.DeleteOneAsync(b => b.Id == id);
        return result.DeletedCount > 0;
    }

    public async Task<bool> DecrementAvailableAsync(string bookId)
    {
        var update = Builders<Book>.Update.Inc(b => b.Available, -1);
        var filter = Builders<Book>.Filter.And(
            Builders<Book>.Filter.Eq(b => b.Id, bookId),
            Builders<Book>.Filter.Gt(b => b.Available, 0)
        );

        var result = await _books.UpdateOneAsync(filter, update);
        return result.ModifiedCount > 0;
    }

    public async Task<bool> IncrementAvailableAsync(string bookId)
    {
        var update = Builders<Book>.Update.Inc(b => b.Available, 1);
        var result = await _books.UpdateOneAsync(b => b.Id == bookId, update);
        return result.ModifiedCount > 0;
    }
}
