using Microsoft.Extensions.Configuration;
using Moq;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;
using JustBigO_Fun_.Services;

namespace JustBigO_Fun_.Tests.Services
{
    public class GeminiRefactoringSuggestionGeneratorTests
    {
        [Fact]
        public async Task GenerateAsync_ReturnsError_WhenApiKeyIsMissing()
        {
            // Arrange
            var mockHandler = new Mock<HttpMessageHandler>();
            var httpClient = new HttpClient(mockHandler.Object);
            var mockConfig = new Mock<IConfiguration>();
            mockConfig.Setup(c => c["GeminiApiKey"]).Returns((string?)null);
            var service = new GeminiRefactoringSuggestionGenerator(httpClient, mockConfig.Object);

            // Act
            var result = await service.GenerateAsync("Title", "Desc", "Code", "csharp");

            // Assert
            Assert.Contains("Gemini API key is not configured", result.RefactoringSteps);
        }
    }
}
