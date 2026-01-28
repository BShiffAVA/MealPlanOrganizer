using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MealPlanOrganizer_Functions.Migrations
{
    /// <inheritdoc />
    public partial class AddUS005Fields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CookTimeMinutes",
                table: "Recipes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Recipes",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CuisineType",
                table: "Recipes",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "Recipes",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PrepTimeMinutes",
                table: "Recipes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Servings",
                table: "Recipes",
                type: "int",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Recipes",
                keyColumn: "Id",
                keyValue: new Guid("b3f9a1b6-7c1b-4b33-b17b-0d2f9b2a5e01"),
                columns: new[] { "CookTimeMinutes", "CreatedBy", "CreatedUtc", "CuisineType", "ImageUrl", "PrepTimeMinutes", "Servings" },
                values: new object[] { null, null, new DateTime(2026, 1, 28, 14, 32, 47, 161, DateTimeKind.Utc).AddTicks(2574), null, null, null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CookTimeMinutes",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "CuisineType",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "PrepTimeMinutes",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "Servings",
                table: "Recipes");

            migrationBuilder.UpdateData(
                table: "Recipes",
                keyColumn: "Id",
                keyValue: new Guid("b3f9a1b6-7c1b-4b33-b17b-0d2f9b2a5e01"),
                column: "CreatedUtc",
                value: new DateTime(2026, 1, 28, 13, 32, 37, 650, DateTimeKind.Utc).AddTicks(8329));
        }
    }
}
