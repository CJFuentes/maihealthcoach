using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MAIHealthCoach.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserGoalTargets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserGoalTargets",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CaloriesKcal = table.Column<int>(type: "integer", nullable: true),
                    ProteinGrams = table.Column<int>(type: "integer", nullable: true),
                    CarbohydrateGrams = table.Column<int>(type: "integer", nullable: true),
                    FatGrams = table.Column<int>(type: "integer", nullable: true),
                    WaterMl = table.Column<int>(type: "integer", nullable: true),
                    LastOverriddenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserGoalTargets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserGoalTargets_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "public",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserGoalTargets_UserId",
                schema: "public",
                table: "UserGoalTargets",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserGoalTargets",
                schema: "public");
        }
    }
}
