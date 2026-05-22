using Leafy_Library.Models;
using Microsoft.Extensions.Options;
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
        mongoSettings.ApplicationName = "devrel.book.building-modern-data-applications-with-mongodb-app";
        mongoSettings.ServerApi = new ServerApi(ServerApiVersion.V1);

        var client = new MongoClient(mongoSettings);
        var database = client.GetDatabase(settings.Value.DatabaseName);

        Books = database.GetCollection<Book>("books");
        Authors = database.GetCollection<Author>("authors");
        Reviews = database.GetCollection<Review>("reviews");
        Users = database.GetCollection<User>("users");
        IssueDetails = database.GetCollection<IssueDetail>("issueDetails");
    }
}
