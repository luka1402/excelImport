using ExcelImporter.Application.Files;
using ExcelImporter.Application.Mapping;
using ExcelImporter.Application.Rules;
using ExcelImporter.Application.Schema;
using ExcelImporter.Domain.Rules;

namespace ExcelImporter.Application.Import;

/// <summary>Zahtev za dry run. <c>HeaderRowIndex</c> je 0-based indeks reda sa nazivima kolona; podaci počinju u sledećem redu.</summary>
public sealed record DryRunRequest(
    Guid FileId,
    int? SheetIndex,
    int HeaderRowIndex,
    string SchemaName,
    string TableName,
    IReadOnlyList<ColumnMapping> Mappings,
    IReadOnlyList<ColumnConstant> Constants,
    IReadOnlyList<Rule> Rules,
    string ConnectionString);

/// <summary>Greška jedne ćelije. <see cref="RowNumber"/> je broj reda u Excelu (1-based).</summary>
public sealed record DryRunError(int RowNumber, string Column, string Reason);

public sealed record DryRunPreviewRow(int RowNumber, IReadOnlyDictionary<string, object?> Values);

/// <summary>
/// Rezultat probnog prolaza. <see cref="ErrorCount"/> je ukupan broj grešaka, a <see cref="Errors"/> samo prvih
/// <see cref="DryRunService.MaxReportedErrors"/>. Potpuno prazni redovi se ne računaju ni u jednu grupu (<see cref="EmptyRowCount"/>).
/// </summary>
public sealed record DryRunResult(
    int ImportCount,
    int SkippedCount,
    int FailedRowCount,
    int EmptyRowCount,
    int ErrorCount,
    IReadOnlyList<DryRunError> Errors,
    IReadOnlyList<DryRunPreviewRow> PreviewRows)
{
    public bool ErrorsTruncated => ErrorCount > Errors.Count;
}

/// <summary>Mapiranje ili konstante ne prolaze proveru protiv fajla i ciljne šeme; dry run se ne pokreće.</summary>
public sealed class DryRunConfigurationException(IReadOnlyList<MappingError> errors)
    : Exception("Konfiguracija uvoza nije ispravna: " + string.Join(" ", errors.Select(error => error.Message)))
{
    public IReadOnlyList<MappingError> Errors { get; } = errors;
}

/// <summary>
/// Korak 6 čarobnjaka: strimujući prolaz kroz ceo sheet uz primenu pravila (<see cref="RowPipeline"/>), bez upisa u ciljnu bazu.
/// Iz ciljne baze se samo čita (šema i lookup rečnici). U memoriji se drže brojači, ograničena lista grešaka i pregled
/// prvih <see cref="PreviewRowCount"/> redova, pa potrošnja ne zavisi od veličine fajla.
/// </summary>
public sealed class DryRunService(
    IUploadedFileStore fileStore,
    IExcelSheetReader reader,
    ISchemaProvider schemaProvider,
    ILookupDictionaryLoader lookupLoader,
    TimeProvider timeProvider)
{
    public const int MaxReportedErrors = 100;
    public const int PreviewRowCount = 20;

    /// <summary>Vraća <c>null</c> ako fajl ne postoji.</summary>
    /// <exception cref="DryRunConfigurationException">Mapiranje ili konstante nisu ispravni.</exception>
    /// <exception cref="LookupConfigurationException">Neispravan lookup.</exception>
    /// <exception cref="InvalidHeaderRowException">Header red ne postoji u sheet-u.</exception>
    /// <exception cref="UnreadableExcelFileException">Fajl nije ispravan .xlsx.</exception>
    /// <exception cref="SheetNotFoundException">Sheet ne postoji.</exception>
    public async Task<DryRunResult?> RunAsync(DryRunRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var content = fileStore.OpenRead(request.FileId);
        if (content is null)
        {
            return null;
        }

        var schema = await schemaProvider.GetSchemaAsync(request.ConnectionString, cancellationToken);
        var rows = reader.StreamRows(content, sheets => FileAnalyzer.SelectSheet(sheets, request.SheetIndex), cancellationToken);
        using var enumerator = rows.GetEnumerator();

        var header = ReadHeader(enumerator, request.HeaderRowIndex);
        var table = ValidateConfiguration(request, schema, header.Keys);
        var constants = ColumnConstants.Resolve(table, request.Constants, timeProvider.GetUtcNow().UtcDateTime);
        var sourceIndexes = request.Mappings
            .Select(mapping => (Target: table.FindColumn(mapping.TargetColumn)!.Name, Index: header[mapping.SourceColumn]))
            .ToList();

        var pipeline = await RowPipeline.CreateAsync(request.Rules, table.Columns, request.ConnectionString, lookupLoader, cancellationToken);

        var errors = new List<DryRunError>();
        var preview = new List<DryRunPreviewRow>(PreviewRowCount);
        var errorCount = 0;
        var emptyRowCount = 0;
        var rowNumber = request.HeaderRowIndex + 1;

        while (enumerator.MoveNext())
        {
            cancellationToken.ThrowIfCancellationRequested();
            rowNumber++;
            var cells = enumerator.Current;
            if (cells.All(string.IsNullOrWhiteSpace))
            {
                emptyRowCount++;
                continue;
            }

            var outcome = pipeline.Process(BuildRow(cells, sourceIndexes, constants), rowNumber);
            switch (outcome.Kind)
            {
                case RowOutcomeKind.Imported when preview.Count < PreviewRowCount:
                    preview.Add(new DryRunPreviewRow(rowNumber, outcome.Values));
                    break;
                case RowOutcomeKind.Failed:
                    errorCount += outcome.Failures.Count;
                    foreach (var failure in outcome.Failures.Take(MaxReportedErrors - errors.Count))
                    {
                        errors.Add(new DryRunError(failure.RowNumber, failure.ColumnName, failure.Reason));
                    }

                    break;
            }
        }

        return new DryRunResult(pipeline.ImportedCount, pipeline.SkippedCount, pipeline.FailedCount, emptyRowCount, errorCount, errors, preview);
    }

    // Naziv kolone -> pozicija; prva kolona sa tim nazivom pobeđuje, prazni nazivi se ignorišu.
    private static Dictionary<string, int> ReadHeader(IEnumerator<IReadOnlyList<string?>> rows, int headerRowIndex)
    {
        if (headerRowIndex < 0)
        {
            throw new InvalidHeaderRowException(headerRowIndex, 0);
        }

        IReadOnlyList<string?>? headerRow = null;
        for (var index = 0; index <= headerRowIndex; index++)
        {
            if (!rows.MoveNext())
            {
                throw new InvalidHeaderRowException(headerRowIndex, index);
            }

            headerRow = rows.Current;
        }

        var header = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var column = 0; column < headerRow!.Count; column++)
        {
            if (headerRow[column]?.Trim() is { Length: > 0 } name)
            {
                header.TryAdd(name, column);
            }
        }

        return header;
    }

    private static TableSchema ValidateConfiguration(DryRunRequest request, DatabaseSchema schema, IEnumerable<string> sourceColumns)
    {
        var mappingResult = MappingValidator.Validate(request.SchemaName, request.TableName, request.Mappings, schema, sourceColumns);
        if (!mappingResult.IsValid)
        {
            throw new DryRunConfigurationException(mappingResult.Errors);
        }

        var table = schema.FindTable(request.SchemaName, request.TableName)!;
        var constantErrors = ColumnConstants.Validate(table, request.Constants, request.Mappings.Select(mapping => mapping.TargetColumn));
        if (constantErrors.Count > 0)
        {
            throw new DryRunConfigurationException([.. constantErrors]);
        }

        return table;
    }

    private static Dictionary<string, object?> BuildRow(
        IReadOnlyList<string?> cells,
        List<(string Target, int Index)> sourceIndexes,
        IReadOnlyDictionary<string, object?> constants)
    {
        var row = new Dictionary<string, object?>(constants, StringComparer.OrdinalIgnoreCase);
        foreach (var (target, index) in sourceIndexes)
        {
            row[target] = index < cells.Count ? cells[index] : null;
        }

        return row;
    }
}
