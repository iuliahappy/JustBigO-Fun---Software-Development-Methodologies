using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JustBigO_Fun_.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProblemMethodName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MethodName",
                table: "Problems",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MethodName",
                table: "Problems");
        }
    }
}
