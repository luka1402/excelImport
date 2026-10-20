using ExcelImporter.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExcelImporter.Infrastructure.Persistence.Configurations;

internal sealed class ImportTemplateConfiguration : IEntityTypeConfiguration<ImportTemplate>
{
    public void Configure(EntityTypeBuilder<ImportTemplate> builder)
    {
        builder.ToTable("ImportTemplate");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Name).HasMaxLength(200).IsRequired();
        builder.Property(t => t.SchemaName).HasMaxLength(128).IsRequired();
        builder.Property(t => t.TableName).HasMaxLength(128).IsRequired();
        builder.Property(t => t.SheetName).HasMaxLength(255);
        builder.Property(t => t.ColumnsFingerprint).HasMaxLength(64).IsRequired();
        builder.Property(t => t.MappingJson).IsRequired();
        builder.Property(t => t.RulesJson).IsRequired();
        builder.Property(t => t.ConstantsJson).IsRequired();
        builder.Property(t => t.CreatedAt).HasPrecision(3);
        builder.Property(t => t.UpdatedAt).HasPrecision(3);

        builder.HasIndex(t => t.Name).IsUnique();
        builder.HasIndex(t => t.ConnectionId);

        builder.HasOne(t => t.Connection)
            .WithMany()
            .HasForeignKey(t => t.ConnectionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
