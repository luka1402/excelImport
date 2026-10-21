using System.ComponentModel.DataAnnotations;

namespace ExcelImporter.Infrastructure.Excel;

public sealed class ExcelReadingOptions
{
    public const string SectionName = "ExcelReading";

    /// <summary>Gornja granica širine pregleda; štiti odgovor od fajlova sa ogromnim brojem kolona.</summary>
    [Range(1, 16384)]
    public int MaxPreviewColumns { get; init; } = 256;

    /// <summary>Gornja granica zbira nekompresovanih veličina svih stavki u .xlsx arhivi; štiti od zip bombi.</summary>
    [Range(1, int.MaxValue)]
    public long MaxUncompressedBytes { get; init; } = 50L * 1024 * 1024;
}
