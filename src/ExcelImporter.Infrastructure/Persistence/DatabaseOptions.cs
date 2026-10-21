using System.ComponentModel.DataAnnotations;

namespace ExcelImporter.Infrastructure.Persistence;

/// <summary>Konfiguracija aplikacione baze. Connection string dolazi samo iz env varijable ili user-secrets.</summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    [Required(AllowEmptyStrings = false, ErrorMessage = "Connection string baze nije podesen. Postavite environment varijablu Database__ConnectionString (ili user-secrets kljuc Database:ConnectionString).")]
    public string ConnectionString { get; init; } = string.Empty;

    [Range(1, 120)]
    public int HealthCheckTimeoutSeconds { get; init; } = 5;
}