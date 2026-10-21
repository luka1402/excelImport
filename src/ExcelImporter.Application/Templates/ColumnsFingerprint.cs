using System.Security.Cryptography;
using System.Text;
using ExcelImporter.Application.Mapping;

namespace ExcelImporter.Application.Templates;

/// <summary>
/// Otisak skupa naziva kolona fajla (SHA-256, hex). Nazivi se normalizuju i sortiraju, pa promena redosleda,
/// velikih/malih slova ili razmaka ne menja otisak, a dodata, uklonjena ili preimenovana kolona menja.
/// </summary>
public static class ColumnsFingerprint
{
    public static string Compute(IEnumerable<string> columnNames)
    {
        ArgumentNullException.ThrowIfNull(columnNames);

        var normalized = columnNames
            .Select(ColumnNameNormalizer.Normalize)
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', normalized))));
    }
}