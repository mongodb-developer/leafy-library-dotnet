using Leafy_Library.Models;
using MongoDB.Driver;

namespace Leafy_Library.Services;

public class AuthorService
{
    private readonly IMongoCollection<Author> _authors;
    private readonly IMongoCollection<Book> _books;

    public AuthorService(DatabaseService db)
    {
        _authors = db.Authors;
        _books = db.Books;
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

    /// <summary>
    /// Gets an author by ID and resolves their book ISBNs into title/cover references.
    /// </summary>
    public async Task<AuthorResponse?> GetAuthorWithBooksAsync(string id)
    {
        var author = await GetByIdAsync(id);
        if (author is null) return null;

        var bookFilter = Builders<Book>.Filter.In(b => b.Id, author.Books);
        var books = await _books
            .Find(bookFilter)
            .Project(b => new AuthorBookReference
            {
                Isbn = b.Id,
                Title = b.Title,
                Cover = b.Cover
            })
            .ToListAsync();

        return new AuthorResponse
        {
            Id = author.Id,
            Name = author.Name,
            SanitizedName = author.SanitizedName,
            Bio = author.Bio,
            Aliases = author.Aliases,
            Books = books
        };
    }
}
