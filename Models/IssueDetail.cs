using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Leafy_Library.Models;

/// <summary>
/// IssueDetail follows the single-collection pattern.
/// The detail is either a borrowed book or a reservation,
/// indicated by the RecordType field.
/// </summary>
[BsonIgnoreExtraElements]
public class IssueDetail
{
    /// <summary>
    /// Identifier with the format 'userId_objectId' to optimize querying by user.
    /// </summary>
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("recordType")]
    public string RecordType { get; set; } = string.Empty;

    /// <summary>
    /// Reference to the book collection (extended reference pattern).
    /// </summary>
    [BsonElement("book")]
    public IssueDetailBook Book { get; set; } = null!;

    /// <summary>
    /// Reference to the user collection (extended reference pattern).
    /// </summary>
    [BsonElement("user")]
    public IssueDetailUser User { get; set; } = null!;

    // BorrowedBook fields

    [BsonElement("borrowDate")]
    [BsonIgnoreIfNull]
    public DateTime? BorrowDate { get; set; }

    [BsonElement("dueDate")]
    [BsonIgnoreIfNull]
    public DateTime? DueDate { get; set; }

    [BsonElement("returnedDate")]
    [BsonIgnoreIfNull]
    public DateTime? ReturnedDate { get; set; }

    [BsonElement("returned")]
    [BsonIgnoreIfNull]
    public bool? Returned { get; set; }

    // Reservation fields

    /// <summary>
    /// TTL index applied to this field to automatically remove the reservation.
    /// </summary>
    [BsonElement("expirationDate")]
    [BsonIgnoreIfNull]
    public DateTime? ExpirationDate { get; set; }
}

[BsonIgnoreExtraElements]
public class IssueDetailBook
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("title")]
    public string Title { get; set; } = string.Empty;
}

[BsonIgnoreExtraElements]
public class IssueDetailUser
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;
}

public static class IssueDetailType
{
    public const string Reservation = "R";
    public const string BorrowedBook = "B";
}
