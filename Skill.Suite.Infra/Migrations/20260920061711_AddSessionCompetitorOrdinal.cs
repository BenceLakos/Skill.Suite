using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Skill.Suite.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionCompetitorOrdinal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NextCompetitorOrdinal",
                table: "sessions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Ordinal",
                table: "session_competitors",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Every existing enrolment would otherwise be ordinal 0, which is not a harmless default here:
            // the ordinal is added to a service's host port to keep one competitor's container off another's,
            // so a whole session sharing it would put every per-competitor container on one port and leave
            // all but the first failing to start. Numbered from zero per session, in repository-name order,
            // which is stable and is the order the competitors are listed in anyway.
            migrationBuilder.Sql(
                """
                UPDATE session_competitors AS target
                SET "Ordinal" = numbered.ordinal
                FROM (
                    SELECT
                        "Id",
                        CAST(ROW_NUMBER() OVER (
                            PARTITION BY "SessionId" ORDER BY "RepositoryName") - 1 AS integer) AS ordinal
                    FROM session_competitors
                ) AS numbered
                WHERE target."Id" = numbered."Id";
                """);

            // And the sequence carries on from there, so a competitor enrolled after this migration cannot
            // be handed an ordinal — and therefore host ports — that somebody already has.
            migrationBuilder.Sql(
                """
                UPDATE sessions
                SET "NextCompetitorOrdinal" = (
                    SELECT COUNT(*) FROM session_competitors WHERE "SessionId" = sessions."Id");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NextCompetitorOrdinal",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "Ordinal",
                table: "session_competitors");
        }
    }
}
