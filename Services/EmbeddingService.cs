using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Leafy_Library.Models;
using Microsoft.Extensions.Options;

namespace Leafy_Library.Services;

public class EmbeddingService
{
    private readonly EmbeddingSettings _settings;
    private readonly HttpClient _httpClient;

    public EmbeddingService(IOptions<EmbeddingSettings> settings, HttpClient httpClient)
    {
        _settings = settings.Value;
        _httpClient = httpClient;
    }

    // ──────────────────────────────────────────────
    //  Public API
    // ──────────────────────────────────────────────

    public async Task<double[]> GetEmbeddingAsync(string text)
    {
        return _settings.Provider.ToLowerInvariant() switch
        {
            "openai" => await GetOpenAIEmbeddingAsync(text),
            "azureopenai" => await GetAzureOpenAIEmbeddingAsync(text),
            "voyageai" => await GetVoyageAIEmbeddingAsync(text),
            _ => throw new InvalidOperationException(
                $"Unknown embedding provider '{_settings.Provider}'. Use 'OpenAI', 'AzureOpenAI', or 'VoyageAI'.")
        };
    }

    public async Task<double[][]> GetEmbeddingsAsync(string[] texts)
    {
        return _settings.Provider.ToLowerInvariant() switch
        {
            "openai" => await GetOpenAIEmbeddingsAsync(texts),
            "azureopenai" => await GetAzureOpenAIEmbeddingsAsync(texts),
            "voyageai" => await GetVoyageAIEmbeddingsAsync(texts),
            _ => throw new InvalidOperationException(
                $"Unknown embedding provider '{_settings.Provider}'. Use 'OpenAI', 'AzureOpenAI', or 'VoyageAI'.")
        };
    }

    // ──────────────────────────────────────────────
    //  OpenAI
    // ──────────────────────────────────────────────

    private async Task<double[]> GetOpenAIEmbeddingAsync(string text)
    {
        var result = await GetOpenAIEmbeddingsAsync([text]);
        return result[0];
    }

    private async Task<double[][]> GetOpenAIEmbeddingsAsync(string[] texts)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/embeddings");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);

        var body = new { input = texts, model = _settings.Model };
        request.Content = new StringContent(
            JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        return await SendAndParseAsync(request);
    }

    // ──────────────────────────────────────────────
    //  Azure OpenAI
    // ──────────────────────────────────────────────

    private async Task<double[]> GetAzureOpenAIEmbeddingAsync(string text)
    {
        var result = await GetAzureOpenAIEmbeddingsAsync([text]);
        return result[0];
    }

    private async Task<double[][]> GetAzureOpenAIEmbeddingsAsync(string[] texts)
    {
        if (string.IsNullOrWhiteSpace(_settings.Endpoint))
            throw new InvalidOperationException("Azure OpenAI requires 'Endpoint' in EmbeddingSettings.");

        var endpoint = _settings.Endpoint.TrimEnd('/');
        var url = $"{endpoint}/openai/deployments/{_settings.Model}/embeddings?api-version=2024-06-01";

        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("api-key", _settings.ApiKey);

        var body = new { input = texts };
        request.Content = new StringContent(
            JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        return await SendAndParseAsync(request);
    }

    // ──────────────────────────────────────────────
    //  VoyageAI
    // ──────────────────────────────────────────────

    private async Task<double[]> GetVoyageAIEmbeddingAsync(string text)
    {
        var result = await GetVoyageAIEmbeddingsAsync([text]);
        return result[0];
    }

    private async Task<double[][]> GetVoyageAIEmbeddingsAsync(string[] texts)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "https://ai.mongodb.com/v1/embeddings");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);

        var body = new { input = texts, model = _settings.Model, output_dimension = _settings.Dimensions, truncation = true };
        request.Content = new StringContent(
            JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        return await SendAndParseAsync(request);
    }

    // ──────────────────────────────────────────────
    //  Shared helpers
    // ──────────────────────────────────────────────

    private async Task<double[][]> SendAndParseAsync(HttpRequestMessage request)
    {
        using var response = await _httpClient.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Embedding API returned {response.StatusCode}: {responseBody}");
        }

        using var doc = JsonDocument.Parse(responseBody);
        var dataArray = doc.RootElement.GetProperty("data");

        var results = new double[dataArray.GetArrayLength()][];
        foreach (var item in dataArray.EnumerateArray())
        {
            var index = item.GetProperty("index").GetInt32();
            var embedding = item.GetProperty("embedding");
            var values = new double[embedding.GetArrayLength()];
            int i = 0;
            foreach (var val in embedding.EnumerateArray())
            {
                values[i++] = val.GetDouble();
            }
            results[index] = values;
        }

        return results;
    }
}
