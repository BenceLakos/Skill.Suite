using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Skill.Suite.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddCompetitorFullNameAndSessionCredentials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "GitCredentialId",
                table: "sessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "JudgementImagePullCredentialId",
                table: "sessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FullName",
                table: "competitors",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            // Backfill existing rows with the username so the field carries a sensible
            // value until an operator edits each competitor.
            migrationBuilder.Sql(
                "UPDATE competitors SET \"FullName\" = \"Username\" WHERE \"FullName\" = '';");

            migrationBuilder.CreateIndex(
                name: "IX_sessions_GitCredentialId",
                table: "sessions",
                column: "GitCredentialId");

            migrationBuilder.CreateIndex(
                name: "IX_sessions_JudgementImagePullCredentialId",
                table: "sessions",
                column: "JudgementImagePullCredentialId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_sessions_GitCredentialId",
                table: "sessions");

            migrationBuilder.DropIndex(
                name: "IX_sessions_JudgementImagePullCredentialId",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "GitCredentialId",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "JudgementImagePullCredentialId",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "FullName",
                table: "competitors");
        }
    }
}
