using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MvcMusic.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddActivityLogSeenStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActivityLogSeenStatus",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ActivityLogId = table.Column<int>(type: "int", nullable: false),
                    AdminUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SeenAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityLogSeenStatus", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActivityLogSeenStatus_ActivityLog_ActivityLogId",
                        column: x => x.ActivityLogId,
                        principalTable: "ActivityLog",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ActivityLogSeenStatus_AspNetUsers_AdminUserId",
                        column: x => x.AdminUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogSeenStatus_ActivityLogId",
                table: "ActivityLogSeenStatus",
                column: "ActivityLogId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogSeenStatus_AdminUserId",
                table: "ActivityLogSeenStatus",
                column: "AdminUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityLogSeenStatus");
        }
    }
}
