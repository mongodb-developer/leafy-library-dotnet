using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Serializers;

namespace Leafy_Library.Models;

[BsonIgnoreExtraElements]
public class Review
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("text")]
    public string Text { get; set; } = string.Empty;

    [BsonElement("rating")]
    [BsonIgnoreIfNull]
    public int? Rating { get; set; }

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("timestamp")]
    [BsonSerializer(typeof(FlexibleTimestampSerializer))]
    public long Timestamp { get; set; }

    [BsonElement("bookId")]
    public string BookId { get; set; } = string.Empty;
}

/// <summary>
/// Handles timestamp fields stored as either BSON Int64 (epoch ms) or BSON DateTime.
/// </summary>
public class FlexibleTimestampSerializer : SerializerBase<long>
{
    public override long Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        var bsonType = context.Reader.GetCurrentBsonType();
        return bsonType switch
        {
            BsonType.Int64 => context.Reader.ReadInt64(),
            BsonType.Int32 => context.Reader.ReadInt32(),
            BsonType.Double => (long)context.Reader.ReadDouble(),
            BsonType.DateTime => context.Reader.ReadDateTime(),
            _ => context.Reader.ReadInt64(),
        };
    }

    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, long value)
    {
        context.Writer.WriteInt64(value);
    }
}
