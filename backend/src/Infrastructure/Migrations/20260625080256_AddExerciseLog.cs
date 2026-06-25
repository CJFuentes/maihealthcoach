using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MAIHealthCoach.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExerciseLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExerciseLogEntries",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExerciseActivityId = table.Column<Guid>(type: "uuid", nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    CaloriesBurned = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExerciseLogEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExerciseLogEntries_ExerciseActivities_ExerciseActivityId",
                        column: x => x.ExerciseActivityId,
                        principalSchema: "public",
                        principalTable: "ExerciseActivities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExerciseLogEntries_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "public",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExerciseLogEntries_ExerciseActivityId",
                schema: "public",
                table: "ExerciseLogEntries",
                column: "ExerciseActivityId");

            migrationBuilder.CreateIndex(
                name: "IX_ExerciseLogEntries_UserId",
                schema: "public",
                table: "ExerciseLogEntries",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ExerciseLogEntries_UserId_Date",
                schema: "public",
                table: "ExerciseLogEntries",
                columns: new[] { "UserId", "Date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExerciseLogEntries",
                schema: "public");
        }
    }
}
