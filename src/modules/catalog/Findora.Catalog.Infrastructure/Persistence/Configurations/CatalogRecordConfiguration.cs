using Findora.Catalog.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Findora.Catalog.Infrastructure.Persistence.Configurations;

internal sealed class CatalogRecordConfiguration : IEntityTypeConfiguration<CatalogRecord>
{
    internal const string UniqueNameIndex = "ux_catalogs_normalized_name";

    public void Configure(EntityTypeBuilder<CatalogRecord> builder)
    {
        builder.ToTable("catalogs", table =>
            table.HasCheckConstraint("ck_catalogs_name_not_empty", "length(btrim(name)) > 0"));
        builder.HasKey(record => record.Id).HasName("pk_catalogs");
        builder.Property(record => record.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(record => record.Name).HasColumnName("name").IsRequired();
        builder.Property<string>("NormalizedName").HasColumnName("normalized_name")
            .HasComputedColumnSql("lower(btrim(name))", stored: true).IsRequired();
        builder.HasIndex("NormalizedName").IsUnique().HasDatabaseName(UniqueNameIndex);
        builder.Property(record => record.CreatedAt).HasColumnName("created_at")
            .HasColumnType("timestamp with time zone");
        builder.HasMany(record => record.Fields).WithOne()
            .HasForeignKey(field => field.CatalogId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_field_definitions_catalogs");
    }
}
