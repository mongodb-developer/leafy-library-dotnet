using Leafy_Library.Models;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Leafy_Library.Services;

public class DatabaseService
{
    public IMongoCollection<Book> Books { get; }
    public IMongoCollection<Author> Authors { get; }
    public IMongoCollection<Review> Reviews { get; }
    public IMongoCollection<User> Users { get; }
    public IMongoCollection<IssueDetail> IssueDetails { get; }

    public DatabaseService(IOptions<MongoDbSettings> settings)
    {
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

        // Check if the index already exists
        using var cursor = await Books.SearchIndexes.ListAsync(indexName);
        var indexes = await cursor.ToListAsync();

        if (indexes.Any(i => i["name"] == indexName))
        {
            return;
        }

        // Create the search index
        var definition = new BsonDocument
        {
            { "mappings", new BsonDocument
                {
                    { "dynamic", false },
                    { "fields", new BsonDocument
                        {
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
                            { "genres", new BsonDocument("type", "string") },
                            { "title", new BsonDocument("type", "string") }
                        }
                    }
                }
            }
        };

        var model = new CreateSearchIndexModel(indexName, definition);
        await Books.SearchIndexes.CreateOneAsync(model);

        // Wait for the index to be ready
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
}
