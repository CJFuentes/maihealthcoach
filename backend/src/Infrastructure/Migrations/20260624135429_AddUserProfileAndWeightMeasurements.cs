using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MAIHealthCoach.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserProfileAndWeightMeasurements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserProfiles",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    HeightCm = table.Column<double>(type: "double precision", nullable: true),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: true),
                    BiologicalSex = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ActivityLevel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    PrimaryGoal = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Units = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "Metric"),
                    DietaryPreferences_DietType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    DietaryPreferences_Allergies = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true, defaultValue: ""),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserProfiles_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "public",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WeightMeasurements",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    WeightKg = table.Column<double>(type: "double precision", nullable: false),
                    MeasuredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeightMeasurements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WeightMeasurements_UserProfiles_UserProfileId",
                        column: x => x.UserProfileId,
                        principalSchema: "public",
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_UserId",
                schema: "public",
                table: "UserProfiles",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeightMeasurements_UserProfileId_MeasuredAt",
                schema: "public",
                table: "WeightMeasurements",
                columns: new[] { "UserProfileId", "MeasuredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WeightMeasurements",
                schema: "public");

            migrationBuilder.DropTable(
                name: "UserProfiles",
                schema: "public");
        }
    }
}
