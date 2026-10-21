using System.Globalization;

namespace ExcelImporter.Domain.Columns;

/// <summary>
/// Prepoznaje vrstu jedne tekstualne vrednosti. Uvek InvariantCulture: decimalni separator je isključivo tačka,
/// a zarez (npr. "1,5" ili "1,000") i separator hiljada nisu broj, pa vrednost ostaje tekst.
/// Datumi se prepoznaju samo u eksplicitnim formatima, da "03/04/2024" ne bi bio dvosmislen.
/// </summary>
public static class CellValueClassifier
{
    private static readonly string[] DateFormats =
    [
        "yyyy-MM-dd",
        "yyyy-MM-dd'T'HH:mm:ss",
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF",
        "yyyy-MM-dd HH:mm:ss",
        "dd.MM.yyyy",
        "dd.MM.yyyy.",
        "dd.MM.yyyy HH:mm:ss",
        "yyyy/MM/dd",
    ];

    private const NumberStyles IntegerStyles = NumberStyles.AllowLeadingSign;
    private const NumberStyles DecimalStyles = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent;

    /// <summary>Prazna vrednost (<c>null</c>, prazan ili samo razmaci) nema vrstu i ne utiče na tip kolone.</summary>
    public static ColumnType? Classify(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var text = value.Trim();
        if (IsBoolean(text))
        {
            return ColumnType.Boolean;
        }

        if (IsInteger(text))
        {
            return ColumnType.WholeNumber;
        }

        if (IsDecimal(text))
        {
            return ColumnType.DecimalNumber;
        }

        return IsDate(text) ? ColumnType.Date : ColumnType.Text;
    }

    private static bool IsBoolean(string text) =>
        text.Equals("true", StringComparison.OrdinalIgnoreCase) || text.Equals("false", StringComparison.OrdinalIgnoreCase);

    // Vodeće nule ("007", "0123") su šifre, ne brojevi: pretvaranje u broj bi ih nepovratno izgubilo.
    private static bool IsInteger(string text) =>
        !HasLeadingZero(text) && long.TryParse(text, IntegerStyles, CultureInfo.InvariantCulture, out _);

    private static bool IsDecimal(string text) =>
        !HasLeadingZero(text)
        && double.TryParse(text, DecimalStyles, CultureInfo.InvariantCulture, out var number)
        && double.IsFinite(number);

    private static bool IsDate(string text) =>
        DateTime.TryParseExact(text, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    private static bool HasLeadingZero(string text)
    {
        var digits = text.AsSpan().TrimStart("+-");
        return digits.Length > 1 && digits[0] == '0' && char.IsAsciiDigit(digits[1]);
    }
}
