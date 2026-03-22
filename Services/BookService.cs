using Leafy_Library.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Leafy_Library.Services;

public class BookService
{
    private readonly IMongoCollection<Book> _books;
    private readonly EmbeddingService _embeddingService;

    public BookService(DatabaseService db, EmbeddingService embeddingService)
    {
        _books = db.Books;
        _embeddingService = embeddingService;
    }

    public Task<List<Book>> GetAllAsync(int page = 1, int pageSize = 20) =>
        _books.Find(_ => true)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync();

    public Task<long> GetCountAsync() =>
        _books.CountDocumentsAsync(_ => true);

    public async Task<Book?> GetByIdAsync(string id) =>
        await _books.Find(b => b.Id == id).FirstOrDefaultAsync();

    // ── Lexical search (Atlas Search) ──

    public async Task<List<Book>?> LexicalSearchAsync(string query, int page = 1, int pageSize = 20)
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

    public async Task<long> LexicalSearchCountAsync(string query)
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

    // ── Semantic search (Atlas Vector Search) ──

    public async Task<List<Book>?> SemanticSearchAsync(string query, int page = 1, int pageSize = 20)
    {
        var queryEmbedding = await _embeddingService.GetEmbeddingAsync(query);

        var vectorSearchStage = new BsonDocument("$vectorSearch", new BsonDocument
        {
            { "index", "vectorsearch" },
            { "path", "embeddings" },
            { "queryVector", new BsonArray(queryEmbedding.Select(v => (double)v)) },
            { "numCandidates", 100 },
            { "limit", (page * pageSize) + pageSize }
        });

        return await _books.Aggregate()
            .AppendStage<Book>(vectorSearchStage)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync();
    }

    public async Task<long> SemanticSearchCountAsync(string query)
    {
        var queryEmbedding = await _embeddingService.GetEmbeddingAsync(query);

        var vectorSearchStage = new BsonDocument("$vectorSearch", new BsonDocument
        {
            { "index", "vectorsearch" },
            { "path", "embeddings" },
            { "queryVector", new BsonArray(queryEmbedding.Select(v => (double)v)) },
            { "numCandidates", 200 },
            { "limit", 200 }
        });

        var results = await _books.Aggregate()
            .AppendStage<Book>(vectorSearchStage)
            .ToListAsync();

        return results.Count;
    }

    public async Task CreateAsync(Book book)
    {
        ArgumentNullException.ThrowIfNull(book);
        await _books.InsertOneAsync(book);
    }

    public async Task<bool> UpdateAsync(string id, Book updatedBook)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        ArgumentNullException.ThrowIfNull(updatedBook);

        var result = await _books.ReplaceOneAsync(b => b.Id == id, updatedBook);
        return result.ModifiedCount > 0;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        var result = await _books.DeleteOneAsync(b => b.Id == id);
        return result.DeletedCount > 0;
    }

    public async Task<bool> DecrementAvailableAsync(string bookId)
    {
        ArgumentException.ThrowIfNullOrEmpty(bookId);
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
        ArgumentException.ThrowIfNullOrEmpty(bookId);
        var update = Builders<Book>.Update.Inc(b => b.Available, 1);
        var result = await _books.UpdateOneAsync(b => b.Id == bookId, update);
        return result.ModifiedCount > 0;
    }
}
