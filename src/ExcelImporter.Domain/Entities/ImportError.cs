namespace ExcelImporter.Domain.Entities;

/// <summary>Greška u jednom redu. U bazi se čuva ograničen broj po jobu; kompletan spisak ide u Excel izveštaj.</summary>
public sealed class ImportError
{
    public long Id { get; set; }

    public long JobId { get; set; }

    public ImportJob? Job { get; set; }

    public int RowNumber { get; set; }

    public string? ColumnName { get; set; }

    public required string Reason { get; set; }
}
