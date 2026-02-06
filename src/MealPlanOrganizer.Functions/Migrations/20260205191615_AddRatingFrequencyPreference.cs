using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MealPlanOrganizer_Functions.Migrations
{
    /// <inheritdoc />
    public partial class AddRatingFrequencyPreference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RecipeRatings_RecipeId_UserId",
                table: "RecipeRatings");

            migrationBuilder.AddColumn<string>(
                name: "FrequencyPreference",
                table: "RecipeRatings",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Recipes",
                keyColumn: "Id",
                keyValue: new Guid("b3f9a1b6-7c1b-4b33-b17b-0d2f9b2a5e01"),
                column: "CreatedUtc",
                value: new DateTime(2026, 2, 5, 19, 16, 5, 628, DateTimeKind.Utc).AddTicks(7755));

            migrationBuilder.CreateIndex(
                name: "IX_RecipeRatings_RecipeId_UserId",
                table: "RecipeRatings",
                columns: new[] { "RecipeId", "UserId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RecipeRatings_RecipeId_UserId",
                table: "RecipeRatings");

            migrationBuilder.DropColumn(
                name: "FrequencyPreference",
                table: "RecipeRatings");

            migrationBuilder.UpdateData(
                table: "Recipes",
                keyColumn: "Id",
                keyValue: new Guid("b3f9a1b6-7c1b-4b33-b17b-0d2f9b2a5e01"),
                column: "CreatedUtc",
                value: new DateTime(2026, 2, 3, 14, 6, 14, 440, DateTimeKind.Utc).AddTicks(4189));

            migrationBuilder.CreateIndex(
                name: "IX_RecipeRatings_RecipeId_UserId",
                table: "RecipeRatings",
                columns: new[] { "RecipeId", "UserId" },
                unique: true);
        }
    }
}
