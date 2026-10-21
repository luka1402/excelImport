namespace ExcelImporter.Domain.Columns;

/// <summary>
/// Određuje tip kolone nad uzorkom vrednosti. Pravila:
/// prazne ćelije se ignorišu; sve celobrojne → WholeNumber; celobrojne i decimalne → DecimalNumber;
/// sve datumi → Date; sve true/false → Boolean; svaka druga kombinacija (mešano) ili kolona bez vrednosti → Text.
/// </summary>
public static class ColumnTypeDetector
{
    public static (ColumnType Type, bool IsEmpty, bool IsMixed, int NonEmptyCount) Detect(IEnumerable<string?> values)
    {
        var counts = new int[Enum.GetValues<ColumnType>().Length];
        var nonEmpty = 0;

        foreach (var value in values)
        {
            if (CellValueClassifier.Classify(value) is { } kind)
            {
                counts[(int)kind]++;
                nonEmpty++;
            }
        }

        if (nonEmpty == 0)
        {
            return (ColumnType.Text, true, false, 0);
        }

        var integers = counts[(int)ColumnType.WholeNumber];
        var decimals = counts[(int)ColumnType.DecimalNumber];

        if (counts[(int)ColumnType.Text] == nonEmpty)
        {
            return (ColumnType.Text, false, false, nonEmpty);
        }

        if (integers == nonEmpty)
        {
            return (ColumnType.WholeNumber, false, false, nonEmpty);
        }

        if (integers + decimals == nonEmpty)
        {
            return (ColumnType.DecimalNumber, false, false, nonEmpty);
        }

        if (counts[(int)ColumnType.Date] == nonEmpty)
        {
            return (ColumnType.Date, false, false, nonEmpty);
        }

        if (counts[(int)ColumnType.Boolean] == nonEmpty)
        {
            return (ColumnType.Boolean, false, false, nonEmpty);
        }

        return (ColumnType.Text, false, true, nonEmpty);
    }
}
