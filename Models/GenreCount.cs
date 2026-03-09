using MongoDB.Bson.Serialization.Attributes;

namespace Leafy_Library.Models;

[BsonIgnoreExtraElements]
public class GenreCount
{
    [BsonId]
    public string Genre { get; set; } = string.Empty;

    [BsonElement("count")]
    public int Count { get; set; }
}
