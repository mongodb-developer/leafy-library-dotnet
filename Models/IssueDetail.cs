using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Leafy_Library.Models;

public class IssueDetail
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("book")]
    public Book Book { get; set; } = null!;

    [BsonElement("borrowDate")]
    public DateTime BorrowDate { get; set; }

    [BsonElement("dueDate")]
    public DateTime DueDate { get; set; }

    [BsonElement("recordType")]
    public string RecordType { get; set; } = string.Empty;

    [BsonElement("returned")]
    public bool Returned { get; set; }

    [BsonElement("returnedDate")]
    [BsonIgnoreIfNull]
    public DateTime? ReturnedDate { get; set; }

    [BsonElement("user")]
    public User User { get; set; } = null!;
}
