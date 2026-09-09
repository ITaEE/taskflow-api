using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Portfolio.TaskFlowApi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthenticationAndOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 254, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "TEXT", maxLength: 254, nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerUserId",
                table: "Projects",
                type: "TEXT",
                nullable: true);

            migrationBuilder.Sql(
                """
                INSERT INTO Users (Id, Email, NormalizedEmail, PasswordHash, CreatedAtUtc)
                SELECT
                    '11111111-1111-4111-8111-111111111111',
                    'legacy-stage1@local.invalid',
                    'LEGACY-STAGE1@LOCAL.INVALID',
                    '!LEGACY_ACCOUNT_DISABLED!',
                    '2026-09-09 00:00:00'
                WHERE EXISTS (SELECT 1 FROM Projects);

                UPDATE Projects
                SET OwnerUserId = '11111111-1111-4111-8111-111111111111'
                WHERE OwnerUserId IS NULL;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "OwnerUserId",
                table: "Projects",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_OwnerUserId",
                table: "Projects",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_NormalizedEmail",
                table: "Users",
                column: "NormalizedEmail",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_Users_OwnerUserId",
                table: "Projects",
                column: "OwnerUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Projects_Users_OwnerUserId",
                table: "Projects");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Projects_OwnerUserId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "Projects");
        }
    }
}
