using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Serializers;

namespace Leafy_Library.Models;

[BsonIgnoreExtraElements]
public class Book
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("title")]
    public string Title { get; set; } = string.Empty;

    [BsonElement("authors")]
    [BsonIgnoreIfNull]
    public List<BookAuthor> Authors { get; set; } = [];

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

    [BsonElement("bookOfTheMonth")]
    [BsonIgnoreIfNull]
    public bool? BookOfTheMonth { get; set; }

    [BsonElement("embeddings")]
    [BsonIgnoreIfNull]
    public double[]? Embedding { get; set; }
}

/// <summary>
/// Lightweight author reference embedded in a book document (extended reference pattern).
/// </summary>
[BsonIgnoreExtraElements]
public class BookAuthor
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;
}

[BsonIgnoreExtraElements]
public class BookAttribute
{
    [BsonElement("key")]
    public string Key { get; set; } = string.Empty;

    [BsonElement("value")]
    [BsonSerializer(typeof(FlexibleStringSerializer))]
    public string Value { get; set; } = string.Empty;
}

public class FlexibleStringSerializer : SerializerBase<string>
{
    public override string Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        var bsonType = context.Reader.GetCurrentBsonType();
        if (bsonType == BsonType.Null)
        {
            context.Reader.ReadNull();
            return string.Empty;
        }
        return bsonType switch
        {
            BsonType.String => context.Reader.ReadString(),
            BsonType.Double => context.Reader.ReadDouble().ToString(),
            BsonType.Int32 => context.Reader.ReadInt32().ToString(),
            BsonType.Int64 => context.Reader.ReadInt64().ToString(),
            BsonType.Boolean => context.Reader.ReadBoolean().ToString(),
            BsonType.ObjectId => context.Reader.ReadObjectId().ToString(),
            _ => BsonSerializer.Deserialize<BsonValue>(context.Reader).ToString()!,
        };
    }

    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, string value)
    {
        context.Writer.WriteString(value);
    }
}


