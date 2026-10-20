using System.Globalization;

namespace ExcelImporter.Domain.Rules;

/// <summary>
/// Evaluira uslove nad redom. Poređenje: ako se obe strane tumače kao brojevi poredi se numerički,
/// inače ako su obe datumi poredi se po datumu, u suprotnom tekstualno (ordinalno, bez razlike u veličini slova).
/// Kolona koja ne postoji u redu tretira se kao prazna.
/// </summary>
public static class ConditionEvaluator
{
    public static bool Evaluate(Condition condition, IReadOnlyDictionary<string, object?> row)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(row);

        row.TryGetValue(condition.Field, out var cell);
        return Evaluate(condition.Operator, cell, condition.Value);
    }

    /// <summary>Svi uslovi moraju biti ispunjeni (logičko I). Prazna lista je ispunjena.</summary>
    public static bool EvaluateAll(IEnumerable<Condition> conditions, IReadOnlyDictionary<string, object?> row)
    {
        ArgumentNullException.ThrowIfNull(conditions);
        return conditions.All(condition => Evaluate(condition, row));
    }

    public static bool Evaluate(ComparisonOperator comparison, object? cell, string? expected) => comparison switch
    {
        ComparisonOperator.IsEmpty => IsEmpty(cell),
        ComparisonOperator.IsNotEmpty => !IsEmpty(cell),
        ComparisonOperator.Contains => Text(cell).Contains(expected ?? string.Empty, StringComparison.OrdinalIgnoreCase),
        ComparisonOperator.StartsWith => Text(cell).StartsWith(expected ?? string.Empty, StringComparison.OrdinalIgnoreCase),
        ComparisonOperator.Eq => Compare(cell, expected) == 0,
        ComparisonOperator.Neq => Compare(cell, expected) != 0,
        ComparisonOperator.Gt => Compare(cell, expected) > 0,
        ComparisonOperator.Lt => Compare(cell, expected) < 0,
        ComparisonOperator.Gte => Compare(cell, expected) >= 0,
        ComparisonOperator.Lte => Compare(cell, expected) <= 0,
        _ => throw new ArgumentOutOfRangeException(nameof(comparison), comparison, "Nepoznat operator."),
    };

    public static bool IsEmpty(object? cell) => cell switch
    {
        null => true,
        string text => string.IsNullOrWhiteSpace(text),
        _ => false,
    };

    // Prazna vrednost je manja od svake neprazne, a dve prazne su jednake.
    private static int Compare(object? cell, string? expected)
    {
        var cellEmpty = IsEmpty(cell);
        var expectedEmpty = string.IsNullOrWhiteSpace(expected);
        if (cellEmpty || expectedEmpty)
        {
            return cellEmpty == expectedEmpty ? 0 : (cellEmpty ? -1 : 1);
        }

        if (TryNumber(cell, out var left) && TryNumber(expected, out var right))
        {
            return left.CompareTo(right);
        }

        if (TryDate(cell, out var leftDate) && TryDate(expected, out var rightDate))
        {
            return leftDate.CompareTo(rightDate);
        }

        return string.Compare(Text(cell).Trim(), expected!.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryNumber(object? value, out decimal number)
    {
        switch (value)
        {
            case decimal d:
                number = d;
                return true;
            case int or long or short or byte or sbyte or ushort or uint or ulong:
                number = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
                return true;
            case double or float:
                var floating = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                if (double.IsFinite(floating) && Math.Abs(floating) < (double)decimal.MaxValue)
                {
                    number = (decimal)floating;
                    return true;
                }

                break;
            case string text:
                return decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number);
        }

        number = 0;
        return false;
    }

    private static bool TryDate(object? value, out DateTime date)
    {
        switch (value)
        {
            case DateTime dateTime:
                date = dateTime;
                return true;
            case DateOnly dateOnly:
                date = dateOnly.ToDateTime(TimeOnly.MinValue);
                return true;
            case DateTimeOffset offset:
                date = offset.DateTime;
                return true;
            case string text:
                return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out date);
        }

        date = default;
        return false;
    }

    private static string Text(object? cell) => cell switch
    {
        null => string.Empty,
        string text => text,
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => cell.ToString() ?? string.Empty,
    };
}