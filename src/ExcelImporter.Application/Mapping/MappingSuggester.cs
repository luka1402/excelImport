using ExcelImporter.Application.Schema;
using ExcelImporter.Domain.Columns;

namespace ExcelImporter.Application.Mapping;

/// <summary>
/// Predlog mapiranja po nazivu kolone. Prvo se uparuju nazivi koji su isti posle normalizacije, zatim slični
/// (sličnost ≥ <see cref="SimilarityThreshold"/>), najbolji parovi prvi. Svaka kolona fajla i svaka ciljna kolona
/// učestvuje u najviše jednom predlogu. Identity i computed kolone se ne predlažu jer se ne pune iz fajla.
/// </summary>
public static class MappingSuggester
{
    public const double SimilarityThreshold = 0.8;

    public static IReadOnlyList<MappingSuggestion> Suggest(IReadOnlyList<ColumnProfile> sourceColumns, TableSchema table)
    {
        ArgumentNullException.ThrowIfNull(sourceColumns);
        ArgumentNullException.ThrowIfNull(table);

        var sources = sourceColumns
            .Select(column => (column.Name, Key: ColumnNameNormalizer.Normalize(column.Name)))
            .Where(source => source.Key.Length > 0)
            .ToList();
        var targets = table.Columns
            .Where(column => !column.IsIdentity && !column.IsComputed)
            .Select(column => (column.Name, Key: ColumnNameNormalizer.Normalize(column.Name)))
            .Where(target => target.Key.Length > 0)
            .ToList();

        var candidates = (
            from source in sources
            from target in targets
            let score = ColumnNameNormalizer.Similarity(source.Key, target.Key)
            where score >= SimilarityThreshold
            orderby score descending
            select (Source: source.Name, Target: target.Name, Score: score))
            .ToList();

        var usedSources = new HashSet<string>(StringComparer.Ordinal);
        var usedTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var suggestions = new List<MappingSuggestion>();

        // OrderByDescending je stabilan, pa pri istoj oceni važi redosled kolona u fajlu.
        foreach (var candidate in candidates)
        {
            if (usedSources.Contains(candidate.Source) || usedTargets.Contains(candidate.Target))
            {
                continue;
            }

            usedSources.Add(candidate.Source);
            usedTargets.Add(candidate.Target);
            suggestions.Add(new MappingSuggestion(
                candidate.Source,
                candidate.Target,
                candidate.Score >= 1 ? MatchKind.Exact : MatchKind.Similar));
        }

        var sourceOrder = sourceColumns.Select((column, position) => (column.Name, position))
            .GroupBy(item => item.Name).ToDictionary(group => group.Key, group => group.First().position);
        return [.. suggestions.OrderBy(suggestion => sourceOrder[suggestion.SourceColumn])];
    }
}
