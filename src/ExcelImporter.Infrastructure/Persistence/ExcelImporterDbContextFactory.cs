using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ExcelImporter.Infrastructure.Persistence;

/// <summary>Koristi samo `dotnet ef` alat. Connection string nikad nije u kodu: čita se iz env varijable EXCELIMPORTER_DESIGN_CONNECTION.</summary>
internal sealed class ExcelImporterDbContextFactory : IDesignTimeDbContextFactory<ExcelImporterDbContext>
{
    public const string ConnectionEnvironmentVariable = "EXCELIMPORTER_DESIGN_CONNECTION";

    public ExcelImporterDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable)
            ?? "Server=(localdb)\\MSSQLLocalDB;Database=ExcelImporterDb;Integrated Security=true;TrustServerCertificate=true";

        var options = new DbContextOptionsBuilder<ExcelImporterDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new ExcelImporterDbContext(options);
    }
}
