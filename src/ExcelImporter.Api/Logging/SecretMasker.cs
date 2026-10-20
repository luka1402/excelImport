using System.Text.RegularExpressions;

namespace ExcelImporter.Api.Logging;

/// <summary>Maskira tajne u tekstu pre nego što stigne u log: poznate vrednosti iz konfiguracije i parove ključ=vrednost.</summary>
public sealed partial class SecretMasker(IConfiguration configuration)
{
    public const string Mask = "***";

    private static readonly string[] SecretConfigurationKeys = ["Database:ConnectionString"];

    public string Apply(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text ?? string.Empty;
        }

        var result = text;
        foreach (var secret in KnownSecrets())
        {
            result = result.Replace(secret, Mask, StringComparison.Ordinal);
        }

        return SecretPairPattern().Replace(result, match => $"{match.Groups["key"].Value}={Mask}");
    }

    private IEnumerable<string> KnownSecrets() =>
        SecretConfigurationKeys
            .Select(key => configuration[key])
            .OfType<string>()
            .Where(value => value.Length > 0);

    [GeneratedRegex(
        @"(?<key>\b(?:password|pwd|user\s*id|uid|secret|api[_-]?key|access[_-]?key|account[_-]?key|token)\b)\s*=\s*(?<value>""[^""]*""|'[^']*'|[^;\s""']+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SecretPairPattern();
}