using ExcelImporter.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExcelImporter.Infrastructure.Persistence.Configurations;

internal sealed class ConnectionConfigConfiguration : IEntityTypeConfiguration<ConnectionConfig>
{
    public void Configure(EntityTypeBuilder<ConnectionConfig> builder)
    {
        builder.ToTable("ConnectionConfig");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Name).HasMaxLength(100).IsRequired();
        builder.Property(c => c.DisplayName).HasMaxLength(200).IsRequired();
        // Samo ključ ka secret-u / env varijabli; connection string se nikad ne upisuje u bazu.
        builder.Property(c => c.ConfigKey).HasMaxLength(200).IsRequired();
        builder.HasIndex(c => c.Name).IsUnique();
    }
}
