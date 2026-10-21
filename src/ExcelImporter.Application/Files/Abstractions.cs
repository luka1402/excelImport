namespace ExcelImporter.Application.Files;

/// <summary>Čita Excel fajl strimovanjem, bez učitavanja cele radne sveske u memoriju.</summary>
public interface IExcelSheetReader
{
    /// <exception cref="UnreadableExcelFileException">Sadržaj nije ispravan .xlsx.</exception>
    IReadOnlyList<SheetInfo> ListSheets(Stream content, CancellationToken cancellationToken);

    /// <exception cref="UnreadableExcelFileException">Sadržaj nije ispravan .xlsx.</exception>
    /// <exception cref="SheetNotFoundException">Sheet sa datim indeksom ne postoji.</exception>
    SheetPreview ReadPreview(Stream content, int sheetIndex, int rowCount, CancellationToken cancellationToken);
}

/// <summary>Pristup već otpremljenim fajlovima po identifikatoru; putanja se nikad ne gradi od korisničkog unosa.</summary>
public interface IUploadedFileStore
{
    /// <summary>Otvara fajl za čitanje (seekable) ili vraća <c>null</c> ako ne postoji. Pozivalac zatvara strim.</summary>
    Stream? OpenRead(Guid fileId);
}

public sealed class UnreadableExcelFileException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public sealed class InvalidHeaderRowException(int headerRowIndex, int rowCount)
    : Exception($"Red {headerRowIndex} ne postoji; sheet ima {rowCount} redova u uzorku (0-based indeks).");

public sealed class SheetNotFoundException(int sheetIndex)
    : Exception($"Sheet sa indeksom {sheetIndex} ne postoji.");
