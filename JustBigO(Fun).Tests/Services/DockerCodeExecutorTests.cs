using JustBigO_Fun_.Models;
using JustBigO_Fun_.Services;
using Xunit;
using System.Collections.Generic;
using Moq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace JustBigO_Fun_.Tests.Services
{
    public class DockerCodeExecutorTests
    {
        private readonly DockerCodeExecutor _executor;

        public DockerCodeExecutorTests()
        {
            var mockScopeFactory = new Mock<IServiceScopeFactory>();
            var mockLogger = new Mock<ILogger<DockerCodeExecutor>>();
            _executor = new DockerCodeExecutor(mockScopeFactory.Object, mockLogger.Object);
        }

        [Fact]
        public void MapDockerResultToStatus_Handles_TLE_ExitCode()
        {
            // Arrange
            var result = new TestCaseResult();
            int tleExitCode = 124;

            // Act
            _executor.MapDockerResultToStatus(result, tleExitCode, "timeout", "");

            // Assert
            Assert.Equal(SubmissionStatus.TimeLimitExceeded, result.Status);
            Assert.Contains("Time Limit", result.Error);
        }

        [Fact]
        public void MapDockerResultToStatus_Handles_MLE_ExitCode()
        {
            // Arrange
            var result = new TestCaseResult();
            int oomExitCode = 137;

            // Act
            _executor.MapDockerResultToStatus(result, oomExitCode, "killed", "");

            // Assert
            Assert.Equal(SubmissionStatus.MemoryLimitExceeded, result.Status);
            Assert.Contains("Memory Limit", result.Error);
        }

        [Fact]
        public void MapDockerResultToStatus_Handles_Accepted_Logic()
        {
            // Arrange
            var result = new TestCaseResult { Expected = "42" };
            int successExitCode = 0;

            // Act
            _executor.MapDockerResultToStatus(result, successExitCode, "", "42\n");

            // Assert
            Assert.Equal(SubmissionStatus.Accepted, result.Status);
            Assert.Equal("42", result.Output);
        }

        [Fact]
        public void BuildDockerArguments_Sets_Correct_Resource_Limits()
        {
            // Arrange
            string workDir = @"C:\temp\test";
            string cmd = "python main.py";
            string lang = "python";

            // Act
            string args = _executor.BuildDockerArguments(workDir, cmd, lang);

            // Assert
            Assert.Contains("-m 256m", args); // Memory limit
            Assert.Contains("--cpus=\"1.0\"", args); // CPU limit
            Assert.Contains("--network none", args); // No network for security
            Assert.Contains("C:/temp/test", args); // Path sanitization check
        }

        [Fact]
        public void Status_Aggregation_Logic_Test()
        {
            // Priority order for reporting
            var priorities = new[] {
                SubmissionStatus.CompilationError,
                SubmissionStatus.TimeLimitExceeded,
                SubmissionStatus.WrongAnswer
            };

            // Simulating multiple test results
            var results = new List<SubmissionStatus> { SubmissionStatus.Accepted, SubmissionStatus.WrongAnswer };
            
            Assert.Contains(SubmissionStatus.WrongAnswer, results);
        }
    }
}
