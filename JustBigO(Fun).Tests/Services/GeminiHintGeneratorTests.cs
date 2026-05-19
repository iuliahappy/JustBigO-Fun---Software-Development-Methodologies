using Microsoft.Extensions.Configuration;
using Moq;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;
using JustBigO_Fun_.Services;

namespace JustBigO_Fun_.Tests.Services
{
    public class GeminiHintGeneratorTests
    {
        [Fact]
        public async Task GenerateHintAsync_ReturnsMissingKeyMessage_WhenApiKeyIsMissing()
        {
            // Arrange
            var mockHandler = new Mock<HttpMessageHandler>();
            var httpClient = new HttpClient(mockHandler.Object);
            var mockConfig = new Mock<IConfiguration>();
            mockConfig.Setup(c => c["GeminiApiKey"]).Returns((string?)null);
            var service = new GeminiHintGenerator(httpClient, mockConfig.Object);

            // Act
            var result = await service.GenerateHintAsync("Title", "Desc", "Code", "csharp");

            // Assert
            Assert.Contains("GeminiApiKey is missing", result);
        }
    }
}
