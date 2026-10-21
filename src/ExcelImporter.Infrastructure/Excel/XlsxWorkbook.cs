using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using ExcelImporter.Application.Files;

namespace ExcelImporter.Infrastructure.Excel;

/// <summary>
/// Minimalan strimujući .xlsx čitač za pregled: lista sheet-ova dolazi iz <c>xl/workbook.xml</c>, a redovi se čitaju
/// XmlReader-om samo dok ne stignu traženi. Tabela shared strings se ne učitava u celosti, nego se iz nje
/// izvlače samo stringovi na koje pregled stvarno upućuje, pa memorija ne zavisi od veličine fajla.
/// </summary>
internal sealed class XlsxWorkbook : IDisposable
{
    private const string WorkbookPath = "xl/workbook.xml";
    private const string RelationshipsPath = "xl/_rels/workbook.xml.rels";
    private const string SharedStringsPath = "xl/sharedStrings.xml";
    private const string StylesPath = "xl/styles.xml";

    private static readonly XmlReaderSettings XmlSettings = new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        CloseInput = true,
    };

    private readonly ZipArchive archive;
    private readonly ByteBudget budget;
    private readonly List<SheetEntry> sheets;
    private readonly bool date1904;
    private bool[]? dateStyles;

    private XlsxWorkbook(ZipArchive archive, ByteBudget budget, List<SheetEntry> sheets, bool date1904)
    {
        this.archive = archive;
        this.budget = budget;
        this.sheets = sheets;
        this.date1904 = date1904;
    }

    public IReadOnlyList<SheetInfo> Sheets => [.. sheets.Select((sheet, index) => new SheetInfo(index, sheet.Name, sheet.IsHidden))];

    /// <exception cref="InvalidDataException">Nije ZIP ili nedostaju obavezni delovi radne sveske.</exception>
    /// <exception cref="UnreadableExcelFileException">Zbir nekompresovanih veličina prelazi <paramref name="maxUncompressedBytes"/>.</exception>
    public static XlsxWorkbook Open(Stream content, long maxUncompressedBytes)
    {
        var archive = new ZipArchive(content, ZipArchiveMode.Read, leaveOpen: true);
        try
        {
            // Deklarisane veličine iz central directory-ja se proveravaju pre bilo kakvog raspakivanja.
            long total = 0;
            foreach (var entry in archive.Entries)
            {
                total += entry.Length;
                if (total > maxUncompressedBytes)
                {
                    throw new UnreadableExcelFileException("Fajl je prevelik nakon raspakivanja.");
                }
            }

            var budget = new ByteBudget(maxUncompressedBytes);
            var relationships = ReadRelationships(archive, budget);
            var (entries, date1904) = ReadWorkbook(archive, budget, relationships);
            return new XlsxWorkbook(archive, budget, entries, date1904);
        }
        catch
        {
            archive.Dispose();
            throw;
        }
    }

    public SheetPreview ReadRows(int sheetIndex, int rowCount, int maxColumns, CancellationToken cancellationToken)
    {
        if (sheetIndex < 0 || sheetIndex >= sheets.Count)
        {
            throw new SheetNotFoundException(sheetIndex);
        }

        var sheetEntry = FindEntry(sheets[sheetIndex].Path)
            ?? throw new InvalidDataException("Sheet nema sadržaj u arhivi.");
        var rows = new List<List<string?>>(rowCount);
        var pendingStrings = new List<PendingString>();
        var truncated = false;

        using (var reader = CreateReader(sheetEntry))
        {
            var lastRowNumber = 0;
            while (rows.Count < rowCount && reader.Read())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "row")
                {
                    continue;
                }

                var rowNumber = int.TryParse(reader.GetAttribute("r"), NumberStyles.None, CultureInfo.InvariantCulture, out var declared) && declared > lastRowNumber
                    ? declared
                    : lastRowNumber + 1;

                // Preskočeni redovi su prazni redovi, kao i kod čitanja redom iz Excela.
                for (var gap = lastRowNumber + 1; gap < rowNumber && rows.Count < rowCount; gap++)
                {
                    rows.Add([]);
                }

                lastRowNumber = rowNumber;
                if (rows.Count >= rowCount)
                {
                    break;
                }

                var row = new List<string?>();
                rows.Add(row);
                truncated |= ReadCells(reader, row, rows.Count - 1, maxColumns, pendingStrings);
            }
        }

        ResolveSharedStrings(rows, pendingStrings, cancellationToken);

        var width = rows.Count == 0 ? 0 : rows.Max(row => row.Count);
        IReadOnlyList<IReadOnlyList<string?>> rectangular =
        [
            .. rows.Select(row =>
            {
                row.AddRange(new string?[width - row.Count]);
                return (IReadOnlyList<string?>)row;
            }),
        ];
        return new SheetPreview(rectangular, truncated);
    }

    public void Dispose() => archive.Dispose();

    private bool ReadCells(XmlReader reader, List<string?> row, int rowIndex, int maxColumns, List<PendingString> pendingStrings)
    {
        var truncated = false;
        if (reader.IsEmptyElement)
        {
            return false;
        }

        var rowDepth = reader.Depth;
        var nextColumn = 0;
        while (reader.Read() && reader.Depth > rowDepth)
        {
            if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "c" || reader.Depth != rowDepth + 1)
            {
                continue;
            }

            var column = ColumnIndex(reader.GetAttribute("r")) ?? nextColumn;
            nextColumn = column + 1;
            var type = reader.GetAttribute("t");
            var style = int.TryParse(reader.GetAttribute("s"), NumberStyles.None, CultureInfo.InvariantCulture, out var styleIndex) ? styleIndex : 0;
            var value = ReadCellValue(reader, type);

            if (column >= maxColumns)
            {
                truncated = true;
                continue;
            }

            while (row.Count <= column)
            {
                row.Add(null);
            }

            if (type == "s" && int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var sharedIndex))
            {
                pendingStrings.Add(new PendingString(rowIndex, column, sharedIndex));
            }
            else
            {
                row[column] = Format(value, type, style);
            }
        }

        return truncated;
    }

    // Vrednost ćelije: <v> za sve tipove, osim inlineStr gde je tekst u <is>. Čitanje ostaje unutar elementa ćelije.
    private static string? ReadCellValue(XmlReader reader, string? type)
    {
        if (reader.IsEmptyElement)
        {
            return null;
        }

        string? value = null;
        using var cell = reader.ReadSubtree();
        cell.Read();
        cell.Read();
        while (!cell.EOF)
        {
            if (cell.NodeType == XmlNodeType.Element && cell.Depth == 1 && cell.LocalName == "v" && type != "inlineStr")
            {
                value = cell.ReadElementContentAsString();
            }
            else if (cell.NodeType == XmlNodeType.Element && cell.Depth == 1 && cell.LocalName == "is")
            {
                value = ReadRichText(cell);
            }
            else
            {
                cell.Read();
            }
        }

        return value;
    }

    // Spaja <t> iz <si>/<is> (običan i formatiran tekst); fonetski <rPh> se preskače. Završava na prvom čvoru posle elementa.
    private static string ReadRichText(XmlReader reader)
    {
        if (reader.IsEmptyElement)
        {
            reader.Read();
            return string.Empty;
        }

        var text = new StringBuilder();
        var depth = reader.Depth;
        reader.Read();
        while (!reader.EOF && reader.Depth > depth)
        {
            if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "rPh")
            {
                reader.Skip();
            }
            else if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "t")
            {
                text.Append(reader.ReadElementContentAsString());
            }
            else
            {
                reader.Read();
            }
        }

        reader.Read();
        return text.ToString();
    }
    private string? Format(string? raw, string? type, int style)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return null;
        }

        switch (type)
        {
            case "b":
                return raw is "1" ? "true" : "false";
            case "str" or "e" or "inlineStr" or "d":
                return raw;
        }

        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            return raw;
        }

        if (IsDateStyle(style) && TryToDate(number, out var date))
        {
            return date.TimeOfDay == TimeSpan.Zero
                ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : date.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
        }

        return number.ToString("R", CultureInfo.InvariantCulture);
    }

    private bool TryToDate(double serial, out DateTime date)
    {
        try
        {
            date = DateTime.FromOADate(serial);
            if (date1904)
            {
                date = date.AddDays(1462);
            }

            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException)
        {
            date = default;
            return false;
        }
    }

    private bool IsDateStyle(int style)
    {
        dateStyles ??= ReadDateStyles();
        return style >= 0 && style < dateStyles.Length && dateStyles[style];
    }

    private bool[] ReadDateStyles()
    {
        var entry = FindEntry(StylesPath);
        if (entry is null)
        {
            return [];
        }

        var customFormats = new Dictionary<int, string>();
        var isDate = new List<bool>();
        using var reader = CreateReader(entry);
        var insideCellXfs = false;
        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element)
            {
                switch (reader.LocalName)
                {
                    case "numFmt" when int.TryParse(reader.GetAttribute("numFmtId"), NumberStyles.None, CultureInfo.InvariantCulture, out var id):
                        customFormats[id] = reader.GetAttribute("formatCode") ?? string.Empty;
                        break;
                    case "cellXfs":
                        insideCellXfs = true;
                        break;
                    case "xf" when insideCellXfs:
                        var formatId = int.TryParse(reader.GetAttribute("numFmtId"), NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
                        isDate.Add(customFormats.TryGetValue(formatId, out var code) ? IsDateFormatCode(code) : IsBuiltInDateFormat(formatId));
                        break;
                }
            }
            else if (reader.NodeType == XmlNodeType.EndElement && reader.LocalName == "cellXfs")
            {
                break;
            }
        }

        return [.. isDate];
    }

    private static bool IsBuiltInDateFormat(int id) => id is (>= 14 and <= 22) or (>= 27 and <= 36) or (>= 45 and <= 47) or (>= 50 and <= 58);

    // Ugrađeni znaci datuma/vremena (d m y h s) van navodnika i uglastih zagrada; "General" i brojčani formati to nemaju.
    private static bool IsDateFormatCode(string code)
    {
        var quoted = false;
        var bracketed = false;
        for (var position = 0; position < code.Length; position++)
        {
            var symbol = code[position];
            if (symbol == '\\' || symbol == '_' || symbol == '*')
            {
                position++;
            }
            else if (symbol == '"')
            {
                quoted = !quoted;
            }
            else if (symbol == '[')
            {
                bracketed = true;
            }
            else if (symbol == ']')
            {
                bracketed = false;
            }
            else if (!quoted && !bracketed && char.ToLowerInvariant(symbol) is 'd' or 'm' or 'y' or 'h' or 's')
            {
                return true;
            }
        }

        return false;
    }

    // Prolazi kroz sharedStrings.xml jednom, bez čuvanja stringova koji nisu u pregledu, i staje čim pronađe poslednji traženi.
    private void ResolveSharedStrings(List<List<string?>> rows, List<PendingString> pending, CancellationToken cancellationToken)
    {
        if (pending.Count == 0)
        {
            return;
        }

        var entry = FindEntry(SharedStringsPath);
        if (entry is null)
        {
            return;
        }

        var needed = pending.Select(item => item.SharedIndex).ToHashSet();
        var lastNeeded = needed.Max();
        var found = new Dictionary<int, string>(needed.Count);

        using (var reader = CreateReader(entry))
        {
            var index = 0;
            var more = reader.Read();
            while (more && index <= lastNeeded)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "si")
                {
                    if (needed.Contains(index))
                    {
                        found[index] = ReadRichText(reader);
                        more = !reader.EOF;
                    }
                    else
                    {
                        reader.Skip();
                        more = !reader.EOF;
                    }

                    index++;
                }
                else
                {
                    more = reader.Read();
                }
            }
        }

        foreach (var item in pending)
        {
            if (found.TryGetValue(item.SharedIndex, out var text) && text.Length > 0)
            {
                rows[item.Row][item.Column] = text;
            }
        }
    }

    private static Dictionary<string, string> ReadRelationships(ZipArchive archive, ByteBudget budget)
    {
        var entry = archive.GetEntry(RelationshipsPath) ?? throw new InvalidDataException("Nedostaju veze radne sveske.");
        var targets = new Dictionary<string, string>(StringComparer.Ordinal);
        using var reader = XmlReader.Create(budget.Wrap(entry.Open()), XmlSettings);
        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "Relationship"
                && reader.GetAttribute("Id") is { } id && reader.GetAttribute("Target") is { } target)
            {
                targets[id] = ResolvePath(target);
            }
        }

        return targets;
    }

    private static (List<SheetEntry> Sheets, bool Date1904) ReadWorkbook(ZipArchive archive, ByteBudget budget, Dictionary<string, string> relationships)
    {
        var entry = archive.GetEntry(WorkbookPath) ?? throw new InvalidDataException("Nedostaje radna sveska.");
        var entries = new List<SheetEntry>();
        var date1904 = false;
        using var reader = XmlReader.Create(budget.Wrap(entry.Open()), XmlSettings);
        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element)
            {
                continue;
            }

            if (reader.LocalName == "workbookPr")
            {
                date1904 = reader.GetAttribute("date1904") is "1" or "true";
            }
            else if (reader.LocalName == "sheet")
            {
                var name = reader.GetAttribute("name") ?? string.Empty;
                var hidden = reader.GetAttribute("state") is { } state && !string.Equals(state, "visible", StringComparison.OrdinalIgnoreCase);
                var relationshipId = reader.GetAttribute("id", "http://schemas.openxmlformats.org/officeDocument/2006/relationships");
                if (relationshipId is not null && relationships.TryGetValue(relationshipId, out var path))
                {
                    entries.Add(new SheetEntry(name, hidden, path));
                }
            }
        }

        return (entries, date1904);
    }

    // Cilj veze je relativan na xl/ ili apsolutan (počinje sa '/'); '..' segmenti se razrešavaju.
    private static string ResolvePath(string target)
    {
        var segments = new List<string>();
        var combined = target.StartsWith('/') ? target[1..] : "xl/" + target;
        foreach (var segment in combined.Split('/'))
        {
            if (segment == "..")
            {
                if (segments.Count > 0)
                {
                    segments.RemoveAt(segments.Count - 1);
                }
            }
            else if (segment is not ("" or "."))
            {
                segments.Add(segment);
            }
        }

        return string.Join('/', segments);
    }

    private ZipArchiveEntry? FindEntry(string path) =>
        archive.GetEntry(path) ?? archive.Entries.FirstOrDefault(entry => string.Equals(entry.FullName, path, StringComparison.OrdinalIgnoreCase));

    private XmlReader CreateReader(ZipArchiveEntry entry) => XmlReader.Create(budget.Wrap(entry.Open()), XmlSettings);

    // "AB12" -> 27 (0-based); null kad referenca nema slova.
    private static int? ColumnIndex(string? reference)
    {
        if (reference is null)
        {
            return null;
        }

        var column = 0;
        var letters = 0;
        foreach (var symbol in reference)
        {
            if (symbol is >= 'A' and <= 'Z')
            {
                column = (column * 26) + (symbol - 'A' + 1);
                letters++;
            }
            else if (symbol is >= 'a' and <= 'z')
            {
                column = (column * 26) + (symbol - 'a' + 1);
                letters++;
            }
            else
            {
                break;
            }
        }

        return letters == 0 || column > 16384 ? null : column - 1;
    }

    private sealed record SheetEntry(string Name, bool IsHidden, string Path);

    private readonly record struct PendingString(int Row, int Column, int SharedIndex);
}
