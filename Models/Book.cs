using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Leafy_Library.Models;

public class Book
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("title")]
    public string Title { get; set; } = string.Empty;

    [BsonElement("authors")]
    public List<Author> Authors { get; set; } = [];

    [BsonElement("genres")]
    [BsonIgnoreIfNull]
    public List<string>? Genres { get; set; }

    [BsonElement("pages")]
    [BsonIgnoreIfNull]
    public int? Pages { get; set; }

    [BsonElement("year")]
    [BsonIgnoreIfNull]
    public int? Year { get; set; }

    [BsonElement("synopsis")]
    [BsonIgnoreIfNull]
    public string? Synopsis { get; set; }

    [BsonElement("cover")]
    [BsonIgnoreIfNull]
    public string? Cover { get; set; }

    [BsonElement("attributes")]
    public List<BookAttribute> Attributes { get; set; } = [];

    [BsonElement("totalInventory")]
    public int TotalInventory { get; set; }

    [BsonElement("available")]
    public int Available { get; set; }

    [BsonElement("binding")]
    [BsonIgnoreIfNull]
    public string? Binding { get; set; }

    [BsonElement("language")]
    [BsonIgnoreIfNull]
    public string? Language { get; set; }

    [BsonElement("publisher")]
    [BsonIgnoreIfNull]
    public string? Publisher { get; set; }

    [BsonElement("longTitle")]
    [BsonIgnoreIfNull]
    public string? LongTitle { get; set; }

    [BsonElement("reviews")]
    public List<Review> Reviews { get; set; } = [];
}

public class BookAttribute
{
    [BsonElement("key")]
    public string Key { get; set; } = string.Empty;

    [BsonElement("value")]    
    public string Value { get; set; } = string.Empty;
}


