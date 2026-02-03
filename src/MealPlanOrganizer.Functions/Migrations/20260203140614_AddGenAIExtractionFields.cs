using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MealPlanOrganizer_Functions.Migrations
{
    /// <inheritdoc />
    public partial class AddGenAIExtractionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ImageUrl",
                table: "Recipes",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ExtractionConfidence",
                table: "Recipes",
                type: "decimal(3,2)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsExtracted",
                table: "Recipes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SourceImageUrl",
                table: "Recipes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "QuantityValue",
                table: "RecipeIngredients",
                type: "decimal(10,4)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Unit",
                table: "RecipeIngredients",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "RecipeIngredients",
                keyColumn: "Id",
                keyValue: new Guid("3d3d5d7f-9a0c-4b3e-9f5a-2a9f7b6c1a11"),
                columns: new[] { "QuantityValue", "Unit" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "RecipeIngredients",
                keyColumn: "Id",
                keyValue: new Guid("7a2b6e9d-1c4f-4b82-8b8a-9b3c7a1d2f22"),
                columns: new[] { "QuantityValue", "Unit" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Recipes",
                keyColumn: "Id",
                keyValue: new Guid("b3f9a1b6-7c1b-4b33-b17b-0d2f9b2a5e01"),
                columns: new[] { "CreatedUtc", "ExtractionConfidence", "SourceImageUrl" },
                values: new object[] { new DateTime(2026, 2, 3, 14, 6, 14, 440, DateTimeKind.Utc).AddTicks(4189), null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExtractionConfidence",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "IsExtracted",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "SourceImageUrl",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "QuantityValue",
                table: "RecipeIngredients");

            migrationBuilder.DropColumn(
                name: "Unit",
                table: "RecipeIngredients");

            migrationBuilder.AlterColumn<string>(
                name: "ImageUrl",
                table: "Recipes",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.UpdateData(
                table: "Recipes",
                keyColumn: "Id",
                keyValue: new Guid("b3f9a1b6-7c1b-4b33-b17b-0d2f9b2a5e01"),
                column: "CreatedUtc",
                value: new DateTime(2026, 1, 28, 16, 6, 43, 754, DateTimeKind.Utc).AddTicks(2536));
        }
    }
}
