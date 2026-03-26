using JustBigO_Fun_.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace JustBigO_Fun_.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Problem> Problems => Set<Problem>();
        public DbSet<ProblemTest> ProblemTests => Set<ProblemTest>();
        public DbSet<Submission> Submissions => Set<Submission>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Submission>(e =>
            {
                e.Property(s => s.SourceCode).HasColumnType("nvarchar(max)");
                e.Property(s => s.ResultsJson).HasColumnType("nvarchar(max)");
                e.HasOne(s => s.Problem)
                    .WithMany()
                    .HasForeignKey(s => s.ProblemId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<Problem>(e =>
            {
                e.HasIndex(p => p.Slug).IsUnique();
                e.Property(p => p.Description).HasColumnType("nvarchar(max)");
                e.Property(p => p.CodeTemplatesJson).HasColumnType("nvarchar(max)");
            });

            builder.Entity<ProblemTest>(e =>
            {
                e.Property(t => t.InputJson).HasColumnType("nvarchar(max)");
                e.Property(t => t.ExpectedOutputJson).HasColumnType("nvarchar(max)");
                e.HasOne(t => t.Problem)
                    .WithMany(p => p.Tests)
                    .HasForeignKey(t => t.ProblemId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
