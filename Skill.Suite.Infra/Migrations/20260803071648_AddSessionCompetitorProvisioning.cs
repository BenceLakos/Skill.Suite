using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Skill.Suite.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionCompetitorProvisioning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "WebhookSecret",
                table: "sessions",
                type: "bytea",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "session_competitors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitorId = table.Column<Guid>(type: "uuid", nullable: false),
                    RepositoryName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RepositoryUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ProvisionStatus = table.Column<int>(type: "integer", nullable: false),
                    ProvisionError = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ProvisionedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_competitors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_session_competitors_sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_session_competitors_CompetitorId",
                table: "session_competitors",
                column: "CompetitorId");

            migrationBuilder.CreateIndex(
                name: "IX_session_competitors_ProvisionStatus",
                table: "session_competitors",
                column: "ProvisionStatus");

            migrationBuilder.CreateIndex(
                name: "IX_session_competitors_SessionId_CompetitorId",
                table: "session_competitors",
                columns: new[] { "SessionId", "CompetitorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_session_competitors_SessionId_RepositoryName",
                table: "session_competitors",
                columns: new[] { "SessionId", "RepositoryName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "session_competitors");

            migrationBuilder.DropColumn(
                name: "WebhookSecret",
                table: "sessions");
        }
    }
}
