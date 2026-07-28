using Leafy_Library.Models;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Leafy_Library.Services;

public class DatabaseService
{
    private readonly ILogger<DatabaseService> _logger;

    public IMongoCollection<Book> Books { get; }
    public IMongoCollection<Author> Authors { get; }
    public IMongoCollection<Review> Reviews { get; }
    public IMongoCollection<User> Users { get; }
    public IMongoCollection<IssueDetail> IssueDetails { get; }

    public DatabaseService(IOptions<MongoDbSettings> settings, ILogger<DatabaseService> logger)
    {
        _logger = logger;

        var connectionString = settings.Value.ConnectionString;

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Missing database connection string! Open appsettings.json and add your MongoDB connection string to the MongoDb:ConnectionString setting.");
        }

        var mongoSettings = MongoClientSettings.FromConnectionString(connectionString);
        mongoSettings.ApplicationName = "devrel.book.building-intelligent-data-applications-with-mongodb";

        var client = new MongoClient(mongoSettings);
        var database = client.GetDatabase(settings.Value.DatabaseName);

        Books = database.GetCollection<Book>("books");
        Authors = database.GetCollection<Author>("authors");
        Reviews = database.GetCollection<Review>("reviews");
        Users = database.GetCollection<User>("users");
        IssueDetails = database.GetCollection<IssueDetail>("issueDetails");
    }

    public async Task EnsureSearchIndexAsync()
    {
        const string indexName = "fulltextsearch";

        using var cursor = await Books.SearchIndexes.ListAsync();
        var indexes = await cursor.ToListAsync();

        if (indexes.Any(i => i.GetValue("name", string.Empty).AsString == indexName))
        {
            _logger.LogInformation("Search index '{IndexName}' already exists.", indexName);
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
                                    new BsonDocument
                                    {
                                        { "type", "string" },
                                        { "analyzer", "lucene.english" }
                                    },
                                    new BsonDocument
                                    {
                                        { "type", "autocomplete" },
                                        { "tokenization", "edgeGram" },
                                        { "minGrams", 2 },
                                        { "maxGrams", 15 }
                                    }
                                }
                            },
                            { "authors", new BsonDocument
                                {
                                    { "type", "document" },
                                    { "fields", new BsonDocument
                                        {
                                            { "name", new BsonDocument("type", "string") }
                                        }
                                    }
                                }
                            },
                            { "genres", new BsonArray
                                {
                                    new BsonDocument("type", "string"),
                                    new BsonDocument("type", "stringFacet")
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
            using var statusCursor = await Books.SearchIndexes.ListAsync();
            var statusList = await statusCursor.ToListAsync();
            var index = statusList.FirstOrDefault(i => i.GetValue("name", string.Empty).AsString == indexName);

            if (index is not null && index.GetValue("queryable", false).AsBoolean)
            {
                break;
            }

            await Task.Delay(TimeSpan.FromSeconds(2));
        }

        _logger.LogInformation("Search index '{IndexName}' is ready.", indexName);
    }
}
