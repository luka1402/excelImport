using ExcelImporter.Application.Schema;
using ExcelImporter.Domain.Columns;

namespace ExcelImporter.Application.Mapping;

/// <summary>
/// Upozorenja o mapiranju. Pretpostavlja mapiranje koje je prošlo <see cref="MappingValidator"/>;
/// nepoznate kolone se tiho preskaču. Upozorenja ne blokiraju uvoz, to odlučuje korisnik.
/// </summary>
public static class MappingWarnings
{
    private static readonly HashSet<string> _textTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "char", "varchar", "nchar", "nvarchar", "text", "ntext",
    };

    /// <summary>Nepodudaran tip i nemapirana obavezna kolona (na osnovu prepoznate strukture, bez čitanja vrednosti).</summary>
    /// <param name="sourceColumns">Prepoznate kolone fajla (<see cref="SheetStructure.Columns"/>).</param>
    /// <param name="table">Ciljna tabela iz whitelist-a.</param>
    /// <param name="mappings">Mapiranje koje je korisnik izabrao.</param>
    /// <param name="columnsFilledByRules">Ciljne kolone koje puni neko pravilo (npr. podrazumevana vrednost); za njih nema upozorenja o obaveznoj koloni.</param>
    public static IReadOnlyList<MappingWarning> ForStructure(
        IReadOnlyList<ColumnProfile> sourceColumns,
        TableSchema table,
        IReadOnlyList<ColumnMapping> mappings,
        IEnumerable<string>? columnsFilledByRules = null)
    {
        ArgumentNullException.ThrowIfNull(sourceColumns);
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(mappings);

        var warnings = new List<MappingWarning>();
        var profiles = sourceColumns.GroupBy(column => column.Name).ToDictionary(group => group.Key, group => group.First());

        foreach (var mapping in mappings)
        {
            if (!profiles.TryGetValue(mapping.SourceColumn, out var profile) || table.FindColumn(mapping.TargetColumn) is not { } target)
            {
                continue;
            }

            if (!IsTypeCompatible(profile, target))
            {
                warnings.Add(new MappingWarning(
                    MappingWarningKind.TypeMismatch,
                    mapping.SourceColumn,
                    target.Name,
                    $"Kolona \"{mapping.SourceColumn}\" sadrži {Describe(profile)}, a ciljna kolona \"{target.Name}\" je tipa {target.DataType}."));
            }
        }

        var mappedTargets = new HashSet<string>(mappings.Select(mapping => mapping.TargetColumn), StringComparer.OrdinalIgnoreCase);
        var filledByRules = new HashSet<string>(columnsFilledByRules ?? [], StringComparer.OrdinalIgnoreCase);

        foreach (var column in table.Columns.Where(column => !column.IsNullable && !column.IsIdentity && !column.IsComputed))
        {
            if (!mappedTargets.Contains(column.Name) && !filledByRules.Contains(column.Name))
            {
                warnings.Add(new MappingWarning(
                    MappingWarningKind.RequiredColumnUnmapped,
                    null,
                    column.Name,
                    $"Obavezna kolona \"{column.Name}\" ne dozvoljava NULL, a nije mapirana."));
            }
        }

        return warnings;
    }

    /// <summary>
    /// Tekst duži od max dužine ciljne kolone. Isti metod služi za pregled (prvih 20 redova) i za ceo fajl u dry run-u;
    /// redovi se čitaju jednom, redom, bez držanja u memoriji. Vraća po jedno upozorenje po koloni sa brojem prekoračenja.
    /// </summary>
    /// <param name="sourceColumns">Prepoznate kolone fajla.</param>
    /// <param name="table">Ciljna tabela iz whitelist-a.</param>
    /// <param name="mappings">Mapiranje koje je korisnik izabrao.</param>
    /// <param name="dataRows">Redovi podataka (bez header-a); vrednosti su indeksirane pozicijom kolone u fajlu.</param>
    /// <param name="firstRowNumber">Broj reda u fajlu za prvi red iz <paramref name="dataRows"/>.</param>
    public static IReadOnlyList<MappingWarning> ForTextLength(
        IReadOnlyList<ColumnProfile> sourceColumns,
        TableSchema table,
        IReadOnlyList<ColumnMapping> mappings,
        IEnumerable<IReadOnlyList<string?>> dataRows,
        int firstRowNumber)
    {
        ArgumentNullException.ThrowIfNull(sourceColumns);
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(mappings);
        ArgumentNullException.ThrowIfNull(dataRows);

        var profiles = sourceColumns.GroupBy(column => column.Name).ToDictionary(group => group.Key, group => group.First());
        var checks = new List<TextLengthCheck>();

        foreach (var mapping in mappings)
        {
            if (profiles.TryGetValue(mapping.SourceColumn, out var profile)
                && table.FindColumn(mapping.TargetColumn) is { MaxLength: >= 0 } target
                && _textTypes.Contains(target.DataType))
            {
                checks.Add(new TextLengthCheck(mapping.SourceColumn, profile.Index, target.Name, target.MaxLength!.Value));
            }
        }

        if (checks.Count == 0)
        {
            return [];
        }

        var rowNumber = firstRowNumber;
        foreach (var row in dataRows)
        {
            foreach (var check in checks)
            {
                if (check.SourceIndex < row.Count && row[check.SourceIndex] is { } value)
                {
                    check.Add(rowNumber, value.Length);
                }
            }

            rowNumber++;
        }

        return [.. checks.Where(check => check.Count > 0).Select(check => check.ToWarning())];
    }

    private static bool IsTypeCompatible(ColumnProfile source, ColumnSchema target)
    {
        if (source.IsEmpty)
        {
            return true;
        }

        var dataType = target.DataType.ToLowerInvariant();
        if (_textTypes.Contains(dataType))
        {
            return true;
        }

        if (source.IsMixed || source.Type == ColumnType.Text)
        {
            return dataType == "uniqueidentifier";
        }

        return source.Type switch
        {
            ColumnType.WholeNumber => dataType is "tinyint" or "smallint" or "int" or "bigint" or "decimal" or "numeric" or "money" or "smallmoney" or "float" or "real",
            ColumnType.DecimalNumber => dataType is "decimal" or "numeric" or "money" or "smallmoney" or "float" or "real",
            ColumnType.Date => dataType is "date" or "datetime" or "datetime2" or "smalldatetime",
            ColumnType.Boolean => dataType == "bit",
            _ => false,
        };
    }

    private static string Describe(ColumnProfile profile) =>
        profile.IsMixed
            ? "mešovite vrednosti"
            : profile.Type switch
            {
                ColumnType.WholeNumber => "cele brojeve",
                ColumnType.DecimalNumber => "decimalne brojeve",
                ColumnType.Date => "datume",
                ColumnType.Boolean => "logičke vrednosti",
                _ => "tekst",
            };

    private sealed class TextLengthCheck(string sourceColumn, int sourceIndex, string targetColumn, int maxLength)
    {
        private int _firstRow;
        private int _longest;

        public int SourceIndex { get; } = sourceIndex;

        public int Count { get; private set; }

        public void Add(int rowNumber, int length)
        {
            if (length <= maxLength)
            {
                return;
            }

            if (Count++ == 0)
            {
                _firstRow = rowNumber;
            }

            _longest = Math.Max(_longest, length);
        }

        public MappingWarning ToWarning() => new(
            MappingWarningKind.TextTooLong,
            sourceColumn,
            targetColumn,
            $"{Count} vrednosti u koloni \"{sourceColumn}\" je duže od {maxLength} karaktera (najduža ima {_longest}), koliko dozvoljava kolona \"{targetColumn}\".",
            Count,
            _firstRow,
            _longest);
    }
}
