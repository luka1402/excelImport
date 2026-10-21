namespace ExcelImporter.Application.Files;

/// <summary>Korak 2 čarobnjaka: lista sheet-ova i pregled prvih redova izabranog sheet-a.</summary>
public sealed class FileAnalyzer(IUploadedFileStore fileStore, IExcelSheetReader reader)
{
    public const int PreviewRowCount = 20;

    /// <summary>Vraća <c>null</c> ako fajl ne postoji. Bez <paramref name="sheetIndex"/> bira se prvi vidljiv sheet.</summary>
    public FileAnalysis? Analyze(Guid fileId, int? sheetIndex, CancellationToken cancellationToken)
    {
        using var content = fileStore.OpenRead(fileId);
        if (content is null)
        {
            return null;
        }

        var sheets = reader.ListSheets(content, cancellationToken);
        var selected = SelectSheet(sheets, sheetIndex);

        content.Position = 0;
        var preview = reader.ReadPreview(content, selected.Index, PreviewRowCount, cancellationToken);
        return new FileAnalysis(sheets, selected, preview);
    }

    private static SheetInfo SelectSheet(IReadOnlyList<SheetInfo> sheets, int? sheetIndex)
    {
        if (sheetIndex is { } index)
        {
            return sheets.FirstOrDefault(sheet => sheet.Index == index) ?? throw new SheetNotFoundException(index);
        }

        return sheets.FirstOrDefault(sheet => !sheet.IsHidden)
            ?? (sheets.Count > 0 ? sheets[0] : throw new UnreadableExcelFileException("Fajl ne sadrži nijedan sheet."));
    }
}
