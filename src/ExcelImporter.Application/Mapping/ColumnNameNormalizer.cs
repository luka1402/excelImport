using System.Globalization;
using System.Text;

namespace ExcelImporter.Application.Mapping;

/// <summary>
/// Normalizacija naziva kolone za poređenje: ignoriše velika/mala slova, razmake i interpunkciju (razmak, _, -, .)
/// i dijakritike (š→s, č→c, ć→c, ž→z, đ→d). Ostaju samo slova i cifre, pa "Šifra Kupca", "sifra_kupca" i "SIFRAKUPCA" daju isti rezultat.
/// </summary>
public static class ColumnNameNormalizer
{
    public static string Normalize(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var decomposed = name.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            // đ nema dekompoziciju u Unicode-u, pa se zamenjuje ručno.
            var folded = character is 'đ' or 'Đ' ? 'd' : character;

            if (char.IsLetterOrDigit(folded))
            {
                builder.Append(char.ToLowerInvariant(folded));
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Sličnost dva već normalizovana naziva u opsegu 0..1 (Levenshtein). Nazivi koji se razlikuju po ciframa
    /// (npr. adresa1 i adresa2) nisu slični, jer su to obično dve različite kolone.
    /// </summary>
    public static double Similarity(string normalizedLeft, string normalizedRight)
    {
        if (normalizedLeft.Length == 0 || normalizedRight.Length == 0)
        {
            return 0;
        }

        if (normalizedLeft == normalizedRight)
        {
            return 1;
        }

        if (Digits(normalizedLeft) != Digits(normalizedRight))
        {
            return 0;
        }

        var distance = LevenshteinDistance(normalizedLeft, normalizedRight);
        return 1.0 - (double)distance / Math.Max(normalizedLeft.Length, normalizedRight.Length);
    }

    private static string Digits(string value) => new([.. value.Where(char.IsDigit)]);

    private static int LevenshteinDistance(string left, string right)
    {
        var previous = new int[right.Length + 1];
        var current = new int[right.Length + 1];

        for (var j = 0; j <= right.Length; j++)
        {
            previous[j] = j;
        }

        for (var i = 1; i <= left.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= right.Length; j++)
            {
                var substitution = previous[j - 1] + (left[i - 1] == right[j - 1] ? 0 : 1);
                current[j] = Math.Min(substitution, Math.Min(previous[j] + 1, current[j - 1] + 1));
            }

            (previous, current) = (current, previous);
        }

        return previous[right.Length];
    }
}
