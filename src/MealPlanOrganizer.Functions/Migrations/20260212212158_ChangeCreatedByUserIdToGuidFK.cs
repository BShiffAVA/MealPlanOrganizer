using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MealPlanOrganizer_Functions.Migrations
{
    /// <inheritdoc />
    public partial class ChangeCreatedByUserIdToGuidFK : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Recipes_Users_UserId",
                table: "Recipes");

            migrationBuilder.DropIndex(
                name: "IX_Recipes_UserId",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Recipes");

            // Clear existing string values before converting column type to GUID
            migrationBuilder.Sql("UPDATE Recipes SET CreatedByUserId = NULL");

            migrationBuilder.AlterColumn<Guid>(
                name: "CreatedByUserId",
                table: "Recipes",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.UpdateData(
                table: "Recipes",
                keyColumn: "Id",
                keyValue: new Guid("b3f9a1b6-7c1b-4b33-b17b-0d2f9b2a5e01"),
                columns: new[] { "CreatedByUserId", "CreatedUtc" },
                values: new object[] { null, new DateTime(2026, 2, 12, 21, 21, 58, 275, DateTimeKind.Utc).AddTicks(6692) });

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_CreatedByUserId",
                table: "Recipes",
                column: "CreatedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Recipes_Users_CreatedByUserId",
                table: "Recipes",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Recipes_Users_CreatedByUserId",
                table: "Recipes");

            migrationBuilder.DropIndex(
                name: "IX_Recipes_CreatedByUserId",
                table: "Recipes");

            migrationBuilder.AlterColumn<string>(
                name: "CreatedByUserId",
                table: "Recipes",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "Recipes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Recipes",
                keyColumn: "Id",
                keyValue: new Guid("b3f9a1b6-7c1b-4b33-b17b-0d2f9b2a5e01"),
                columns: new[] { "CreatedByUserId", "CreatedUtc", "UserId" },
                values: new object[] { null, new DateTime(2026, 2, 12, 13, 48, 22, 527, DateTimeKind.Utc).AddTicks(538), null });

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_UserId",
                table: "Recipes",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Recipes_Users_UserId",
                table: "Recipes",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id");
        }
    }
}
