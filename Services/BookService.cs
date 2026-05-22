using Leafy_Library.Models;
using MongoDB.Bson;
using MongoDB.Driver.Search;
using MongoDB.Driver;

namespace Leafy_Library.Services;

public class BookService
{
    private readonly IMongoCollection<Book> _books;
    private readonly EmbeddingService _embeddingService;
    private readonly ILogger<BookService> _logger;

    public BookService(DatabaseService db, EmbeddingService embeddingService, ILogger<BookService> logger)
    {
        _books = db.Books;
        _embeddingService = embeddingService;
        _logger = logger;
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

    // Tutorial chapter API

    public async Task<List<Book>> SearchAsync(string query, int page, int pageSize)
    {
        try
        {
            var searchDef = Builders<Book>.Search.Text(
                Builders<Book>.SearchPath.Multi("title", "authors.name", "genres"),
                query);

            return await _books
                .Aggregate()
                .Search(searchDef, indexName: "fulltextsearch")
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync();
        }
        catch (MongoCommandException ex) when (ex.Message.Contains("index not found"))
        {
            _logger.LogWarning("Search index not found when running query: {Message}", ex.Message);
            return [];
        }
        catch (MongoCommandException ex)
        {
            _logger.LogError(ex, "Search query failed unexpectedly");
            return [];
        }
    }

    public async Task<long> SearchCountAsync(string query)
    {
        var searchDef = Builders<Book>.Search.Text(
            Builders<Book>.SearchPath.Multi("title", "authors.name", "genres"),
            query);

        var result = await _books
            .Aggregate()
            .Search(searchDef, indexName: "fulltextsearch")
            .Count()
            .FirstOrDefaultAsync();

        return result?.Count ?? 0;
    }

    public async Task<List<string>> GetAutocompleteSuggestionsAsync(string query)
    {
        var searchDef = Builders<Book>.Search.Autocomplete(
            "title",
            query,
            SearchAutocompleteTokenOrder.Any);

        return await _books
            .Aggregate()
            .Search(searchDef, indexName: "fulltextsearch")
            .Limit(5)
            .Project<string>(Builders<Book>.Projection.Expression(x => x.Title))
            .ToListAsync();
    }

    public async Task<Dictionary<string, int>> GetGenreFacetsAsync(string query)
    {
        var pipeline = new BsonDocument[]
        {
            new BsonDocument("$searchMeta", new BsonDocument
            {
                { "index", "fulltextsearch" },
                { "facet", new BsonDocument
                    {
                        { "operator", new BsonDocument
                            {
                                { "text", new BsonDocument
                                    {
                                        { "query", query },
                                        { "path", new BsonArray { "title", "authors.name", "genres" } }
                                    }
                                }
                            }
                        },
                        { "facets", new BsonDocument
                            {
                                { "genreFacet", new BsonDocument
                                    {
                                        { "type", "string" },
                                        { "path", "genres" },
                                        { "numBuckets", 10 }
                                    }
                                }
                            }
                        }
                    }
                }
            })
        };

        var result = await _books
            .Aggregate<BsonDocument>(pipeline)
            .FirstOrDefaultAsync();

        if (result == null) return [];

        var buckets = result["facet"]["genreFacet"]["buckets"].AsBsonArray;

        return buckets.ToDictionary(
            b => b["_id"].AsString,
            b => b["count"].ToInt32());
    }

    public async Task<List<Book>> SearchInGenreAsync(string query, string genre, int page, int pageSize)
    {
        var searchDef = Builders<Book>.Search.Compound()
            .Must(Builders<Book>.Search.Text(
                Builders<Book>.SearchPath.Multi("title", "authors.name", "genres"),
                query))
            .Filter(Builders<Book>.Search.Text("genres", genre));

        return await _books
            .Aggregate()
            .Search(searchDef, indexName: "fulltextsearch")
            .Match(b => b.Genres != null && b.Genres.Contains(genre))
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync();
    }

    public async Task<long> SearchInGenreCountAsync(string query, string genre)
    {
        var searchDef = Builders<Book>.Search.Compound()
            .Must(Builders<Book>.Search.Text(
                Builders<Book>.SearchPath.Multi("title", "authors.name", "genres"),
                query))
            .Filter(Builders<Book>.Search.Text("genres", genre));

        var result = await _books
            .Aggregate()
            .Search(searchDef, indexName: "fulltextsearch")
            .Match(b => b.Genres != null && b.Genres.Contains(genre))
            .Count()
            .FirstOrDefaultAsync();

        return result?.Count ?? 0;
    }

    // Existing API wrappers used by controllers/other branch work

    public Task<List<Book>> LexicalSearchAsync(string query, int page = 1, int pageSize = 20) =>
        SearchAsync(query, page, pageSize);

    public Task<long> LexicalSearchCountAsync(string query) =>
        SearchCountAsync(query);

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
