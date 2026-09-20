using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Skill.Suite.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionDatabaseSeedScript : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DatabaseSeedScript",
                table: "sessions",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DatabaseSeedScript",
                table: "sessions");
        }
    }
}
