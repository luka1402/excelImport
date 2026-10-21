namespace ExcelImporter.Domain.Rules;

/// <summary>Neispravna konfiguracija lookup pravila (nepoznata tabela/kolona, duplikat u ključnoj koloni). Prekida uvoz pre obrade redova.</summary>
public sealed class LookupConfigurationException(string message) : Exception(message);

/// <summary>
/// Rečnik jednog lookup pravila: ključ (trim, OrdinalIgnoreCase) → povratna vrednost. Gradi se jednom po uvozu.
/// Duplikat ključa nije dozvoljen jer bi "prvi pobeđuje" tiho dao pogrešan ID.
/// </summary>
public sealed class LookupMap
{
    private readonly Dictionary<string, object?> values = new(StringComparer.OrdinalIgnoreCase);

    public LookupMap(IEnumerable<KeyValuePair<string, object?>> entries, string description)
    {
        ArgumentNullException.ThrowIfNull(entries);

        foreach (var (rawKey, value) in entries)
        {
            var key = rawKey?.Trim();

            // Prazan ključ se nikad ne traži (prazna ćelija ne ide u lookup), pa ga preskačemo.
            if (string.IsNullOrEmpty(key))
            {
                continue;
            }

            if (!values.TryAdd(key, value))
            {
                throw new LookupConfigurationException(
                    $"Lookup {description}: ključna kolona sadrži duplikat vrednosti \"{key}\" (poređenje bez razlike velikih/malih slova i razmaka na krajevima).");
            }
        }
    }

    public int Count => values.Count;

    public bool TryGetValue(string key, out object? value) => values.TryGetValue(key.Trim(), out value);
}
