using System.Globalization;
using ExcelImporter.Application.Schema;
using ExcelImporter.Domain.Rules;

namespace ExcelImporter.Application.Rules;

/// <summary>
/// Konvertuje vrednost ćelije u CLR tip koji odgovara SQL tipu ciljne kolone. Greška nosi red, kolonu i razlog.
/// Prazna vrednost postaje NULL samo ako kolona dozvoljava NULL. Tekst se parsira invariantno
/// (decimalna tačka); drugačiji format treba da reši Transform pravilo pre konverzije.
/// </summary>
public static class ColumnValueConverter
{
    public static CellRuleResult Convert(ColumnSchema column, object? cell, int rowNumber)
    {
        ArgumentNullException.ThrowIfNull(column);

        CellRuleResult Fail(string reason) => new(cell, new RuleFailure(rowNumber, column.Name, reason));

        if (ConditionEvaluator.IsEmpty(cell))
        {
            return column.IsNullable
                ? new CellRuleResult(null)
                : Fail("Vrednost je prazna, a ciljna kolona ne dozvoljava NULL.");
        }

        var dataType = column.DataType.ToLowerInvariant();
        var text = AsText(cell).Trim();

        return dataType switch
        {
            "tinyint" => ConvertInteger(cell, text, byte.MinValue, byte.MaxValue, column, Fail, value => (byte)value),
            "smallint" => ConvertInteger(cell, text, short.MinValue, short.MaxValue, column, Fail, value => (short)value),
            "int" => ConvertInteger(cell, text, int.MinValue, int.MaxValue, column, Fail, value => (int)value),
            "bigint" => ConvertInteger(cell, text, long.MinValue, long.MaxValue, column, Fail, value => value),
            "decimal" or "numeric" => ConvertDecimal(cell, text, column, Fail, checkPrecision: true),
            "money" or "smallmoney" => ConvertDecimal(cell, text, column, Fail, checkPrecision: false),
            "float" => ConvertDouble(cell, text, Fail, value => value),
            "real" => ConvertDouble(cell, text, Fail, value => (float)value),
            "bit" => ConvertBoolean(cell, text, Fail),
            "date" or "datetime" or "datetime2" or "smalldatetime" => ConvertDate(cell, text, Fail),
            "uniqueidentifier" => Guid.TryParse(text, out var guid)
                ? new CellRuleResult(guid)
                : Fail($"Vrednost \"{text}\" nije ispravan GUID."),
            "char" or "varchar" or "nchar" or "nvarchar" or "text" or "ntext" => ConvertText(cell, column, Fail),
            _ => Fail($"Tip ciljne kolone \"{column.DataType}\" nije podržan za uvoz."),
        };
    }

    private static CellRuleResult ConvertInteger(
        object? cell, string text, long min, long max, ColumnSchema column, Func<string, CellRuleResult> fail, Func<long, object> create)
    {
        if (!TryWholeNumber(cell, text, out var number))
        {
            return fail($"Vrednost \"{text}\" nije ceo broj (tip {column.DataType}).");
        }

        return number < min || number > max
            ? fail($"Vrednost {number} je van opsega tipa {column.DataType}.")
            : new CellRuleResult(create(number));
    }

    private static bool TryWholeNumber(object? cell, string text, out long number)
    {
        switch (cell)
        {
            case decimal d when decimal.Truncate(d) == d && d >= long.MinValue && d <= long.MaxValue:
                number = (long)d;
                return true;
            case double or float:
                var floating = System.Convert.ToDouble(cell, CultureInfo.InvariantCulture);
                if (double.IsFinite(floating) && Math.Truncate(floating) == floating && Math.Abs(floating) < 9e18)
                {
                    number = (long)floating;
                    return true;
                }

                number = 0;
                return false;
            case bool:
                number = 0;
                return false;
            case int or long or short or byte or sbyte or ushort or uint:
                number = System.Convert.ToInt64(cell, CultureInfo.InvariantCulture);
                return true;
            case string:
                return long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out number);
            default:
                number = 0;
                return false;
        }
    }

    private static CellRuleResult ConvertDecimal(
        object? cell, string text, ColumnSchema column, Func<string, CellRuleResult> fail, bool checkPrecision)
    {
        decimal number;
        switch (cell)
        {
            case decimal d:
                number = d;
                break;
            case double or float:
                var floating = System.Convert.ToDouble(cell, CultureInfo.InvariantCulture);
                if (!double.IsFinite(floating) || Math.Abs(floating) >= (double)decimal.MaxValue)
                {
                    return fail($"Vrednost \"{text}\" nije ispravan broj.");
                }

                number = (decimal)floating;
                break;
            case int or long or short or byte or sbyte or ushort or uint or ulong:
                number = System.Convert.ToDecimal(cell, CultureInfo.InvariantCulture);
                break;
            case string when decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var parsed):
                number = parsed;
                break;
            default:
                return fail($"Vrednost \"{text}\" nije broj (tip {column.DataType}).");
        }

        if (checkPrecision && column.Precision > 0)
        {
            var rounded = decimal.Round(number, column.Scale, MidpointRounding.AwayFromZero);
            if (rounded != number)
            {
                return fail($"Vrednost {number.ToString(CultureInfo.InvariantCulture)} ima više decimala od dozvoljenih ({column.Scale}).");
            }

            var integerDigits = column.Precision - column.Scale;
            if (Math.Abs(number) >= Pow10(integerDigits))
            {
                return fail($"Vrednost {number.ToString(CultureInfo.InvariantCulture)} ne staje u {column.DataType}({column.Precision},{column.Scale}).");
            }
        }

        return new CellRuleResult(number);
    }

    private static decimal Pow10(int exponent)
    {
        var result = 1m;
        for (var i = 0; i < exponent; i++)
        {
            result *= 10m;
        }

        return result;
    }

    private static CellRuleResult ConvertDouble(object? cell, string text, Func<string, CellRuleResult> fail, Func<double, object> create)
    {
        double number;
        switch (cell)
        {
            case double or float or decimal or int or long or short or byte or sbyte or ushort or uint or ulong:
                number = System.Convert.ToDouble(cell, CultureInfo.InvariantCulture);
                break;
            case string when double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed):
                number = parsed;
                break;
            default:
                return fail($"Vrednost \"{text}\" nije broj.");
        }

        return double.IsFinite(number)
            ? new CellRuleResult(create(number))
            : fail($"Vrednost \"{text}\" nije konačan broj.");
    }

    private static CellRuleResult ConvertBoolean(object? cell, string text, Func<string, CellRuleResult> fail)
    {
        if (cell is bool flag)
        {
            return new CellRuleResult(flag);
        }

        return text.ToLowerInvariant() switch
        {
            "true" or "1" or "yes" or "da" => new CellRuleResult(true),
            "false" or "0" or "no" or "ne" => new CellRuleResult(false),
            _ => fail($"Vrednost \"{text}\" nije logička vrednost (true/false, 1/0, da/ne)."),
        };
    }

    private static CellRuleResult ConvertDate(object? cell, string text, Func<string, CellRuleResult> fail)
    {
        switch (cell)
        {
            case DateTime dateTime:
                return new CellRuleResult(dateTime);
            case DateOnly dateOnly:
                return new CellRuleResult(dateOnly.ToDateTime(TimeOnly.MinValue));
            case DateTimeOffset offset:
                return new CellRuleResult(offset.DateTime);
            case string when DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var parsed):
                return new CellRuleResult(parsed);
            default:
                return fail($"Vrednost \"{text}\" nije datum.");
        }
    }

    private static string AsText(object? cell) => cell switch
    {
        null => string.Empty,
        string text => text,
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => cell.ToString() ?? string.Empty,
    };

    private static CellRuleResult ConvertText(object? cell, ColumnSchema column, Func<string, CellRuleResult> fail)
    {
        var text = AsText(cell);
        return column.MaxLength is { } max && max >= 0 && text.Length > max
            ? fail($"Tekst ima {text.Length} karaktera, a kolona dozvoljava najviše {max}.")
            : new CellRuleResult(text);
    }
}
