using ExcelImporter.Application.Rules;
using ExcelImporter.Application.Schema;

namespace ExcelImporter.Application.Mapping;

/// <summary>
/// Validira konstante protiv ciljne tabele i računa njihove vrednosti. Tip literala proverava isti konvertor koji
/// se koristi pri uvozu, pa konstanta koja prođe validaciju ne može pasti pri uvozu. Dinamičke vrednosti se računaju
/// jednom po uvozu (<see cref="Resolve"/>), da svi redovi dobiju isti trenutak.
/// </summary>
public static class ColumnConstants
{
    private static readonly HashSet<string> _dateTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "date", "datetime", "datetime2", "smalldatetime",
    };

    /// <param name="table">Ciljna tabela iz whitelist-a.</param>
    /// <param name="constants">Konstante koje je korisnik zadao.</param>
    /// <param name="mappedTargetColumns">Ciljne kolone koje se pune iz fajla; konstanta za njih nije dozvoljena.</param>
    public static IReadOnlyList<MappingError> Validate(
        TableSchema table,
        IReadOnlyList<ColumnConstant> constants,
        IEnumerable<string> mappedTargetColumns)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(constants);
        ArgumentNullException.ThrowIfNull(mappedTargetColumns);

        var mapped = new HashSet<string>(mappedTargetColumns, StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var errors = new List<MappingError>();

        foreach (var constant in constants)
        {
            var column = table.FindColumn(constant.TargetColumn);
            if (column is null)
            {
                errors.Add(new MappingError(constant.TargetColumn, $"Kolona \"{constant.TargetColumn}\" ne postoji u tabeli \"{table.Schema}.{table.Name}\"."));
                continue;
            }

            if (column.IsIdentity || column.IsComputed)
            {
                errors.Add(new MappingError(column.Name, $"Kolona \"{column.Name}\" je {(column.IsIdentity ? "identity" : "computed")} i ne može imati konstantu."));
                continue;
            }

            if (!seen.Add(column.Name))
            {
                errors.Add(new MappingError(column.Name, $"Kolona \"{column.Name}\" ima više konstanti."));
                continue;
            }

            if (mapped.Contains(column.Name))
            {
                errors.Add(new MappingError(column.Name, $"Kolona \"{column.Name}\" je mapirana iz fajla, pa ne može imati konstantu."));
                continue;
            }

            if (ValidateValue(column, constant.Value) is { } message)
            {
                errors.Add(new MappingError(column.Name, message));
            }
        }

        return errors;
    }

    /// <summary>
    /// Vrednosti konstanti u tipu ciljne kolone, spremne da se upišu u red pre <c>RowPipeline.Process</c>.
    /// Pretpostavlja konstante koje su prošle <see cref="Validate"/>; <paramref name="now"/> je trenutak uvoza.
    /// </summary>
    public static IReadOnlyDictionary<string, object?> Resolve(
        TableSchema table,
        IReadOnlyList<ColumnConstant> constants,
        DateTime now)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(constants);

        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var constant in constants)
        {
            var column = table.FindColumn(constant.TargetColumn)
                ?? throw new InvalidOperationException($"Kolona \"{constant.TargetColumn}\" ne postoji u tabeli \"{table.Schema}.{table.Name}\".");
            var result = ColumnValueConverter.Convert(column, RawValue(constant.Value, now), 0);
            values[column.Name] = result.IsSuccess
                ? result.Value
                : throw new InvalidOperationException(result.Failure!.Reason);
        }

        return values;
    }

    private static string? ValidateValue(ColumnSchema column, ConstantValue value)
    {
        if (value.Literal is null == value.Dynamic is null)
        {
            return $"Konstanta za kolonu \"{column.Name}\" mora imati ili literal ili dinamičku vrednost.";
        }

        if (value.Dynamic is { } kind)
        {
            if (!Enum.IsDefined(kind))
            {
                return $"Nepoznata dinamička vrednost za kolonu \"{column.Name}\".";
            }

            return _dateTypes.Contains(column.DataType)
                ? null
                : $"Dinamička vrednost (danas/sada) može samo u kolonu tipa datum, a \"{column.Name}\" je tipa {column.DataType}.";
        }

        var result = ColumnValueConverter.Convert(column, value.Literal, 0);
        return result.IsSuccess ? null : $"Konstanta za kolonu \"{column.Name}\": {result.Failure!.Reason}";
    }

    private static object RawValue(ConstantValue value, DateTime now) => value.Dynamic switch
    {
        DynamicConstantKind.Today => now.Date,
        DynamicConstantKind.Now => now,
        _ => value.Literal!,
    };
}
