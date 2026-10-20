namespace ExcelImporter.Infrastructure.Persistence;

/// <summary>Proverava da li je baza dostupna. Izdvojeno radi zamene u testovima.</summary>
public interface IDatabaseProbe
{
    Task CheckConnectionAsync(CancellationToken cancellationToken);
}