using JustBigO_Fun_.Data;
using JustBigO_Fun_.Models;
using JustBigO_Fun_.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace JustBigO_Fun_.Tests.Services
{
    public class CurrentCodeCompletionServiceTests
    {
        [Fact]
        public async Task CompleteAsync_ReturnsError_WhenProblemNotFound()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: "ProblemNotFoundDb")
                .Options;

            using var db = new ApplicationDbContext(options);
            var mockApproach = new Mock<IApproachAnalyzer>();
            var mockCompleter = new Mock<ICodeCompleter>();
            var mockExecutor = new Mock<ICodeExecutor>();
            var mockLogger = new Mock<ILogger<CurrentCodeCompletionService>>();

            var service = new CurrentCodeCompletionService(db, mockApproach.Object, mockCompleter.Object, mockExecutor.Object, mockLogger.Object);

            // Act
            var result = await service.CompleteAsync(999, "code", "python");

            // Assert
            Assert.Equal(SubmissionStatus.SystemError, result.LastStatus);
            Assert.Equal("Problem not found.", result.Message);
        }

        [Fact]
        public async Task CompleteAsync_ReturnsError_WhenCodeIsEmpty()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: "EmptyCodeDb")
                .Options;

            using var db = new ApplicationDbContext(options);
            db.Problems.Add(new Problem { Id = 1, Title = "Test", Slug = "test", MethodName = "solve" });
            db.SaveChanges();

            var mockApproach = new Mock<IApproachAnalyzer>();
            var mockCompleter = new Mock<ICodeCompleter>();
            var mockExecutor = new Mock<ICodeExecutor>();
            var mockLogger = new Mock<ILogger<CurrentCodeCompletionService>>();

            var service = new CurrentCodeCompletionService(db, mockApproach.Object, mockCompleter.Object, mockExecutor.Object, mockLogger.Object);

            // Act
            var result = await service.CompleteAsync(1, "", "python");

            // Assert
            Assert.Equal(SubmissionStatus.SystemError, result.LastStatus);
            Assert.Contains("Editor is empty", result.Message);
        }
    }
}
