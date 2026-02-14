using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MealPlanOrganizer_Functions.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingRatings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PendingRatings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecipeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MealPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MealPlanRecipeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServedDate = table.Column<DateTime>(type: "date", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Pending"),
                    CompletedUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingRatings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PendingRatings_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PendingRatings_MealPlanRecipes_MealPlanRecipeId",
                        column: x => x.MealPlanRecipeId,
                        principalTable: "MealPlanRecipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PendingRatings_MealPlans_MealPlanId",
                        column: x => x.MealPlanId,
                        principalTable: "MealPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PendingRatings_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PendingRatings_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "Recipes",
                keyColumn: "Id",
                keyValue: new Guid("b3f9a1b6-7c1b-4b33-b17b-0d2f9b2a5e01"),
                column: "CreatedUtc",
                value: new DateTime(2026, 2, 14, 4, 10, 47, 982, DateTimeKind.Utc).AddTicks(2521));

            migrationBuilder.CreateIndex(
                name: "IX_PendingRatings_HouseholdId_Status",
                table: "PendingRatings",
                columns: new[] { "HouseholdId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PendingRatings_MealPlanId",
                table: "PendingRatings",
                column: "MealPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_PendingRatings_MealPlanRecipeId",
                table: "PendingRatings",
                column: "MealPlanRecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_PendingRatings_RecipeId",
                table: "PendingRatings",
                column: "RecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_PendingRatings_UserId_MealPlanRecipeId",
                table: "PendingRatings",
                columns: new[] { "UserId", "MealPlanRecipeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PendingRatings_UserId_Status",
                table: "PendingRatings",
                columns: new[] { "UserId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PendingRatings");

            migrationBuilder.UpdateData(
                table: "Recipes",
                keyColumn: "Id",
                keyValue: new Guid("b3f9a1b6-7c1b-4b33-b17b-0d2f9b2a5e01"),
                column: "CreatedUtc",
                value: new DateTime(2026, 2, 13, 18, 41, 49, 862, DateTimeKind.Utc).AddTicks(1163));
        }
    }
}
