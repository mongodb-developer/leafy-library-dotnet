using Leafy_Library.Models;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver.Search;
using MongoDB.Driver;

namespace Leafy_Library.Services;

public class DatabaseService
{
    public IMongoCollection<Book> Books { get; }
    public IMongoCollection<Author> Authors { get; }
    public IMongoCollection<Review> Reviews { get; }
    public IMongoCollection<User> Users { get; }
    public IMongoCollection<IssueDetail> IssueDetails { get; }

    private readonly int _embeddingDimensions;
    private readonly ILogger<DatabaseService> _logger;

    public DatabaseService(IOptions<MongoDbSettings> settings, IOptions<EmbeddingSettings> embeddingSettings, ILogger<DatabaseService> logger)
    {
        _logger = logger;
        var connectionString = settings.Value.ConnectionString;

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Missing database connection string! Open appsettings.json and add your MongoDB connection string to the MongoDb:ConnectionString setting.");
        }

        var mongoSettings = MongoClientSettings.FromConnectionString(connectionString);
        mongoSettings.ApplicationName = "devrel.book.building-modern-data-applications-with-mongodb";
        mongoSettings.ServerApi = new ServerApi(ServerApiVersion.V1);

        var client = new MongoClient(mongoSettings);
        var database = client.GetDatabase(settings.Value.DatabaseName);

        Books = database.GetCollection<Book>("books");
        Authors = database.GetCollection<Author>("authors");
        Reviews = database.GetCollection<Review>("reviews");
        Users = database.GetCollection<User>("users");
        IssueDetails = database.GetCollection<IssueDetail>("issueDetails");

        _embeddingDimensions = embeddingSettings.Value.Dimensions;
    }

    public async Task EnsureSearchIndexAsync()
    {
        const string indexName = "fulltextsearch";

        var cursor = await Books.SearchIndexes.ListAsync();
        var existing = await cursor.ToListAsync();
        if (existing.Any(i => i["name"].AsString == indexName))
        {
            return;
        }

        var definition = new BsonDocument
        {
            { "mappings", new BsonDocument
                {
                    { "dynamic", false },
                    { "fields", new BsonDocument
                        {
                            { "title", new BsonArray
                                {
                                    new BsonDocument { { "type", "string" }, { "analyzer", "lucene.english" } },
                                    new BsonDocument { { "type", "autocomplete" }, { "tokenization", "edgeGram" }, { "minGrams", 2 }, { "maxGrams", 15 } }
                                }
                            },
                            { "authors", new BsonDocument
                                {
                                    { "type", "document" },
                                    { "fields", new BsonDocument
                                        {
                                            { "name", new BsonDocument { { "type", "string" } } }
                                        }
                                    }
                                }
                            },
                            { "genres", new BsonArray
                                {
                                    new BsonDocument { { "type", "string" } },
                                    new BsonDocument { { "type", "stringFacet" } }
                                }
                            }
                        }
                    }
                }
            }
        };

        var model = new CreateSearchIndexModel(indexName, definition);
        await Books.SearchIndexes.CreateOneAsync(model);

        _logger.LogInformation(
            "Search index '{IndexName}' creation started. Waiting for it to become queryable...",
            indexName);

        while (true)
        {
            await Task.Delay(TimeSpan.FromSeconds(2));
            cursor = await Books.SearchIndexes.ListAsync();
            var indexes = await cursor.ToListAsync();
            var index = indexes.FirstOrDefault(i => i["name"].AsString == indexName);
            if (index != null && index.GetValue("queryable", false).AsBoolean)
            {
                break;
            }
        }

        _logger.LogInformation("Search index '{IndexName}' is ready.", indexName);
    }

    public async Task EnsureVectorSearchIndexAsync()
    {
        const string indexName = "vectorsearch";

        using var cursor = await Books.SearchIndexes.ListAsync(indexName);
        var indexes = await cursor.ToListAsync();

        if (indexes.Any(i => i["name"] == indexName))
        {
            return;
        }

        var definition = new BsonDocument
        {
            { "fields", new BsonArray
                {
                    new BsonDocument
                    {
                        { "type", "vector" },
                        { "path", "embeddings" },
                        { "numDimensions", _embeddingDimensions },
                        { "similarity", "cosine" }
                    }
                }
            }
        };

        var model = new CreateSearchIndexModel(indexName, SearchIndexType.VectorSearch, definition);
        await Books.SearchIndexes.CreateOneAsync(model);

        Console.WriteLine($"Waiting for '{indexName}' index to be ready...");

        while (true)
        {
            using var statusCursor = await Books.SearchIndexes.ListAsync(indexName);
            var statusList = await statusCursor.ToListAsync();
            var index = statusList.FirstOrDefault(i => i["name"] == indexName);

            if (index is not null && index["queryable"].AsBoolean)
            {
                break;
            }

            await Task.Delay(1000);
        }
    }

    public async Task GenerateEmbeddingsAsync(EmbeddingService embeddingService)
    {
        var filter = Builders<Book>.Filter.Exists(b => b.Embedding, false)
            | Builders<Book>.Filter.Eq(b => b.Embedding, null);

        var booksWithoutEmbeddings = await Books
            .Find(filter)
            .ToListAsync();

        if (booksWithoutEmbeddings.Count == 0)
            return;

        Console.WriteLine($"Generating embeddings for {booksWithoutEmbeddings.Count} books...");

        foreach (var book in booksWithoutEmbeddings)
        {
            var text = embeddingService.BuildEmbeddingText(book);
            var embedding = await embeddingService.GetEmbeddingAsync(text);

            var update = Builders<Book>.Update.Set(b => b.Embedding, embedding);
            await Books.UpdateOneAsync(b => b.Id == book.Id, update);
        }

        Console.WriteLine("Embedding generation complete.");
    }
}
