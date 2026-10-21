namespace ExcelImporter.Domain.Rules;

/// <summary>
/// Primenjuje lookup pravilo nad jednim redom. Čista logika bez I/O; rečnik je već učitan.
/// Nepoznata vrednost je uvek greška reda, nikad tiha zamena NULL-om.
/// </summary>
public static class LookupEvaluator
{
    public static CellRuleResult Evaluate(
        LookupRule rule,
        LookupMap dictionary,
        IReadOnlyDictionary<string, object?> row,
        int rowNumber,
        bool targetIsNullable)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(dictionary);
        ArgumentNullException.ThrowIfNull(row);

        row.TryGetValue(rule.SourceField, out var cell);

        if (ConditionEvaluator.IsEmpty(cell))
        {
            return targetIsNullable
                ? new CellRuleResult(null)
                : Fail("Izvorna vrednost je prazna, a ciljna kolona ne dozvoljava NULL.");
        }

        var key = CellRuleEvaluator.AsText(cell).Trim();
        if (!dictionary.TryGetValue(key, out var value))
        {
            return Fail($"Vrednost \"{key}\" ne postoji u {rule.Schema}.{rule.Table}.{rule.KeyColumn}.");
        }

        return value is null
            ? Fail($"Vrednost \"{key}\" u {rule.Schema}.{rule.Table} ima NULL u koloni {rule.ReturnColumn}.")
            : new CellRuleResult(value);

        CellRuleResult Fail(string reason) =>
            new(cell, new RuleFailure(rowNumber, rule.TargetField, reason));
    }
}
