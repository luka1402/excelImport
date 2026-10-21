namespace ExcelImporter.Domain.Columns;

/// <summary>
/// Heuristika za header red među prvim redovima sheet-a. Header je prvi red koji:
/// ima samo tekstualne ćelije (broj, datum ili true/false ukazuju na podatke), bez ponovljenih naziva,
/// i popunjen je bar do polovine širine tabele (preskače naslove izveštaja sa jednom popunjenom ćelijom).
/// Ako nijedan red ne zadovoljava uslove, header ne postoji. Tabela čiji su svi redovi tekst
/// ne može se razlikovati od tabele bez header-a, pa se tada prvi popunjeni red uzima za header; korisnik to menja ručnim izborom.
/// </summary>
public static class HeaderRowDetector
{
    public const int SearchRowCount = 10;

    public static int? Detect(IReadOnlyList<IReadOnlyList<string?>> rows)
    {
        var candidates = rows.Take(SearchRowCount).ToList();
        var widest = candidates.Select(CountFilled).DefaultIfEmpty(0).Max();
        if (widest == 0)
        {
            return null;
        }

        for (var index = 0; index < candidates.Count; index++)
        {
            if (IsHeaderLike(candidates[index], widest))
            {
                return index;
            }
        }

        return null;
    }

    private static bool IsHeaderLike(IReadOnlyList<string?> row, int widest)
    {
        var filled = row.Where(cell => !string.IsNullOrWhiteSpace(cell)).Select(cell => cell!.Trim()).ToList();
        if (filled.Count == 0 || filled.Count * 2 < widest)
        {
            return false;
        }

        var allText = filled.All(cell => CellValueClassifier.Classify(cell) == ColumnType.Text);
        var distinct = filled.Distinct(StringComparer.OrdinalIgnoreCase).Count() == filled.Count;
        return allText && distinct;
    }

    private static int CountFilled(IReadOnlyList<string?> row) => row.Count(cell => !string.IsNullOrWhiteSpace(cell));
}
