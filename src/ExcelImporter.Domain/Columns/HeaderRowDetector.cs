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
            if (IsHeaderLike(candidates[index], widest) && CoversFollowingRow(candidates, index))
            {
                return index;
            }
        }

        return DetectWithYearColumns(candidates, widest);
    }

    // Nazivi kolona su često godine ("2023", "2024"), pa strogi uslov "sve je tekst" odbija ispravan header.
    // Ovaj red se prihvata tek kad nijedan strogi red ne postoji: ima bar jedan tekst, sve ostalo su godine,
    // a red ispod u tim kolonama nema godine (inače bi i on mogao biti podatak iste vrste).
    private static int? DetectWithYearColumns(List<IReadOnlyList<string?>> candidates, int widest)
    {
        for (var index = 0; index < candidates.Count - 1; index++)
        {
            var filled = Filled(candidates[index]);
            if (filled.Count == 0 || filled.Count * 2 < widest || !HasDistinctValues(filled))
            {
                continue;
            }

            var types = candidates[index].Select(cell => CellValueClassifier.Classify(cell)).ToList();
            var hasText = types.Contains(ColumnType.Text);
            var yearColumns = Enumerable.Range(0, types.Count).Where(column => types[column] is not null and not ColumnType.Text).ToList();
            var onlyYears = yearColumns.All(column => IsYear(candidates[index][column]!));
            if (!hasText || yearColumns.Count == 0 || !onlyYears)
            {
                continue;
            }

            var next = candidates[index + 1];
            var nextAllYears = yearColumns.All(column => column < next.Count && next[column] is { } cell && IsYear(cell));
            if (!nextAllYears)
            {
                return index;
            }
        }

        return null;
    }

    private static bool IsYear(string cell) =>
        CellValueClassifier.Classify(cell) == ColumnType.WholeNumber
        && int.TryParse(cell.Trim(), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var year)
        && year is >= 1900 and <= 2100;

    private static List<string> Filled(IReadOnlyList<string?> row) =>
        row.Where(cell => !string.IsNullOrWhiteSpace(cell)).Select(cell => cell!.Trim()).ToList();

    private static bool HasDistinctValues(List<string> filled) =>
        filled.Distinct(StringComparer.OrdinalIgnoreCase).Count() == filled.Count;

    private static bool IsHeaderLike(IReadOnlyList<string?> row, int widest)
    {
        var filled = Filled(row);
        if (filled.Count == 0 || filled.Count * 2 < widest)
        {
            return false;
        }

        var allText = filled.All(cell => CellValueClassifier.Classify(cell) == ColumnType.Text);
        return allText && HasDistinctValues(filled);
    }

    // Naslov izveštaja sa delimično popunjenim redom (npr. 2 od 3 ćelije) ne sme da prođe kao header
    // kada sledeći popunjen red ima više ćelija, jer header mora imati naziv za svaku kolonu podataka.
    // Izuzetak je red sa praznom ćelijom unutar popunjenog raspona: naslov izveštaja je uvek sabijen,
    // a ovakav red je header kome nedostaje naziv kolone (dobija generisani naziv).
    private static bool CoversFollowingRow(List<IReadOnlyList<string?>> candidates, int index)
    {
        if (HasInnerGap(candidates[index]))
        {
            return true;
        }

        var filled = CountFilled(candidates[index]);
        var next = candidates.Skip(index + 1).Select(CountFilled).FirstOrDefault(count => count > 0);
        return filled >= next;
    }

    private static bool HasInnerGap(IReadOnlyList<string?> row)
    {
        var first = -1;
        var last = -1;
        for (var column = 0; column < row.Count; column++)
        {
            if (!string.IsNullOrWhiteSpace(row[column]))
            {
                first = first < 0 ? column : first;
                last = column;
            }
        }

        return first >= 0 && last - first + 1 > CountFilled(row);
    }

    private static int CountFilled(IReadOnlyList<string?> row) => row.Count(cell => !string.IsNullOrWhiteSpace(cell));
}
