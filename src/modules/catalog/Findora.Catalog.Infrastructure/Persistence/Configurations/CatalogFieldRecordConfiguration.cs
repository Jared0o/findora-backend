using Findora.Catalog.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Findora.Catalog.Infrastructure.Persistence.Configurations;

internal sealed class CatalogFieldRecordConfiguration : IEntityTypeConfiguration<CatalogFieldRecord>
{
    public void Configure(EntityTypeBuilder<CatalogFieldRecord> builder)
    {
        builder.ToTable("field_definitions", table =>
        {
            table.HasCheckConstraint("ck_field_definitions_name_not_empty", "length(btrim(name)) > 0");
            table.HasCheckConstraint("ck_field_definitions_type", "type IN ('Int', 'Decimal', 'String', 'Bool')");
        });
        builder.HasKey(field => new { field.CatalogId, field.Name }).HasName("pk_field_definitions");
        builder.Property(field => field.CatalogId).HasColumnName("catalog_id");
        builder.Property(field => field.Name).HasColumnName("name");
        builder.Property(field => field.Type).HasColumnName("type").HasConversion<string>().IsRequired();
        builder.Property(field => field.IsArray).HasColumnName("is_array");
        builder.Property(field => field.IsRequired).HasColumnName("is_required");
    }
}
