using MongoDB.Bson.Serialization.Attributes;

namespace Leafy_Library.Models;

[BsonIgnoreExtraElements]
public class LoanSummary
{
    [BsonElement("title")]
    public string Title { get; set; } = string.Empty;

    [BsonElement("borrowDate")]
    public DateTime? BorrowDate { get; set; }

    [BsonElement("dueDate")]
    public DateTime? DueDate { get; set; }

    [BsonElement("daysToDue")]
    public int DaysToDue { get; set; }

    [BsonElement("status")]
    public string Status { get; set; } = string.Empty;
}
