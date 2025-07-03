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
            _logger.LogInformation("Google search URL: {SearchUrl}", searchUrl);
            
            var response = await _httpClient.GetAsync(searchUrl, cancellationToken);

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Google search failed with status: {StatusCode}, Response: {ResponseContent}", 
                    response.StatusCode, responseContent);
                return new List<SearchResult>();
            }
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
        // Build base URL with only required parameters first
        var baseUrl = $"{GoogleSearchApiUrl}?key={_apiKey}&cx={_searchEngineId}&q={Uri.EscapeDataString(query)}";
        
        // Add number of results (max 10 for Custom Search API)
        var clampedResults = Math.Max(1, Math.Min(numResults, 10));
        baseUrl += $"&num={clampedResults}";
        
        // Only add optional parameters if they're meaningful
        if (!string.IsNullOrEmpty(lang) && lang.ToLower() != "en")
        {
            baseUrl += $"&lr=lang_{lang.ToLower()}";
        }
        
        if (!string.IsNullOrEmpty(country) && country.ToLower() != "us")
        {
            baseUrl += $"&gl={country.ToLower()}";
        }

        return baseUrl;
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