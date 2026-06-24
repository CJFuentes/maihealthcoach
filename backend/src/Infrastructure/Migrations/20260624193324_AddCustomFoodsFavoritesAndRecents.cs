using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MAIHealthCoach.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomFoodsFavoritesAndRecents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                schema: "public",
                table: "FoodItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UserFavoriteFoods",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FoodItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserFavoriteFoods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserFavoriteFoods_FoodItems_FoodItemId",
                        column: x => x.FoodItemId,
                        principalSchema: "public",
                        principalTable: "FoodItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserFavoriteFoods_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "public",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FoodItems_CreatedByUserId",
                schema: "public",
                table: "FoodItems",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserFavoriteFoods_FoodItemId",
                schema: "public",
                table: "UserFavoriteFoods",
                column: "FoodItemId");

            migrationBuilder.CreateIndex(
                name: "IX_UserFavoriteFoods_UserId_FoodItemId",
                schema: "public",
                table: "UserFavoriteFoods",
                columns: new[] { "UserId", "FoodItemId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_FoodItems_Users_CreatedByUserId",
                schema: "public",
                table: "FoodItems",
                column: "CreatedByUserId",
                principalSchema: "public",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FoodItems_Users_CreatedByUserId",
                schema: "public",
                table: "FoodItems");

            migrationBuilder.DropTable(
                name: "UserFavoriteFoods",
                schema: "public");

            migrationBuilder.DropIndex(
                name: "IX_FoodItems_CreatedByUserId",
                schema: "public",
                table: "FoodItems");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                schema: "public",
                table: "FoodItems");
        }
    }
}
