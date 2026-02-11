using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MealPlanOrganizer_Functions.Migrations
{
    /// <inheritdoc />
    public partial class AddInviteCodeEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InviteCodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    ExpiresUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsRevoked = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    UsedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UsedUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InviteCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InviteCodes_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InviteCodes_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InviteCodes_Users_UsedByUserId",
                        column: x => x.UsedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "Recipes",
                keyColumn: "Id",
                keyValue: new Guid("b3f9a1b6-7c1b-4b33-b17b-0d2f9b2a5e01"),
                column: "CreatedUtc",
                value: new DateTime(2026, 2, 11, 17, 33, 8, 656, DateTimeKind.Utc).AddTicks(9559));

            migrationBuilder.CreateIndex(
                name: "IX_InviteCodes_Code",
                table: "InviteCodes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InviteCodes_CreatedByUserId",
                table: "InviteCodes",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InviteCodes_HouseholdId",
                table: "InviteCodes",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_InviteCodes_UsedByUserId",
                table: "InviteCodes",
                column: "UsedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InviteCodes");

            migrationBuilder.UpdateData(
                table: "Recipes",
                keyColumn: "Id",
                keyValue: new Guid("b3f9a1b6-7c1b-4b33-b17b-0d2f9b2a5e01"),
                column: "CreatedUtc",
                value: new DateTime(2026, 2, 10, 21, 2, 56, 221, DateTimeKind.Utc).AddTicks(3341));
        }
    }
}
