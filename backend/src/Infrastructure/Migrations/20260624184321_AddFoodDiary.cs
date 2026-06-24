using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MAIHealthCoach.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFoodDiary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DiaryEntries",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FoodItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServingSizeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(9,3)", precision: 9, scale: 3, nullable: false),
                    MealType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiaryEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiaryEntries_FoodItems_FoodItemId",
                        column: x => x.FoodItemId,
                        principalSchema: "public",
                        principalTable: "FoodItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DiaryEntries_ServingSizes_ServingSizeId",
                        column: x => x.ServingSizeId,
                        principalSchema: "public",
                        principalTable: "ServingSizes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DiaryEntries_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "public",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DiaryEntries_FoodItemId",
                schema: "public",
                table: "DiaryEntries",
                column: "FoodItemId");

            migrationBuilder.CreateIndex(
                name: "IX_DiaryEntries_ServingSizeId",
                schema: "public",
                table: "DiaryEntries",
                column: "ServingSizeId");

            migrationBuilder.CreateIndex(
                name: "IX_DiaryEntries_UserId",
                schema: "public",
                table: "DiaryEntries",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_DiaryEntries_UserId_Date",
                schema: "public",
                table: "DiaryEntries",
                columns: new[] { "UserId", "Date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DiaryEntries",
                schema: "public");
        }
    }
}
