using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MealPlanOrganizer_Functions.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceFrequencyWithNextTimePreference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "FrequencyPreference",
                table: "RecipeRatings",
                newName: "NextTimePreference");

            migrationBuilder.UpdateData(
                table: "Recipes",
                keyColumn: "Id",
                keyValue: new Guid("b3f9a1b6-7c1b-4b33-b17b-0d2f9b2a5e01"),
                column: "CreatedUtc",
                value: new DateTime(2026, 2, 26, 14, 36, 24, 405, DateTimeKind.Utc).AddTicks(3505));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "NextTimePreference",
                table: "RecipeRatings",
                newName: "FrequencyPreference");

            migrationBuilder.UpdateData(
                table: "Recipes",
                keyColumn: "Id",
                keyValue: new Guid("b3f9a1b6-7c1b-4b33-b17b-0d2f9b2a5e01"),
                column: "CreatedUtc",
                value: new DateTime(2026, 2, 14, 4, 28, 2, 972, DateTimeKind.Utc).AddTicks(667));
        }
    }
}
