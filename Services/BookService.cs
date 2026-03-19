using Leafy_Library.Models;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Leafy_Library.Services;

public class BookService
{
    private readonly IMongoCollection<Book> _books;
    private readonly EmbeddingService _embeddingService;
    private readonly int _dimensions;

    public BookService(DatabaseService db, EmbeddingService embeddingService,
        IOptions<EmbeddingSettings> embeddingSettings)
    {
        _books = db.Books;
        _embeddingService = embeddingService;
        _dimensions = embeddingSettings.Value.Dimensions;
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

    public async Task<List<Book>?> SearchAsync(string query, int page = 1, int pageSize = 20)
    {
        var queryVector = await _embeddingService.GetEmbeddingAsync(query);

        var pipeline = new BsonDocument("$vectorSearch", new BsonDocument
        {
            { "index", "vector_index" },
            { "path", "embedding" },
            { "queryVector", new BsonArray(queryVector) },
            { "numCandidates", 100 },
            { "limit", pageSize }
        });

        return await _books.Aggregate()
            .AppendStage<Book>(pipeline)
            .ToListAsync();
    }

    public async Task<long> SearchCountAsync(string query)
    {
        var queryVector = await _embeddingService.GetEmbeddingAsync(query);

        var pipeline = new BsonDocument("$vectorSearch", new BsonDocument
        {
            { "index", "vector_index" },
            { "path", "embedding" },
            { "queryVector", new BsonArray(queryVector) },
            { "numCandidates", 100 },
            { "limit", 100 }
        });

        var results = await _books.Aggregate()
            .AppendStage<Book>(pipeline)
            .ToListAsync();

        return results.Count;
    }

    // ──────────────────────────────────────────────
    //  Embedding generation
    // ──────────────────────────────────────────────

    public async Task GenerateEmbeddingsAsync()
    {
        var filter = Builders<Book>.Filter.Eq(b => b.Embedding, null);
        var booksWithoutEmbeddings = await _books.Find(filter).ToListAsync();

        if (booksWithoutEmbeddings.Count == 0) return;

        // Process in batches of 20
        const int batchSize = 20;
        for (int i = 0; i < booksWithoutEmbeddings.Count; i += batchSize)
        {
            var batch = booksWithoutEmbeddings.Skip(i).Take(batchSize).ToList();
            var texts = batch.Select(BuildEmbeddingText).ToArray();
            var embeddings = await _embeddingService.GetEmbeddingsAsync(texts);

            var updates = new List<WriteModel<Book>>();
            for (int j = 0; j < batch.Count; j++)
            {
                var update = Builders<Book>.Update.Set(b => b.Embedding, embeddings[j]);
                updates.Add(new UpdateOneModel<Book>(
                    Builders<Book>.Filter.Eq(b => b.Id, batch[j].Id), update));
            }

            await _books.BulkWriteAsync(updates);
        }
    }

    private static string BuildEmbeddingText(Book book)
    {
        var parts = new List<string> { book.Title };

        if (book.Authors.Count > 0)
            parts.Add(string.Join(", ", book.Authors.Select(a => a.Name)));

        if (book.Genres is { Count: > 0 })
            parts.Add(string.Join(", ", book.Genres));

        return string.Join(" ", parts);
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
