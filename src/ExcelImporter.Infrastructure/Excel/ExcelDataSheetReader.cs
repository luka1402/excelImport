using System.Xml;
using ExcelImporter.Application.Files;
using Microsoft.Extensions.Options;

namespace ExcelImporter.Infrastructure.Excel;

/// <summary>
/// .xlsx čitač za pregled. Fajl se otvara jednom po pozivu, lista sheet-ova dolazi iz <c>xl/workbook.xml</c>,
/// a red po red se čita direktno iz strima (vidi <see cref="XlsxWorkbook"/>), pa se u memoriji drži samo traženi pregled.
/// </summary>
public sealed class ExcelDataSheetReader(IOptions<ExcelReadingOptions> options) : IExcelSheetReader
{
    public IReadOnlyList<SheetInfo> ListSheets(Stream content, CancellationToken cancellationToken)
    {
        return Guard(() =>
        {
            using var workbook = XlsxWorkbook.Open(content, options.Value.MaxUncompressedBytes);
            return workbook.Sheets;
        });
    }

    public SheetPreview ReadPreview(Stream content, int sheetIndex, int rowCount, CancellationToken cancellationToken)
    {
        return Guard(() =>
        {
            using var workbook = XlsxWorkbook.Open(content, options.Value.MaxUncompressedBytes);
            return workbook.ReadRows(sheetIndex, rowCount, options.Value.MaxPreviewColumns, cancellationToken);
        });
    }

    public WorkbookPreview ReadWorkbook(
        Stream content,
        Func<IReadOnlyList<SheetInfo>, SheetInfo> selectSheet,
        int rowCount,
        CancellationToken cancellationToken)
    {
        return Guard(() =>
        {
            using var workbook = XlsxWorkbook.Open(content, options.Value.MaxUncompressedBytes);
            var sheets = workbook.Sheets;
            var selected = selectSheet(sheets);
            var preview = workbook.ReadRows(selected.Index, rowCount, options.Value.MaxPreviewColumns, cancellationToken);
            return new WorkbookPreview(sheets, selected, preview);
        });
    }

    public IEnumerable<IReadOnlyList<string?>> StreamRows(
        Stream content,
        Func<IReadOnlyList<SheetInfo>, SheetInfo> selectSheet,
        CancellationToken cancellationToken)
    {
        // Fajl ostaje otvoren dok se enumeracija ne završi; izuzeci čitanja se prevode isto kao u ostalim metodama.
        using var workbook = Guard(() => XlsxWorkbook.Open(content, options.Value.MaxUncompressedBytes));
        var selected = selectSheet(workbook.Sheets);
        using var rows = workbook.EnumerateRows(selected.Index, options.Value.MaxPreviewColumns, cancellationToken).GetEnumerator();
        while (Guard(rows.MoveNext))
        {
            yield return rows.Current;
        }
    }

    // Oštećen ili nevalidan sadržaj (nije ZIP, loš XML, DTD...) se prijavljuje domenskim izuzetkom; I/O greške se propagiraju.
    private static T Guard<T>(Func<T> read)
    {
        try
        {
            return read();
        }
        catch (Exception exception) when (exception is InvalidDataException or XmlException or NotSupportedException)
        {
            throw new UnreadableExcelFileException("Fajl nije ispravan .xlsx dokument.", exception);
        }
    }
}
