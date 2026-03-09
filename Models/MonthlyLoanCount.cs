using MongoDB.Bson.Serialization.Attributes;

namespace Leafy_Library.Models;

[BsonIgnoreExtraElements]
public class MonthlyLoanCount
{
    [BsonId]
    public string Month { get; set; } = string.Empty;

    [BsonElement("count")]
    public int Count { get; set; }
}
