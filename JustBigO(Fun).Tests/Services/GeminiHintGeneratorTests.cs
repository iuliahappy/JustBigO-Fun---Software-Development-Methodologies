using Microsoft.Extensions.Configuration;
using Moq;
using Moq.Protected;
using System.Net;
using System.Net.Http;
using System.Threading;
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

        [Fact]
        public async Task GenerateHintAsync_StripsCodeAndLimitsToTwoSentences()
        {
            // Arrange
            var apiResponse = """
                {
                  "candidates": [
                    {
                      "content": {
                        "parts": [
                          {
                            "text": "Try tracking the needed counts with a hash map. ```python\nfor x in nums:\n    pass\n``` Then move two pointers only when the window is valid. Extra explanation that should be removed."
                          }
                        ]
                      }
                    }
                  ]
                }
                """;

            var mockHandler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            mockHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(apiResponse)
                });

            var httpClient = new HttpClient(mockHandler.Object);
            var mockConfig = new Mock<IConfiguration>();
            mockConfig.Setup(c => c["GeminiApiKey"]).Returns("fake-key");
            var service = new GeminiHintGenerator(httpClient, mockConfig.Object);

            // Act
            var result = await service.GenerateHintAsync("Two Sum", "desc", "code", "python");

            // Assert
            Assert.DoesNotContain("```", result);
            Assert.DoesNotContain("pass", result);
            Assert.DoesNotContain("Extra explanation", result);
            Assert.Equal("Try tracking the needed counts with a hash map. Then move two pointers only when the window is valid.", result);
        }
    }
}

