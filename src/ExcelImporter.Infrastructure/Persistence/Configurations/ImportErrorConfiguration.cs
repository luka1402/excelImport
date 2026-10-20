using ExcelImporter.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExcelImporter.Infrastructure.Persistence.Configurations;

internal sealed class ImportErrorConfiguration : IEntityTypeConfiguration<ImportError>
{
    public void Configure(EntityTypeBuilder<ImportError> builder)
    {
        builder.ToTable("ImportError");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.ColumnName).HasMaxLength(200);
        builder.Property(e => e.Reason).HasMaxLength(1000).IsRequired();

        builder.HasIndex(e => e.JobId);

        builder.HasOne(e => e.Job)
            .WithMany(j => j.Errors)
            .HasForeignKey(e => e.JobId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
