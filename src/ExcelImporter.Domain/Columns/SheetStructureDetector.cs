namespace ExcelImporter.Domain.Columns;

/// <summary>Korak 2: header red (automatski ili ručno izabran) i tipovi kolona nad uzorkom redova.</summary>
public static class SheetStructureDetector
{
    public const int DefaultSampleRowCount = 1000;

    /// <param name="rows">Redovi sheet-a od prvog; ćelije su tekst, prazna ćelija je <c>null</c>.</param>
    /// <param name="headerRowIndex">Ručno izabran header (0-based) ili <c>null</c> za automatsko prepoznavanje.</param>
    /// <param name="sampleRowCount">Koliko redova posle header-a ulazi u određivanje tipa.</param>
    /// <exception cref="ArgumentOutOfRangeException">Ručno izabran red ne postoji u prosleđenim redovima.</exception>
    public static SheetStructure Detect(
        IReadOnlyList<IReadOnlyList<string?>> rows,
        int? headerRowIndex = null,
        int sampleRowCount = DefaultSampleRowCount)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentOutOfRangeException.ThrowIfLessThan(sampleRowCount, 1);
        if (headerRowIndex is { } manual)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(manual, nameof(headerRowIndex));
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(manual, rows.Count, nameof(headerRowIndex));
        }

        var header = headerRowIndex ?? HeaderRowDetector.Detect(rows);
        var firstDataRow = header + 1 ?? 0;
        var sample = rows.Skip(firstDataRow).Take(sampleRowCount).ToList();
        var width = rows.Skip(header ?? 0).Select(row => row.Count).DefaultIfEmpty(0).Max();

        var columns = Enumerable.Range(0, width)
            .Select(column => ProfileColumn(column, header is { } h ? Cell(rows[h], column) : null, sample))
            .ToList();
        return new SheetStructure(header, headerRowIndex is not null, columns);
    }

    private static ColumnProfile ProfileColumn(int column, string? headerCell, List<IReadOnlyList<string?>> sample)
    {
        var name = string.IsNullOrWhiteSpace(headerCell) ? $"Kolona {column + 1}" : headerCell.Trim();
        var (type, isEmpty, isMixed, nonEmpty) = ColumnTypeDetector.Detect(sample.Select(row => Cell(row, column)));
        return new ColumnProfile(column, name, type, isEmpty, isMixed, nonEmpty);
    }

    private static string? Cell(IReadOnlyList<string?> row, int column) => column < row.Count ? row[column] : null;
}
