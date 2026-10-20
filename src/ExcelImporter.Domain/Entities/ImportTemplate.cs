namespace ExcelImporter.Domain.Entities;

/// <summary>Sačuvano mapiranje, pravila i konstante za uvoz u jednu ciljnu tabelu.</summary>
public sealed class ImportTemplate
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public int ConnectionId { get; set; }

    public ConnectionConfig? Connection { get; set; }

    public required string SchemaName { get; set; }

    public required string TableName { get; set; }

    public string? SheetName { get; set; }

    public int HeaderRow { get; set; } = 1;

    /// <summary>Otisak naziva kolona iz Excel fajla; služi za upozorenje o razlikama pri primeni šablona.</summary>
    public required string ColumnsFingerprint { get; set; }

    public required string MappingJson { get; set; }

    public required string RulesJson { get; set; }

    public required string ConstantsJson { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
