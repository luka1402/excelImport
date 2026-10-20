using ExcelImporter.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExcelImporter.Infrastructure.Persistence;

/// <summary>Aplikaciona baza ExcelImporterDb: konekcije, šabloni i istorija uvoza.</summary>
public sealed class ExcelImporterDbContext(DbContextOptions<ExcelImporterDbContext> options) : DbContext(options)
{
    public DbSet<ConnectionConfig> Connections => Set<ConnectionConfig>();

    public DbSet<ImportTemplate> Templates => Set<ImportTemplate>();

    public DbSet<ImportJob> Jobs => Set<ImportJob>();

    public DbSet<ImportError> Errors => Set<ImportError>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ExcelImporterDbContext).Assembly);
}
