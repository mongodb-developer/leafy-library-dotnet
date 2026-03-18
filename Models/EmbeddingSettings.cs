namespace Leafy_Library.Models;

public class EmbeddingSettings
{
    public string Provider { get; set; } = string.Empty; // "OpenAI", "AzureOpenAI", "VoyageAI"
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int Dimensions { get; set; } = 1536;
    public string? Endpoint { get; set; } // Required for Azure OpenAI
}
