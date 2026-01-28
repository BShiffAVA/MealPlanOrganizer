using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MealPlanOrganizer_Functions.Migrations
{
    /// <inheritdoc />
    public partial class SeedSampleData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Recipes",
                columns: new[] { "Id", "CreatedUtc", "Description", "Title" },
                values: new object[] { new Guid("b3f9a1b6-7c1b-4b33-b17b-0d2f9b2a5e01"), new DateTime(2026, 1, 28, 13, 32, 37, 650, DateTimeKind.Utc).AddTicks(8329), "Classic lasagna with noodles and sauce", "Sample Lasagna" });

            migrationBuilder.InsertData(
                table: "RecipeIngredients",
                columns: new[] { "Id", "Name", "Quantity", "RecipeId" },
                values: new object[,]
                {
                    { new Guid("3d3d5d7f-9a0c-4b3e-9f5a-2a9f7b6c1a11"), "noodles", "12 pieces", new Guid("b3f9a1b6-7c1b-4b33-b17b-0d2f9b2a5e01") },
                    { new Guid("7a2b6e9d-1c4f-4b82-8b8a-9b3c7a1d2f22"), "sauce", "2 cups", new Guid("b3f9a1b6-7c1b-4b33-b17b-0d2f9b2a5e01") }
                });

            migrationBuilder.InsertData(
                table: "RecipeSteps",
                columns: new[] { "Id", "Instruction", "RecipeId", "StepNumber" },
                values: new object[,]
                {
                    { new Guid("c9a1d2e3-4f5b-6c7d-8e9f-0a1b2c3d4e55"), "Boil noodles", new Guid("b3f9a1b6-7c1b-4b33-b17b-0d2f9b2a5e01"), 1 },
                    { new Guid("f1e2d3c4-b5a6-7890-1a2b-3c4d5e6f7a88"), "Bake with sauce", new Guid("b3f9a1b6-7c1b-4b33-b17b-0d2f9b2a5e01"), 2 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "RecipeIngredients",
                keyColumn: "Id",
                keyValue: new Guid("3d3d5d7f-9a0c-4b3e-9f5a-2a9f7b6c1a11"));

            migrationBuilder.DeleteData(
                table: "RecipeIngredients",
                keyColumn: "Id",
                keyValue: new Guid("7a2b6e9d-1c4f-4b82-8b8a-9b3c7a1d2f22"));

            migrationBuilder.DeleteData(
                table: "RecipeSteps",
                keyColumn: "Id",
                keyValue: new Guid("c9a1d2e3-4f5b-6c7d-8e9f-0a1b2c3d4e55"));

            migrationBuilder.DeleteData(
                table: "RecipeSteps",
                keyColumn: "Id",
                keyValue: new Guid("f1e2d3c4-b5a6-7890-1a2b-3c4d5e6f7a88"));

            migrationBuilder.DeleteData(
                table: "Recipes",
                keyColumn: "Id",
                keyValue: new Guid("b3f9a1b6-7c1b-4b33-b17b-0d2f9b2a5e01"));
        }
    }
}
