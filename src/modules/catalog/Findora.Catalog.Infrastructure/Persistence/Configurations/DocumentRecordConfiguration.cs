using Findora.Catalog.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Findora.Catalog.Infrastructure.Persistence.Configurations;

internal sealed class DocumentRecordConfiguration : IEntityTypeConfiguration<DocumentRecord>
{
    public void Configure(EntityTypeBuilder<DocumentRecord> builder)
    {
        builder.ToTable("documents", table => table.HasCheckConstraint("ck_documents_data_object",
            "jsonb_typeof(data) = 'object' AND data <> '{}'::jsonb"));
        builder.HasKey(record => record.Id).HasName("pk_documents");
        builder.Property(record => record.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(record => record.CatalogId).HasColumnName("catalog_id");
        builder.Property(record => record.Data).HasColumnName("data").HasColumnType("jsonb").IsRequired();
        builder.Property(record => record.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
        builder.HasIndex(record => record.CatalogId).HasDatabaseName("ix_documents_catalog_id");
        builder.HasOne<CatalogRecord>().WithMany().HasForeignKey(record => record.CatalogId)
            .OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_documents_catalogs");
    }
}
