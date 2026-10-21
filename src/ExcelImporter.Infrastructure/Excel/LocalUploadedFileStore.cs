using ExcelImporter.Application.Files;
using Microsoft.Extensions.Options;

namespace ExcelImporter.Infrastructure.Excel;

/// <summary>
/// Otpremljeni fajlovi u jednom direktorijumu pod imenom <c>{fileId:N}.xlsx</c>. Ime se gradi isključivo od <see cref="Guid"/>-a,
/// pa korisnički unos ne može da utiče na putanju.
/// </summary>
public sealed class LocalUploadedFileStore(IOptions<UploadStorageOptions> options) : IUploadedFileStore
{
    public Stream? OpenRead(Guid fileId)
    {
        var path = Path.Combine(options.Value.ResolveDirectory(), $"{fileId:N}.xlsx");
        try
        {
            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, FileOptions.RandomAccess);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
    }
}

public sealed class UploadStorageOptions
{
    public const string SectionName = "UploadStorage";

    /// <summary>Direktorijum sa otpremljenim fajlovima; podrazumevano poddirektorijum sistemskog temp-a.</summary>
    public string? Directory { get; init; }

    public string ResolveDirectory() =>
        string.IsNullOrWhiteSpace(Directory) ? Path.Combine(Path.GetTempPath(), "excel-importer-uploads") : Directory;
}
