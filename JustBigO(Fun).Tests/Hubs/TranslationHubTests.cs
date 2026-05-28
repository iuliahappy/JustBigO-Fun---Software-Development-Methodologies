using Moq;
using JustBigO_Fun_.Hubs;
using JustBigO_Fun_.Services.AI;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace JustBigO_Fun_.Tests.Hubs
{
    public class TranslationHubTests
    {
        [Fact]
        public async Task TranslateCode_HandlesTimeout_Gracefully()
        {
            // Arrange
            var mockTranslator = new Mock<ICodeTranslatorAgent>();
            
            // Setup the translator to throw OperationCanceledException (simulating a timeout)
            mockTranslator.Setup(t => t.TranslateWithReflexionAsync(
                It.IsAny<string>(), 
                It.IsAny<string>(), 
                It.IsAny<string>(), 
                It.IsAny<CancellationToken>()))
                .ThrowsAsync(new OperationCanceledException());

            var hub = new TranslationHub(mockTranslator.Object);
            
            // Mock SignalR Clients
            var mockClients = new Mock<IHubCallerClients>();
            var mockClientProxy = new Mock<ISingleClientProxy>();
            mockClients.Setup(c => c.Caller).Returns(mockClientProxy.Object);
            hub.Clients = mockClients.Object;

            // Act
            await hub.TranslateCode("public void Test() {}", "csharp", "python");

            // Assert
            // Verify that SendAsync was called with the timeout error message
            mockClientProxy.Verify(
                c => c.SendCoreAsync(
                    "ReceiveCodeChunk",
                    It.Is<object[]>(o => o[0].ToString().Contains("timed out")),
                    default), 
                Times.Once);
        }
    }
}
