using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Skill.Suite.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddCompetitorMobileIpAddress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MobileIpAddress",
                table: "competitors",
                type: "character varying(45)",
                maxLength: 45,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MobileIpAddress",
                table: "competitors");
        }
    }
}
