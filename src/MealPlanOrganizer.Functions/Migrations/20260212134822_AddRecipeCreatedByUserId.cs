using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MealPlanOrganizer_Functions.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipeCreatedByUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreatedByUserId",
                table: "Recipes",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Recipes",
                keyColumn: "Id",
                keyValue: new Guid("b3f9a1b6-7c1b-4b33-b17b-0d2f9b2a5e01"),
                columns: new[] { "CreatedByUserId", "CreatedUtc" },
                values: new object[] { null, new DateTime(2026, 2, 12, 13, 48, 22, 527, DateTimeKind.Utc).AddTicks(538) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "Recipes");

            migrationBuilder.UpdateData(
                table: "Recipes",
                keyColumn: "Id",
                keyValue: new Guid("b3f9a1b6-7c1b-4b33-b17b-0d2f9b2a5e01"),
                column: "CreatedUtc",
                value: new DateTime(2026, 2, 11, 19, 24, 38, 72, DateTimeKind.Utc).AddTicks(7531));
        }
    }
}
