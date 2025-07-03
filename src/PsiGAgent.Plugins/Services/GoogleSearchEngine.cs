using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using PsiGAgent.Plugins.Models;

namespace PsiGAgent.Plugins.Services;

/// <summary>
/// Google search engine implementation
/// </summary>
public class GoogleSearchEngine : ISearchEngine
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GoogleSearchEngine> _logger;
    private readonly string? _apiKey;
    private readonly string? _searchEngineId;
    private const string GoogleSearchApiUrl = "https://www.googleapis.com/customsearch/v1";

    public string Name => "google";

    public GoogleSearchEngine(HttpClient httpClient, ILogger<GoogleSearchEngine> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = Environment.GetEnvironmentVariable("GOOGLE_API_KEY");
        _searchEngineId = Environment.GetEnvironmentVariable("GOOGLE_SEARCH_ENGINE_ID");
    }

    public async Task<List<SearchResult>> PerformSearchAsync(
        string query,
        int numResults,
        string? lang,
        string? country,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_apiKey) || string.IsNullOrEmpty(_searchEngineId))
        {
            _logger.LogWarning("Google API credentials not configured");
            return new List<SearchResult>();
        }

        try
        {
            var searchUrl = BuildSearchUrl(query, numResults, lang, country);
            var response = await _httpClient.GetAsync(searchUrl, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Google search failed with status: {StatusCode}", response.StatusCode);
                return new List<SearchResult>();
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var googleResponse = JsonSerializer.Deserialize<GoogleSearchResponse>(responseContent);

            if (googleResponse?.Items == null)
            {
                return new List<SearchResult>();
            }

            var results = new List<SearchResult>();
            for (int i = 0; i < googleResponse.Items.Length && i < numResults; i++)
            {
                var item = googleResponse.Items[i];
                results.Add(new SearchResult
                {
                    Position = i + 1,
                    Url = item.Link ?? string.Empty,
                    Title = item.Title ?? $"Result {i + 1}",
                    Description = item.Snippet ?? string.Empty,
                    Source = Name
                });
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing Google search for query: {Query}", query);
            return new List<SearchResult>();
        }
    }

    private string BuildSearchUrl(string query, int numResults, string? lang, string? country)
    {
        var url = $"{GoogleSearchApiUrl}?key={_apiKey}&cx={_searchEngineId}&q={Uri.EscapeDataString(query)}&num={Math.Min(numResults, 10)}";

        if (!string.IsNullOrEmpty(lang))
        {
            url += $"&lr=lang_{lang}";
        }
        if (!string.IsNullOrEmpty(country))
        {
            url += $"&gl={country.ToLower()}";
        }

        return url;
    }

    private class GoogleSearchResponse
    {
        [JsonPropertyName("items")]
        public GoogleSearchItem[]? Items { get; set; }
    }

    private class GoogleSearchItem
    {
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("link")]
        public string? Link { get; set; }

        [JsonPropertyName("snippet")]
        public string? Snippet { get; set; }
    }
}