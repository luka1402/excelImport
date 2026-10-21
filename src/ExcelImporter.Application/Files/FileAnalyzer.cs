using ExcelImporter.Domain.Columns;

namespace ExcelImporter.Application.Files;

/// <summary>Korak 2 čarobnjaka: lista sheet-ova, pregled prvih redova izabranog sheet-a, header red i tipovi kolona.</summary>
public sealed class FileAnalyzer(IUploadedFileStore fileStore, IExcelSheetReader reader)
{
    public const int PreviewRowCount = 20;
    public const int SampleRowCount = SheetStructureDetector.DefaultSampleRowCount;

    /// <summary>
    /// Vraća <c>null</c> ako fajl ne postoji. Bez <paramref name="sheetIndex"/> bira se prvi vidljiv sheet;
    /// bez <paramref name="headerRowIndex"/> (0-based) header se prepoznaje automatski.
    /// </summary>
    /// <exception cref="InvalidHeaderRowException">Ručno izabran red ne postoji u uzorku sheet-a.</exception>
    public FileAnalysis? Analyze(Guid fileId, int? sheetIndex, CancellationToken cancellationToken, int? headerRowIndex = null)
    {
        using var content = fileStore.OpenRead(fileId);
        if (content is null)
        {
            return null;
        }

        var (sheets, selected, sample) = reader.ReadWorkbook(content, all => SelectSheet(all, sheetIndex), SampleRowCount, cancellationToken);
        var structure = DetectStructure(sample, headerRowIndex);
        var preview = sample with { Rows = [.. sample.Rows.Take(PreviewRowCount)] };
        return new FileAnalysis(sheets, selected, preview, structure);
    }

    private static SheetStructure DetectStructure(SheetPreview sample, int? headerRowIndex)
    {
        if (headerRowIndex is { } manual && (manual < 0 || manual >= sample.Rows.Count))
        {
            throw new InvalidHeaderRowException(manual, sample.Rows.Count);
        }

        return SheetStructureDetector.Detect(sample.Rows, headerRowIndex);
    }

    internal static SheetInfo SelectSheet(IReadOnlyList<SheetInfo> sheets, int? sheetIndex)
    {
        if (sheetIndex is { } index)
        {
            return sheets.FirstOrDefault(sheet => sheet.Index == index) ?? throw new SheetNotFoundException(index);
        }

        return sheets.FirstOrDefault(sheet => !sheet.IsHidden)
            ?? (sheets.Count > 0 ? sheets[0] : throw new UnreadableExcelFileException("Fajl ne sadrži nijedan sheet."));
    }
}
