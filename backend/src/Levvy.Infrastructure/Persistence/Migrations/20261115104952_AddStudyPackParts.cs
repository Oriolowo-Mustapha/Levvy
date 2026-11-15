using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Levvy.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStudyPackParts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StudyPackPart",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudyPackId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    StartOffsetMs = table.Column<long>(type: "bigint", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudyPackPart", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudyPackPart_StudyPacks_StudyPackId",
                        column: x => x.StudyPackId,
                        principalTable: "StudyPacks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StudyPackPart_StudyPackId_Ordinal",
                table: "StudyPackPart",
                columns: new[] { "StudyPackId", "Ordinal" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StudyPackPart");
        }
    }
}
