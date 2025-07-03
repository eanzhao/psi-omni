using Microsoft.Extensions.Logging;
using Moq;
using PsiGAgent.Plugins.Models;
using PsiGAgent.Plugins.Services;

namespace PsiGAgent.Common.Tests;

public class WebSearchServiceTests
{
    [Fact]
    public async Task WebSearchService_WithoutApiKey_ReturnsError()
    {
        // Arrange
        var httpClient = new HttpClient();
        var logger = new Mock<ILogger<WebSearchService>>();
        var contentFetcher = new Mock<IWebContentFetcher>();
        
        // Clear any existing API keys
        Environment.SetEnvironmentVariable("GOOGLE_API_KEY", null);
        Environment.SetEnvironmentVariable("GOOGLE_SEARCH_ENGINE_ID", null);
        
        // Create mock search engines
        var googleEngine = new Mock<ISearchEngine>();
        googleEngine.Setup(x => x.Name).Returns("google");
        googleEngine.Setup(x => x.PerformSearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new List<SearchResult>());
        
        var searchEngines = new List<ISearchEngine> { googleEngine.Object };
        var service = new WebSearchService(logger.Object, contentFetcher.Object, searchEngines);

        // Act
        var result = await service.ExecuteAsync("test query");

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Equal("test query", result.Metadata.Query);
    }

    [Fact]
    public async Task WebSearchService_WithFailingEngines_ReturnsError()
    {
        // Arrange
        var httpClient = new HttpClient();
        var logger = new Mock<ILogger<WebSearchService>>();
        var contentFetcher = new Mock<IWebContentFetcher>();
        
        // Create mock search engines that fail
        var googleEngine = new Mock<ISearchEngine>();
        googleEngine.Setup(x => x.Name).Returns("google");
        googleEngine.Setup(x => x.PerformSearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new List<SearchResult>());
        
        var searchEngines = new List<ISearchEngine> { googleEngine.Object };
        var service = new WebSearchService(logger.Object, contentFetcher.Object, searchEngines);

        // Act
        var result = await service.ExecuteAsync("test query");

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Equal("test query", result.Metadata.Query);
    }

    [Fact]
    public void SearchResponse_DefaultValues_MatchOpenManus()
    {
        // Arrange & Act
        var response = new SearchResponse();

        // Assert - matches OpenManus defaults
        Assert.True(response.Success);
        Assert.Empty(response.Results);
        Assert.Equal(0, response.TotalResults);
        Assert.NotNull(response.Metadata);
        Assert.Equal("en", response.Metadata.Lang);
        Assert.Equal("us", response.Metadata.Country);
        Assert.False(response.Metadata.FetchContent);
    }

    [Fact]
    public void SearchResult_Properties_MatchOpenManus()
    {
        // Arrange & Act
        var result = new SearchResult
        {
            Position = 1,
            Url = "https://example.com",
            Title = "Test Title",
            Description = "Test Description",
            Source = "google",
            RawContent = "Full content here"
        };

        // Assert - matches OpenManus SearchResult structure
        Assert.Equal(1, result.Position);
        Assert.Equal("https://example.com", result.Url);
        Assert.Equal("Test Title", result.Title);
        Assert.Equal("Test Description", result.Description);
        Assert.Equal("google", result.Source);
        Assert.Equal("Full content here", result.RawContent);
    }
}