using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Leafy_Library.Models;

[BsonIgnoreExtraElements]
public class User
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("username")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("isAdmin")]
    [BsonIgnoreIfNull]
    public bool? IsAdmin { get; set; }
}
