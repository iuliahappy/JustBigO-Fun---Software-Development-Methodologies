using JustBigO_Fun_.Controllers;
using JustBigO_Fun_.Data;
using JustBigO_Fun_.Models;
using JustBigO_Fun_.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.Security.Claims;
using System.Threading.Tasks;
using Xunit;

namespace JustBigO_Fun_.Tests.Controllers
{
    public class SubmissionControllerTests
    {
        private readonly DbContextOptions<ApplicationDbContext> _options;

        public SubmissionControllerTests()
        {
            _options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: "SubTestDb_" + System.Guid.NewGuid())
                .Options;
        }

        [Fact]
        public async Task Submit_ReturnsOk_WithSubmissionId()
        {
            // Arrange
            using var db = new ApplicationDbContext(_options);
            db.Problems.Add(new Problem { Id = 1, Title = "Test", Slug = "test" });
            
            // Add mock user to prevent 401 from userExists check
            var userId = "user-123";
            db.Users.Add(new Microsoft.AspNetCore.Identity.IdentityUser { Id = userId, UserName = "testuser", Email = "test@test.com" });
            
            db.SaveChanges();

            var mockExecutor = new Mock<ICodeExecutor>();
            var controller = new SubmissionController(db, mockExecutor.Object);

            // Mocking User Identity
            var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[] {
                new Claim(ClaimTypes.NameIdentifier, userId)
            }, "mock"));

            controller.ControllerContext = new ControllerContext()
            {
                HttpContext = new DefaultHttpContext() { User = user }
            };

            var request = new SubmissionRequest { ProblemId = 1, SourceCode = "print(1)", Language = "python" };

            // Act
            var result = await controller.Submit(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);
        }

        [Fact]
        public async Task GetStatus_ReturnsNotFound_WhenSubmissionDoesNotExist()
        {
            // Arrange
            using var db = new ApplicationDbContext(_options);
            var mockExecutor = new Mock<ICodeExecutor>();
            var controller = new SubmissionController(db, mockExecutor.Object);

            // Act
            var result = await controller.GetStatus(999);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }
    }
}
