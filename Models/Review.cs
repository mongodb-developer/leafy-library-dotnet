using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Leafy_Library.Models;

public class Review
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("text")]
    [BsonIgnoreIfNull]
    public string? Text { get; set; }

    [BsonElement("rating")]
    public int Rating { get; set; }

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("timestamp")]
    public DateTime Timestamp { get; set; }

    [BsonElement("bookId")]
    public string BookId { get; set; } = string.Empty;
}
