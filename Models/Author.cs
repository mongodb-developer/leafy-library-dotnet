using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Leafy_Library.Models;

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
