using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MAIHealthCoach.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPushReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeviceRegistrations",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Token = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Platform = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceRegistrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeviceRegistrations_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "public",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReminderPreferences",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MealRemindersEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    WaterRemindersEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    MealReminderTimesJson = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    WaterReminderTime = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    QuietHoursStart = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    QuietHoursEnd = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    UtcOffsetMinutes = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReminderPreferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReminderPreferences_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "public",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceRegistrations_Token",
                schema: "public",
                table: "DeviceRegistrations",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeviceRegistrations_UserId",
                schema: "public",
                table: "DeviceRegistrations",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReminderPreferences_UserId",
                schema: "public",
                table: "ReminderPreferences",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeviceRegistrations",
                schema: "public");

            migrationBuilder.DropTable(
                name: "ReminderPreferences",
                schema: "public");
        }
    }
}
