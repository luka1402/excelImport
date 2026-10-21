using ExcelImporter.Application.Schema;
using ExcelImporter.Domain.Rules;

namespace ExcelImporter.Application.Rules;

public enum RowOutcomeKind
{
    Imported,
    Skipped,
    Failed,
}

/// <summary>Rezultat jednog reda: uvezen (sa vrednostima ciljnih kolona), preskočen filterom ili greška sa listom grešaka.</summary>
public sealed record RowOutcome(
    int RowNumber,
    RowOutcomeKind Kind,
    IReadOnlyDictionary<string, object?> Values,
    IReadOnlyList<RuleFailure> Failures);

/// <summary>
/// Primenjuje pravila nad redovima u fiksnom redosledu: RowFilter → Transform → ConditionalValue → Default → Lookup
/// → konverzija u tip ciljne kolone. Svaka faza se izvrši nad svim kolonama pre sledeće, pa lookup vidi
/// transformisanu izvornu vrednost bez obzira na redosled kolona. Pravila iste faze primenjuju se redom iz liste.
/// Kolona koja je već dobila grešku ne ide u sledeće faze (nema kaskadnih grešaka), ali se ostale kolone i dalje obrađuju.
/// Lookup rečnici se učitavaju jednom (<see cref="CreateAsync"/>); <see cref="Process"/> je čista logika bez I/O.
/// Brojači nisu thread-safe: jedna instanca obrađuje jedan uvoz sekvencijalno.
/// </summary>
public sealed class RowPipeline
{
    private readonly IReadOnlyList<RowFilterRule> _filters;
    private readonly IReadOnlyList<TransformRule> _transforms;
    private readonly IReadOnlyList<ConditionalValueRule> _conditionals;
    private readonly IReadOnlyList<DefaultValueRule> _defaults;
    private readonly IReadOnlyList<(LookupRule Rule, LookupMap Dictionary)> _lookups;
    private readonly IReadOnlyList<ColumnSchema> _targetColumns;

    private RowPipeline(
        IReadOnlyList<Rule> rules,
        IReadOnlyList<ColumnSchema> targetColumns,
        IReadOnlyList<(LookupRule, LookupMap)> lookups)
    {
        _filters = [.. rules.OfType<RowFilterRule>()];
        _transforms = [.. rules.OfType<TransformRule>()];
        _conditionals = [.. rules.OfType<ConditionalValueRule>()];
        _defaults = [.. rules.OfType<DefaultValueRule>()];
        _lookups = lookups;
        _targetColumns = [.. targetColumns.Where(column => !column.IsComputed && !column.IsIdentity)];
    }

    public int ImportedCount { get; private set; }

    public int SkippedCount { get; private set; }

    public int FailedCount { get; private set; }

    /// <remarks>
    /// Identity i computed kolone se ne pune.
    /// </remarks>
    /// <param name="rules">Pravila uvoza; lookup pravila učitavaju svoje rečnike pri kreiranju.</param>
    /// <param name="targetColumns">Kolone ciljne tabele koje se pune; svaka dobija vrednost iz reda pod svojim imenom.</param>
    /// <param name="connectionString">Konekcija ka ciljnoj bazi iz koje se učitavaju lookup rečnici.</param>
    /// <param name="lookupLoader">Učitavač lookup rečnika.</param>
    /// <param name="cancellationToken">Token za otkazivanje.</param>
    /// <exception cref="LookupConfigurationException">Neispravan lookup (nepoznata tabela/kolona, duplikat ključa).</exception>
    public static async Task<RowPipeline> CreateAsync(
        IEnumerable<Rule> rules,
        IReadOnlyList<ColumnSchema> targetColumns,
        string connectionString,
        ILookupDictionaryLoader lookupLoader,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(targetColumns);
        ArgumentNullException.ThrowIfNull(lookupLoader);

        var ruleList = rules.ToList();
        var lookups = new List<(LookupRule, LookupMap)>();
        foreach (var lookup in ruleList.OfType<LookupRule>())
        {
            lookups.Add((lookup, await lookupLoader.LoadAsync(connectionString, lookup, cancellationToken)));
        }

        return new RowPipeline(ruleList, targetColumns, lookups);
    }

    public RowOutcome Process(IReadOnlyDictionary<string, object?> row, int rowNumber)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (!PassesFilters(row))
        {
            SkippedCount++;
            return new RowOutcome(rowNumber, RowOutcomeKind.Skipped, new Dictionary<string, object?>(), []);
        }

        var working = new Dictionary<string, object?>(row, StringComparer.OrdinalIgnoreCase);
        var failures = new List<RuleFailure>();
        var failedColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Apply(string targetField, CellRuleResult result)
        {
            if (result.IsSuccess)
            {
                working[targetField] = result.Value;
            }
            else
            {
                failures.Add(result.Failure!);
                failedColumns.Add(targetField);
            }
        }

        foreach (var rule in _transforms.Where(rule => !failedColumns.Contains(rule.TargetField)))
        {
            Apply(rule.TargetField, CellRuleEvaluator.Evaluate(rule, working, rowNumber));
        }

        foreach (var rule in _conditionals.Where(rule => !failedColumns.Contains(rule.TargetField)))
        {
            Apply(rule.TargetField, CellRuleEvaluator.Evaluate(rule, working, rowNumber));
        }

        foreach (var rule in _defaults.Where(rule => !failedColumns.Contains(rule.TargetField)))
        {
            Apply(rule.TargetField, CellRuleEvaluator.Evaluate(rule, working, rowNumber));
        }

        foreach (var (rule, dictionary) in _lookups)
        {
            if (failedColumns.Contains(rule.TargetField) || failedColumns.Contains(rule.SourceField))
            {
                continue;
            }

            var nullable = _targetColumns.FirstOrDefault(column => NameEquals(column.Name, rule.TargetField))?.IsNullable ?? true;
            Apply(rule.TargetField, LookupEvaluator.Evaluate(rule, dictionary, working, rowNumber, nullable));
        }

        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var column in _targetColumns.Where(column => !failedColumns.Contains(column.Name)))
        {
            working.TryGetValue(column.Name, out var cell);
            var converted = ColumnValueConverter.Convert(column, cell, rowNumber);
            if (converted.IsSuccess)
            {
                values[column.Name] = converted.Value;
            }
            else
            {
                failures.Add(converted.Failure!);
            }
        }

        if (failures.Count > 0)
        {
            FailedCount++;
            return new RowOutcome(rowNumber, RowOutcomeKind.Failed, new Dictionary<string, object?>(), failures);
        }

        ImportedCount++;
        return new RowOutcome(rowNumber, RowOutcomeKind.Imported, values, []);
    }

    // Include pravila se kombinuju sa I (red mora zadovoljiti sva), exclude pravilo isključuje red čim su mu uslovi ispunjeni.
    private bool PassesFilters(IReadOnlyDictionary<string, object?> row) =>
        _filters.All(filter =>
        {
            var matches = ConditionEvaluator.EvaluateAll(filter.Conditions, row);
            return filter.Action == RowFilterAction.Include ? matches : !matches;
        });

    private static bool NameEquals(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
