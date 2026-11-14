using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Levvy.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOnlineClassFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BotStatus",
                table: "Sessions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalBotId",
                table: "Sessions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalMeetingUrl",
                table: "Sessions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MeetingPlatform",
                table: "Sessions",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BotStatus",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "ExternalBotId",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "ExternalMeetingUrl",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "MeetingPlatform",
                table: "Sessions");
        }
    }
}
