using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MvcMusic.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveIsThumbnail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsThumbnail",
                table: "ProductImage");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsThumbnail",
                table: "ProductImage",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }
    }
}
