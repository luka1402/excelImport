using System.Globalization;
using System.Text;
using System.Xml;
using ExcelDataReader;
using ExcelDataReader.Exceptions;
using ExcelImporter.Application.Files;
using Microsoft.Extensions.Options;

namespace ExcelImporter.Infrastructure.Excel;

/// <summary>
/// .xlsx čitač nad ExcelDataReader-om. Red po red se čita direktno iz strima fajla (bez DataSet-a),
/// pa se u memoriji drži samo traženi pregled.
/// </summary>
public sealed class ExcelDataSheetReader(IOptions<ExcelReadingOptions> options) : IExcelSheetReader
{
    private static readonly ExcelReaderConfiguration Configuration = CreateConfiguration();

    // ExcelReaderConfiguration traži Windows-1252, koji na .NET-u postoji tek posle registracije code-pages provajdera.
    private static ExcelReaderConfiguration CreateConfiguration()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return new ExcelReaderConfiguration { LeaveOpen = true };
    }

    public IReadOnlyList<SheetInfo> ListSheets(Stream content, CancellationToken cancellationToken)
    {
        return Guard(() =>
        {
            using var reader = ExcelReaderFactory.CreateOpenXmlReader(content, Configuration);
            var sheets = new List<SheetInfo>();
            do
            {
                cancellationToken.ThrowIfCancellationRequested();
                sheets.Add(new SheetInfo(sheets.Count, reader.Name, IsHidden(reader.VisibleState)));
            }
            while (reader.NextResult());

            return sheets;
        });
    }

    public SheetPreview ReadPreview(Stream content, int sheetIndex, int rowCount, CancellationToken cancellationToken)
    {
        return Guard(() =>
        {
            using var reader = ExcelReaderFactory.CreateOpenXmlReader(content, Configuration);
            for (var skipped = 0; skipped < sheetIndex; skipped++)
            {
                if (!reader.NextResult())
                {
                    throw new SheetNotFoundException(sheetIndex);
                }
            }

            return ReadRows(reader, rowCount, options.Value.MaxPreviewColumns, cancellationToken);
        });
    }

    private static SheetPreview ReadRows(IExcelDataReader reader, int rowCount, int maxColumns, CancellationToken cancellationToken)
    {
        var rows = new List<string?[]>(rowCount);
        var width = 0;
        var truncated = false;

        while (rows.Count < rowCount && reader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var columns = Math.Min(reader.FieldCount, maxColumns);
            truncated |= reader.FieldCount > maxColumns;
            width = Math.Max(width, columns);

            var row = new string?[columns];
            for (var column = 0; column < columns; column++)
            {
                row[column] = Format(reader.GetValue(column));
            }

            rows.Add(row);
        }

        // Svi redovi iste širine, da klijent dobije pravougaonu tabelu.
        var rectangular = rows
            .Select(row => (IReadOnlyList<string?>)[.. row, .. new string?[width - row.Length]])
            .ToList();
        return new SheetPreview(rectangular, truncated);
    }

    private static string? Format(object? value) => value switch
    {
        null => null,
        string text => text.Length == 0 ? null : text,
        bool flag => flag ? "true" : "false",
        DateTime date => date.TimeOfDay == TimeSpan.Zero
            ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : date.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture),
        double number => number.ToString("R", CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString(),
    };

    private static bool IsHidden(string? visibleState) =>
        !string.Equals(visibleState, "visible", StringComparison.OrdinalIgnoreCase);

    // Oštećen ili nevalidan sadržaj (nije ZIP, loš XML, DTD...) se prijavljuje domenskim izuzetkom; I/O greške se propagiraju.
    private static T Guard<T>(Func<T> read)
    {
        try
        {
            return read();
        }
        catch (Exception exception) when (exception is ExcelReaderException or InvalidDataException or XmlException or NotSupportedException)
        {
            throw new UnreadableExcelFileException("Fajl nije ispravan .xlsx dokument.", exception);
        }
    }
}
