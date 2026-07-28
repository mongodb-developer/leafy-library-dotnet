using Leafy_Library.Models;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Search;

namespace Leafy_Library.Services;

public class BookService
{
    private readonly IMongoCollection<Book> _books;
    private readonly ILogger<BookService> _logger;

    public BookService(DatabaseService db, ILogger<BookService> logger)
    {
        _books = db.Books;
        _logger = logger;
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

    public async Task<List<Book>> SearchAsync(string query, int page = 1, int pageSize = 20)
    {
        try
        {
            var searchDef = Builders<Book>.Search.Text(
                Builders<Book>.SearchPath.Multi("title", "authors.name", "genres"),
                query);

            return await _books.Aggregate()
                .Search(searchDef, indexName: "fulltextsearch")
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync();
        }
        catch (MongoCommandException ex) when (ex.Message.Contains("index not found", StringComparison.OrdinalIgnoreCase))
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
        try
        {
            var searchDef = Builders<Book>.Search.Text(
                Builders<Book>.SearchPath.Multi("title", "authors.name", "genres"),
                query);

            var result = await _books.Aggregate()
                .Search(searchDef, indexName: "fulltextsearch")
                .Count()
                .FirstOrDefaultAsync();

            return result?.Count ?? 0;
        }
        catch (MongoCommandException ex) when (ex.Message.Contains("index not found", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Search count index not found for query: {Message}", ex.Message);
            return 0;
        }
        catch (MongoCommandException ex)
        {
            _logger.LogError(ex, "Search count query failed unexpectedly");
            return 0;
        }
    }

    public async Task<List<string>> GetAutocompleteSuggestionsAsync(string query)
    {
        var searchDef = Builders<Book>.Search.Autocomplete(
            "title",
            query,
            SearchAutocompleteTokenOrder.Any);

        return await _books.Aggregate()
            .Search(searchDef, indexName: "fulltextsearch")
            .Limit(5)
            .Project(Builders<Book>.Projection.Expression(b => b.Title))
            .ToListAsync();
    }

    public async Task<Dictionary<string, int>> GetGenreFacetsAsync(string query)
    {
        var pipeline = new BsonDocument[]
        {
            new("$searchMeta", new BsonDocument
            {
                { "index", "fulltextsearch" },
                {
                    "facet", new BsonDocument
                    {
                        {
                            "operator", new BsonDocument
                            {
                                {
                                    "text", new BsonDocument
                                    {
                                        { "query", query },
                                        { "path", new BsonArray { "title", "authors.name", "genres" } }
                                    }
                                }
                            }
                        },
                        {
                            "facets", new BsonDocument
                            {
                                {
                                    "genreFacet", new BsonDocument
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

        var result = await _books.Aggregate<BsonDocument>(pipeline).FirstOrDefaultAsync();
        if (result is null)
        {
            return [];
        }

        if (!result.TryGetValue("facet", out var facetValue) || !facetValue.AsBsonDocument.TryGetValue("genreFacet", out var genreFacetValue))
        {
            return [];
        }

        var genreFacet = genreFacetValue.AsBsonDocument;
        if (!genreFacet.TryGetValue("buckets", out var bucketsValue))
        {
            return [];
        }

        var buckets = bucketsValue.AsBsonArray;
        return buckets.ToDictionary(
            b => b["_id"].AsString,
            b => b["count"].ToInt32());
    }

    public async Task<List<Book>> SearchInGenreAsync(string query, string genre, int page = 1, int pageSize = 20)
    {
        var searchDef = Builders<Book>.Search.Compound()
            .Must(Builders<Book>.Search.Text(
                Builders<Book>.SearchPath.Multi("title", "authors.name", "genres"),
                query))
            .Filter(Builders<Book>.Search.Text("genres", genre));

        return await _books.Aggregate()
            .Search(searchDef, indexName: "fulltextsearch")
            .Match(b => b.Genres != null && b.Genres.Contains(genre))
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync();
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
