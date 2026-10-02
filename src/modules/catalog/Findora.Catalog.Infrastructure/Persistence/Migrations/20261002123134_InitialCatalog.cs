using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Findora.Catalog.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "catalog");

            migrationBuilder.CreateTable(
                name: "catalogs",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_catalogs", x => x.id);
                    table.CheckConstraint("ck_catalogs_name_not_empty", "length(btrim(name)) > 0");
                });

            migrationBuilder.CreateTable(
                name: "field_definitions",
                schema: "catalog",
                columns: table => new
                {
                    catalog_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    is_array = table.Column<bool>(type: "boolean", nullable: false),
                    is_required = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_field_definitions", x => new { x.catalog_id, x.name });
                    table.CheckConstraint("ck_field_definitions_name_not_empty", "length(btrim(name)) > 0");
                    table.CheckConstraint("ck_field_definitions_type", "type IN ('Int', 'Decimal', 'String', 'Bool')");
                    table.ForeignKey(
                        name: "fk_field_definitions_catalogs",
                        column: x => x.catalog_id,
                        principalSchema: "catalog",
                        principalTable: "catalogs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "field_definitions",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "catalogs",
                schema: "catalog");
        }
    }
}
