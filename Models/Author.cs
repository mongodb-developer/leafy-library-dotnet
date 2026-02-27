using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Leafy_Library.Models;

[BsonIgnoreExtraElements]
public class Author
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("sanitizedName")]
    public string SanitizedName { get; set; } = string.Empty;

    [BsonElement("bio")]
    [BsonIgnoreIfNull]
    public string? Bio { get; set; }

    [BsonElement("books")]
    public List<string> Books { get; set; } = [];

    [BsonElement("aliases")]
    public List<string> Aliases { get; set; } = [];
}

/// <summary>
/// Author response DTO with resolved book references.
/// </summary>
public class AuthorResponse
{
    public string? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SanitizedName { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public List<string> Aliases { get; set; } = [];
    public List<AuthorBookReference> Books { get; set; } = [];
}

public class AuthorBookReference
{
    public string Isbn { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Cover { get; set; }
}
