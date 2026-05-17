using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JustBigO_Fun_.Migrations
{
    /// <inheritdoc />
    public partial class AddSignatureAndMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "PeakMemoryKb",
                table: "Submissions",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "UserTimeMs",
                table: "Submissions",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignatureJson",
                table: "Problems",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PeakMemoryKb",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "UserTimeMs",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "SignatureJson",
                table: "Problems");
        }
    }
}
