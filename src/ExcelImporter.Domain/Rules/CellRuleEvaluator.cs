using System.Globalization;
using System.Text.RegularExpressions;

namespace ExcelImporter.Domain.Rules;

/// <summary>Greška primene pravila na ćeliju: red, kolona i razlog.</summary>
public sealed record RuleFailure(int RowNumber, string ColumnName, string Reason);

/// <summary>Ishod pravila: nova vrednost ciljne kolone ili greška (tada vrednost ostaje originalna).</summary>
public readonly record struct CellRuleResult(object? Value, RuleFailure? Failure = null)
{
    public bool IsSuccess => Failure is null;
}

/// <summary>
/// Primenjuje pravila Transform, DefaultValue i ConditionalValue nad jednim redom. Čista logika bez I/O.
/// Prazna ćelija se ne transformiše (ostaje prazna), a nevalidan datum/broj daje <see cref="RuleFailure"/>.
/// </summary>
public static class CellRuleEvaluator
{
    public static CellRuleResult Evaluate(Rule rule, IReadOnlyDictionary<string, object?> row, int rowNumber)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(row);

        return rule switch
        {
            TransformRule transform => ApplyTransform(transform, row, rowNumber),
            DefaultValueRule defaultValue => ApplyDefault(defaultValue, row),
            ConditionalValueRule conditional => ApplyConditional(conditional, row),
            _ => throw new ArgumentException($"Pravilo tipa \"{rule.Type}\" ne podržava ova evaluacija.", nameof(rule)),
        };
    }

    private static CellRuleResult ApplyDefault(DefaultValueRule rule, IReadOnlyDictionary<string, object?> row)
    {
        row.TryGetValue(rule.TargetField, out var cell);
        return new CellRuleResult(ConditionEvaluator.IsEmpty(cell) ? rule.Value : cell);
    }

    // Kada uslovi nisu ispunjeni a ElseValue nije zadat, ciljna kolona ostaje nepromenjena.
    private static CellRuleResult ApplyConditional(ConditionalValueRule rule, IReadOnlyDictionary<string, object?> row)
    {
        if (ConditionEvaluator.EvaluateAll(rule.Conditions, row))
        {
            return new CellRuleResult(rule.Value);
        }

        if (rule.ElseValue is not null)
        {
            return new CellRuleResult(rule.ElseValue);
        }

        row.TryGetValue(rule.TargetField, out var cell);
        return new CellRuleResult(cell);
    }

    private static CellRuleResult ApplyTransform(TransformRule rule, IReadOnlyDictionary<string, object?> row, int rowNumber)
    {
        row.TryGetValue(rule.TargetField, out var cell);
        if (ConditionEvaluator.IsEmpty(cell))
        {
            return new CellRuleResult(cell);
        }

        CellRuleResult Fail(string reason) =>
            new(cell, new RuleFailure(rowNumber, rule.TargetField, reason));

        switch (rule.Kind)
        {
            case TransformKind.Trim:
                return new CellRuleResult(AsText(cell).Trim());
            case TransformKind.Upper:
                return new CellRuleResult(AsText(cell).ToUpperInvariant());
            case TransformKind.Lower:
                return new CellRuleResult(AsText(cell).ToLowerInvariant());
            case TransformKind.DateFormat:
                return ParseDate(rule, cell, Fail);
            case TransformKind.DecimalSeparator:
                return ParseDecimal(rule, cell, Fail);
            default:
                throw new ArgumentOutOfRangeException(nameof(rule), rule.Kind, "Nepoznata transformacija.");
        }
    }

    private static CellRuleResult ParseDate(TransformRule rule, object? cell, Func<string, CellRuleResult> fail)
    {
        if (string.IsNullOrWhiteSpace(rule.Format))
        {
            return fail("Transformacija dateFormat zahteva format datuma.");
        }

        switch (cell)
        {
            case DateTime or DateOnly or DateTimeOffset:
                return new CellRuleResult(cell);
            case string text when DateTime.TryParseExact(
                text.Trim(), rule.Format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date):
                return new CellRuleResult(date);
            default:
                return fail($"Vrednost \"{AsText(cell)}\" nije datum u formatu {rule.Format}.");
        }
    }

    private static CellRuleResult ParseDecimal(TransformRule rule, object? cell, Func<string, CellRuleResult> fail)
    {
        if (rule.Separator is not ("," or "."))
        {
            return fail("Transformacija decimalSeparator zahteva separator \",\" ili \".\".");
        }

        if (cell is not string text)
        {
            // Excel već daje broj; separator se odnosi samo na tekstualne vrednosti.
            return IsNumeric(cell)
                ? new CellRuleResult(cell)
                : fail($"Vrednost \"{AsText(cell)}\" nije broj.");
        }

        var format = new NumberFormatInfo
        {
            NumberDecimalSeparator = rule.Separator,
            NumberGroupSeparator = rule.Separator == "," ? "." : ",",
        };

        var trimmed = text.Trim();

        // Separator suprotnog znaka je grupni; bez provere grupa bi "1.5" (separator ",") tiho postao 15.
        var hasValidGrouping = !trimmed.Contains(format.NumberGroupSeparator, StringComparison.Ordinal)
            || IsWellFormedGrouping(trimmed, format);

        return hasValidGrouping
            && decimal.TryParse(trimmed, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands, format, out var number)
            ? new CellRuleResult(number)
            : fail($"Vrednost \"{text}\" nije broj sa decimalnim separatorom \"{rule.Separator}\".");
    }

    private static bool IsWellFormedGrouping(string text, NumberFormatInfo format)
    {
        var group = Regex.Escape(format.NumberGroupSeparator);
        var decimalSeparator = Regex.Escape(format.NumberDecimalSeparator);
        return Regex.IsMatch(text, $@"^[+-]?\d{{1,3}}({group}\d{{3}})+({decimalSeparator}\d+)?$", RegexOptions.CultureInvariant);
    }

    private static bool IsNumeric(object? value) =>
        value is decimal or double or float or int or long or short or byte or sbyte or ushort or uint or ulong;

    internal static string AsText(object? cell) => cell switch
    {
        null => string.Empty,
        string text => text,
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => cell.ToString() ?? string.Empty,
    };
}
