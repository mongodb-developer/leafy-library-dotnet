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
    public string Username { get; set; } = string.Empty;

    [BsonElement("isAdmin")]
    public bool IsAdmin { get; set; } = false;
}
