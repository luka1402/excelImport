using ExcelImporter.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExcelImporter.Infrastructure.Persistence.Configurations;

internal sealed class ImportJobConfiguration : IEntityTypeConfiguration<ImportJob>
{
    public void Configure(EntityTypeBuilder<ImportJob> builder)
    {
        builder.ToTable("ImportJob");
        builder.HasKey(j => j.Id);
        builder.Property(j => j.StartedAt).HasPrecision(3);
        builder.Property(j => j.FinishedAt).HasPrecision(3);
        builder.Property(j => j.FileName).HasMaxLength(260).IsRequired();
        builder.Property(j => j.TargetTable).HasMaxLength(300).IsRequired();
        builder.Property(j => j.Mode).HasConversion<string>().HasMaxLength(30);
        builder.Property(j => j.Status).HasConversion<string>().HasMaxLength(30);
        builder.Property(j => j.ErrorReportPath).HasMaxLength(500);

        builder.HasIndex(j => j.StartedAt).IsDescending();
        builder.HasIndex(j => j.ConnectionId);
        builder.HasIndex(j => j.TemplateId);

        builder.HasOne(j => j.Connection)
            .WithMany()
            .HasForeignKey(j => j.ConnectionId)
            .OnDelete(DeleteBehavior.Restrict);

        // Brisanje šablona ne briše istoriju uvoza.
        builder.HasOne(j => j.Template)
            .WithMany()
            .HasForeignKey(j => j.TemplateId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
