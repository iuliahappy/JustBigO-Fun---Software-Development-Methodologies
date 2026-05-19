using JustBigO_Fun_.Models;
using Xunit;
using System.Linq;

namespace JustBigO_Fun_.Tests.Models
{
    public class ProblemTests
    {
        [Fact]
        public void GetTags_Parses_CommaSeparated_Correctly()
        {
            // Arrange
            var problem = new Problem { Tags = "Array, String" };

            // Act
            var tags = problem.GetTags();

            // Assert
            Assert.Equal(2, tags.Length);
            Assert.Contains("Array", tags);
            Assert.Contains("String", tags);
        }

        [Fact]
        public void GetTags_ReturnsEmpty_WhenTagsIsNull()
        {
            // Arrange
            var problem = new Problem { Tags = null };

            // Act
            var tags = problem.GetTags();

            // Assert
            Assert.Empty(tags);
        }

        [Theory]
        [InlineData("Easy", "easy")]
        [InlineData("Medium", "medium")]
        [InlineData("Hard", "hard")]
        [InlineData("Unknown", "easy")]
        public void GetDifficultyClass_Returns_Correct_Suffix(string difficulty, string expectedClass)
        {
            // Arrange
            var problem = new Problem { Difficulty = difficulty };

            // Act
            var result = problem.GetDifficultyClass();

            // Assert
            Assert.Equal(expectedClass, result);
        }
    }
}
