using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Skill.Suite.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddJudgingProtocolFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Aspect",
                table: "test_unit_results",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AspectCompetitorVisible",
                table: "test_unit_results",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Kind",
                table: "test_fixture_results",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Existing rows all default to Tests, but any fixture already carrying a Quality is a scoring
            // part, not a test class - it was created by a score event. Without this, kind-scoped lookups
            // would treat those historical parts as test fixtures.
            migrationBuilder.Sql(
                """UPDATE test_fixture_results SET "Kind" = 1 WHERE "Quality" IS NOT NULL;""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Aspect",
                table: "test_unit_results");

            migrationBuilder.DropColumn(
                name: "AspectCompetitorVisible",
                table: "test_unit_results");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "test_fixture_results");
        }
    }
}
