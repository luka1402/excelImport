namespace ExcelImporter.Application.Files;

/// <summary>Sheet u radnoj svesci. <see cref="Index"/> je 0-based pozicija u fajlu.</summary>
public sealed record SheetInfo(int Index, string Name, bool IsHidden);

/// <summary>Sirovi pregled prvih redova sheet-a. Vrednosti su tekst (prazna ćelija je <c>null</c>); tipovi se određuju kasnije.</summary>
public sealed record SheetPreview(IReadOnlyList<IReadOnlyList<string?>> Rows, bool ColumnsTruncated);

/// <summary>Rezultat <c>analyze</c>: svi sheet-ovi i pregled izabranog.</summary>
public sealed record FileAnalysis(IReadOnlyList<SheetInfo> Sheets, SheetInfo SelectedSheet, SheetPreview Preview);
