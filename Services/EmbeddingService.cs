using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Leafy_Library.Models;
using Microsoft.Extensions.Options;

namespace Leafy_Library.Services;

public class EmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly EmbeddingSettings _settings;

    public EmbeddingService(HttpClient httpClient, IOptions<EmbeddingSettings> settings)
    {
        _settings = settings.Value;
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
    }

    public async Task<double[]> GetEmbeddingAsync(string text)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);

        // OpenAI/Azure OpenAI use "dimensions" while some providers (e.g. Voyage) use "output_dimension".
        var provider = _settings.Provider?.Trim().ToLowerInvariant() ?? string.Empty;
        object requestBody = provider switch
        {
            "voyageai" or "voyage" => new
            {
                model = _settings.Model,
                input = new[] { text },
                output_dimension = _settings.Dimensions
            },
            _ => new
            {
                model = _settings.Model,
                input = new[] { text },
                dimensions = _settings.Dimensions
            }
        };

        var content = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.PostAsync(_settings.Endpoint, content);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        var embeddingArray = doc.RootElement
            .GetProperty("data")[0]
            .GetProperty("embedding");

        var embedding = new double[embeddingArray.GetArrayLength()];
        var i = 0;
        foreach (var value in embeddingArray.EnumerateArray())
        {
            embedding[i++] = value.GetDouble();
        }

        return embedding;
    }

    public string BuildEmbeddingText(Book book)
    {
        var parts = new List<string> { book.Title };

        if (!string.IsNullOrWhiteSpace(book.Synopsis))
            parts.Add(book.Synopsis);

        if (book.Genres is { Count: > 0 })
            parts.Add(string.Join(", ", book.Genres));

        return string.Join(" | ", parts);
    }
}
