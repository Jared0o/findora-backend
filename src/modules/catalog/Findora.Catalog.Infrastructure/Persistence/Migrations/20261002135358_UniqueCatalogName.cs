using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Findora.Catalog.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UniqueCatalogName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "normalized_name",
                schema: "catalog",
                table: "catalogs",
                type: "text",
                nullable: false,
                computedColumnSql: "lower(btrim(name))",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "ux_catalogs_normalized_name",
                schema: "catalog",
                table: "catalogs",
                column: "normalized_name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_catalogs_normalized_name",
                schema: "catalog",
                table: "catalogs");

            migrationBuilder.DropColumn(
                name: "normalized_name",
                schema: "catalog",
                table: "catalogs");
        }
    }
}
