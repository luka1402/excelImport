namespace ExcelImporter.Application.Schema;

/// <summary>Konekcija ka ciljnoj bazi onako kako je vidi klijent: bez connection stringa i bez ključa konfiguracije.</summary>
public sealed record ConnectionSummary(int Id, string DisplayName);

/// <summary>Konekcija ne postoji u katalogu konekcija.</summary>
public sealed class ConnectionNotFoundException(int connectionId)
    : Exception($"Konekcija {connectionId} ne postoji.");

/// <summary>Ciljna baza se ne može pročitati (nije podešena ili nije dostupna). Poruka je bezbedna za prikaz klijentu.</summary>
public sealed class ConnectionUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);

/// <summary>Katalog konekcija ka ciljnim bazama i čitanje njihove šeme (whitelist) preko <see cref="ISchemaProvider"/>.</summary>
public interface IConnectionCatalog
{
    Task<IReadOnlyList<ConnectionSummary>> ListAsync(CancellationToken cancellationToken);

    /// <exception cref="ConnectionNotFoundException"/>
    /// <exception cref="ConnectionUnavailableException"/>
    Task<DatabaseSchema> GetSchemaAsync(int connectionId, CancellationToken cancellationToken);
}
