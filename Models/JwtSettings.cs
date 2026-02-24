namespace Leafy_Library.Models;

public class JwtSettings
{
    public string Secret { get; set; } = string.Empty;
    public int ExpiryInDays { get; set; } = 365;
    public string Issuer { get; set; } = "LeafyLibrary";
    public string Audience { get; set; } = "LeafyLibraryUsers";
}
