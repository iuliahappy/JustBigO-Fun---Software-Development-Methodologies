using Microsoft.Extensions.Configuration;
using Moq;
using Moq.Protected;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace JustBigO_Fun_.Tests.Services
{
    public class AgentComplexityAnalyzerTests
    {
        [Fact]
        public async Task AnalyzeCodeAsync_ReturnsDefault_WhenCodeIsEmpty()
        {
            // Arrange
            var mockHandler = new Mock<HttpMessageHandler>();
            var httpClient = new HttpClient(mockHandler.Object);
            var mockConfig = new Mock<IConfiguration>();
            var service = new AgentComplexityAnalyzer(httpClient, mockConfig.Object);

            // Act
            var (time, space) = await service.AnalyzeCodeAsync("");

            // Assert
            Assert.Equal("O(1)", time);
            Assert.Equal("O(1)", space);
        }

        [Fact]
        public async Task AnalyzeCodeAsync_ReturnsError_WhenApiKeyIsMissing()
        {
            // Arrange
            var mockHandler = new Mock<HttpMessageHandler>();
            var httpClient = new HttpClient(mockHandler.Object);
            var mockConfig = new Mock<IConfiguration>();
            mockConfig.Setup(c => c["GeminiApiKey"]).Returns((string?)null);
            var service = new AgentComplexityAnalyzer(httpClient, mockConfig.Object);

            // Act
            var (time, space) = await service.AnalyzeCodeAsync("public void Test() {}");

            // Assert
            Assert.Equal("Eroare", time);
            Assert.Contains("Cheie API lipsă", space);
        }
    }
}
