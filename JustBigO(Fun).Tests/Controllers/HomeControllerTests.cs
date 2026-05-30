using JustBigO_Fun_.Controllers;
using JustBigO_Fun_.Data;
using JustBigO_Fun_.Models;
using JustBigO_Fun_.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace JustBigO_Fun_.Tests.Controllers
{
    public class HomeControllerTests
    {
        private readonly DbContextOptions<ApplicationDbContext> _options;

        public HomeControllerTests()
        {
            _options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: "HomeTestDb_" + System.Guid.NewGuid())
                .Options;
        }

        [Fact]
        public async Task Index_ReturnsViewWithProblems()
        {
            // Arrange
            using var db = new ApplicationDbContext(_options);
            db.Problems.Add(new Problem { Id = 1, Title = "P1", Slug = "p1", Difficulty = "Easy", Tags = "" });
            db.Problems.Add(new Problem { Id = 2, Title = "P2", Slug = "p2", Difficulty = "Hard", Tags = "" });
            db.SaveChanges();

            var mockLogger = new Mock<ILogger<HomeController>>();
            var controller = new HomeController(mockLogger.Object, db, new MarkdownRenderer(new HtmlSanitizerService()));
            
            // Mocking User identity
            var user = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity());
            controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext()
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() { User = user }
            };

            // Act
            var result = await controller.Index(null, null);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<ProblemListItem>>(viewResult.ViewData.Model);
            Assert.Equal(2, model.Count());
        }

        [Fact]
        public async Task Index_FiltersByDifficulty()
        {
            // Arrange
            using var db = new ApplicationDbContext(_options);
            db.Problems.Add(new Problem { Id = 3, Title = "EasyP", Slug = "e", Difficulty = "Easy", Tags = "" });
            db.Problems.Add(new Problem { Id = 4, Title = "HardP", Slug = "h", Difficulty = "Hard", Tags = "" });
            db.SaveChanges();

            var mockLogger = new Mock<ILogger<HomeController>>();
            var controller = new HomeController(mockLogger.Object, db, new MarkdownRenderer(new HtmlSanitizerService()));

            // Mocking User identity
            var user = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity());
            controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext()
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() { User = user }
            };

            // Act
            var result = await controller.Index(null, "Easy");

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<ProblemListItem>>(viewResult.ViewData.Model);
            Assert.Single(model);
            Assert.Equal("Easy", model.First().Difficulty);
        }

        [Fact]
        public async Task Solve_ReturnsNotFound_WhenProblemDoesNotExist()
        {
            // Arrange
            using var db = new ApplicationDbContext(_options);
            var mockLogger = new Mock<ILogger<HomeController>>();
            var controller = new HomeController(mockLogger.Object, db, new MarkdownRenderer(new HtmlSanitizerService()));

            // Act
            var result = await controller.Solve(999);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }
    }
}
