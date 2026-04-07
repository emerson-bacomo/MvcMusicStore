using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MvcMusic.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameIsPrimaryToIsThumbnail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IsPrimary",
                table: "ProductImage",
                newName: "IsThumbnail");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IsThumbnail",
                table: "ProductImage",
                newName: "IsPrimary");
        }
    }
}
