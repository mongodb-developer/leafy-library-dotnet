using Leafy_Library.Models;
using MongoDB.Driver;

namespace Leafy_Library.Services;

public class AuthorService
{
    private readonly IMongoCollection<Author> _authors;

    public AuthorService(DatabaseService db)
    {
        _authors = db.Authors;
    }

    public async Task<List<Author>> GetAllAsync(int page = 1, int pageSize = 20)
    {
        return await _authors
            .Find(_ => true)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync();
    }

    public async Task<long> GetCountAsync()
    {
        return await _authors.CountDocumentsAsync(_ => true);
    }

    public async Task<Author?> GetByIdAsync(string id)
    {
        return await _authors
            .Find(a => a.Id == id)
            .FirstOrDefaultAsync();
    }

    public async Task<Author?> GetByNameAsync(string name)
    {
        var sanitized = name.ToLowerInvariant().Replace(" ", "");
        return await _authors
            .Find(a => a.SanitizedName == sanitized)
            .FirstOrDefaultAsync();
    }

    public async Task<List<Author>> SearchAsync(string query, int page = 1, int pageSize = 20)
    {
        var filter = Builders<Author>.Filter.Regex(
            a => a.Name,
            new MongoDB.Bson.BsonRegularExpression(query, "i"));

        return await _authors
            .Find(filter)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync();
    }
}
