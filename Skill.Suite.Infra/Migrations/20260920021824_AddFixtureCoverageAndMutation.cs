using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Skill.Suite.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddFixtureCoverageAndMutation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "LineCoverage",
                table: "test_fixture_results",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "MutationScore",
                table: "test_fixture_results",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LineCoverage",
                table: "test_fixture_results");

            migrationBuilder.DropColumn(
                name: "MutationScore",
                table: "test_fixture_results");
        }
    }
}
