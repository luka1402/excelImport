import { useEffect, useMemo, useState } from 'react';
import { t, type MessageKey } from '../../../messages.sr';
import { EmptyState, ErrorState, LoadingState } from '../../../ui/states';
import { useWizard } from '../../WizardContext';
import type { ColumnRule, RuleDraft } from '../../types';
import { useDatabaseSchema, type ColumnInfo } from '../target/targetApi';
import { RuleForm } from './RuleForm';
import { RowFiltersSection } from './RowFiltersSection';
import { describeRule, emptyCondition, newRuleId } from './rulesLogic';
import styles from './rules.module.css';

type RuleType = ColumnRule['type'];

const ruleTypes: readonly { readonly type: RuleType; readonly labelKey: MessageKey }[] = [
  { type: 'conditionalValue', labelKey: 'rules.type.conditional' },
  { type: 'defaultValue', labelKey: 'rules.type.default' },
  { type: 'transform', labelKey: 'rules.type.transform' },
  { type: 'lookup', labelKey: 'rules.type.lookup' },
];

function newRule(type: RuleType, targetField: string): ColumnRule {
  const id = newRuleId();
  switch (type) {
    case 'conditionalValue':
      return { id, type, targetField, conditions: [emptyCondition(targetField)], value: '', elseValue: '' };
    case 'defaultValue':
      return { id, type, targetField, value: '' };
    case 'transform':
      return { id, type, targetField, kind: 'trim', format: '', separator: '' };
    case 'lookup':
      return { id, type, targetField, sourceField: targetField, schema: 'dbo', table: '', keyColumn: '', returnColumn: '' };
  }
}

export function RulesStep() {
  const { data, updateData } = useWizard();
  const schema = useDatabaseSchema(data.connectionId);
  const [selected, setSelected] = useState<string | null>(null);
  const [enabled, setEnabled] = useState<ReadonlySet<string>>(new Set());
  const [editing, setEditing] = useState<RuleDraft | null>(null);
  const [deletingId, setDeletingId] = useState<string | null>(null);
  const [newType, setNewType] = useState<RuleType>('conditionalValue');

  const table = schema.data?.tables.find(
    (candidate) => candidate.schema === data.tableSchema && candidate.name === data.table,
  );
  const mappedColumns = useMemo(() => {
    const byName = new Map<string, ColumnInfo>(table?.columns.map((column) => [column.name.toLowerCase(), column]));
    const mapped = new Map<string, ColumnInfo>();
    for (const choice of Object.values(data.mapping)) {
      const column = byName.get(choice.target.toLowerCase());
      if (column) {
        mapped.set(column.name, column);
      }
    }
    return mapped;
  }, [table, data.mapping]);

  const isEditing = editing !== null;
  useEffect(() => {
    updateData({ rulesEditing: isEditing });
  }, [isEditing, updateData]);
  // NapuÅ¡tanje koraka sa otvorenom formom ne sme ostaviti blokiran nastavak.
  useEffect(
    () => () => {
      updateData({ rulesEditing: false });
    },
    [updateData],
  );

  if (data.mappingKey === '' || !data.mappingComplete) {
    return <EmptyState message={t('rules.noMappedColumns')} />;
  }
  if (schema.isError) {
    return <ErrorState message={t('target.table.error')} onRetry={() => { void schema.refetch(); }} />;
  }
  if (!table) {
    return <LoadingState />;
  }
  if (mappedColumns.size === 0) {
    return <EmptyState message={t('rules.noMappedColumns')} />;
  }

  const names = [...mappedColumns.keys()];
  const current = selected !== null && mappedColumns.has(selected) ? selected : (names[0] ?? '');
  const rulesFor = (name: string) => data.rules.filter((rule) => rule.targetField === name);
  const columnRules = rulesFor(current);
  const hasCondition = columnRules.length > 0 || enabled.has(current);

  const lookupTaken = columnRules.some((rule) => rule.type === 'lookup');
  // Kolona prima vrednost iz jednog lookup-a; drugi bi pregazio prvi.
  const effectiveNewType = lookupTaken && newType === 'lookup' ? 'conditionalValue' : newType;
  const tables = schema.data?.tables ?? [];

  const upsert = <TRule extends RuleDraft>(list: readonly TRule[], rule: TRule): TRule[] =>
    list.some((candidate) => candidate.id === rule.id)
      ? list.map((candidate) => (candidate.id === rule.id ? rule : candidate))
      : [...list, rule];
  const save = (rule: RuleDraft) => {
    updateData(rule.type === 'rowFilter' ? { rowFilters: upsert(data.rowFilters, rule) } : { rules: upsert(data.rules, rule) });
    setEditing(null);
  };
  const remove = (id: string) => {
    updateData({ rules: data.rules.filter((rule) => rule.id !== id) });
    setDeletingId(null);
  };
  const removeFilter = (id: string) => {
    updateData({ rowFilters: data.rowFilters.filter((filter) => filter.id !== id) });
  };

  return (
    <>
      <div className={styles.layout}>
        <nav aria-label={t('rules.columns')}>
          <h2>{t('rules.columns')}</h2>
          <ul className={styles.columnList}>
            {names.map((name) => (
              <li key={name}>
                <button
                  type="button"
                  aria-current={name === current ? 'true' : undefined}
                  disabled={isEditing && name !== current}
                  onClick={() => {
                    setSelected(name);
                    setDeletingId(null);
                  }}
                >
                  {name} ({rulesFor(name).length})
                </button>
              </li>
            ))}
          </ul>
        </nav>

        <section aria-labelledby="rules-column-heading">
          <h2 id="rules-column-heading">{t('rules.column', { name: current })}</h2>
          <fieldset>
            <legend>{t('rules.hasCondition')}</legend>
            <label>
              <input
                type="radio"
                name={`has-condition-${current}`}
                checked={!hasCondition}
                disabled={columnRules.length > 0}
                onChange={() => {
                  setEnabled((previous) => new Set([...previous].filter((name) => name !== current)));
                }}
              />{' '}
              {t('rules.no')}
            </label>
            <label>
              <input
                type="radio"
                name={`has-condition-${current}`}
                checked={hasCondition}
                onChange={() => {
                  setEnabled((previous) => new Set(previous).add(current));
                }}
              />{' '}
              {t('rules.yes')}
            </label>
            {columnRules.length > 0 && <small className={styles.note}>{t('rules.noRemoveFirst')}</small>}
          </fieldset>

          {hasCondition && (
            <>
              {columnRules.length === 0 && !isEditing && <p role="status">{t('rules.empty')}</p>}
              {columnRules.length > 0 && (
                <>
                  <h3>{t('rules.appliedOrder')}</h3>
                  <ol className={styles.ruleList}>
                    {columnRules.map((rule) => (
                      <li key={rule.id}>
                        {editing?.id === rule.id ? (
                          <RuleForm rule={editing} columns={mappedColumns} tables={tables} onSave={save} onCancel={() => { setEditing(null); }} />
                        ) : (
                          <>
                            <span>{describeRule(rule)}</span>
                            {deletingId === rule.id ? (
                              <span role="alertdialog" aria-label={t('rules.confirmDelete')}>
                                {t('rules.confirmDelete')}{' '}
                                <button type="button" onClick={() => { setDeletingId(null); }}>
                                  {t('rules.cancel')}
                                </button>
                                <button type="button" onClick={() => { remove(rule.id); }}>
                                  {t('rules.confirmDeleteYes')}
                                </button>
                              </span>
                            ) : (
                              <span className={styles.actions}>
                                <button type="button" disabled={isEditing} onClick={() => { setEditing(rule); }}>
                                  {t('rules.edit')}
                                </button>
                                <button
                                  type="button"
                                  aria-label={`${t('rules.remove')}: ${describeRule(rule)}`}
                                  disabled={isEditing}
                                  onClick={() => { setDeletingId(rule.id); }}
                                >
                                  âœ•
                                </button>
                              </span>
                            )}
                          </>
                        )}
                      </li>
                    ))}
                  </ol>
                </>
              )}

              {editing !== null && editing.type !== 'rowFilter' && !columnRules.some((rule) => rule.id === editing.id) && (
                <RuleForm rule={editing} columns={mappedColumns} tables={tables} onSave={save} onCancel={() => { setEditing(null); }} />
              )}

              {!isEditing && (
                <div className={styles.addRow}>
                  <label>
                    {t('rules.addType')}
                    <select
                      value={effectiveNewType}
                      onChange={(event) => {
                        setNewType(event.target.value as RuleType);
                      }}
                    >
                      {ruleTypes.map((option) => (
                        <option key={option.type} value={option.type} disabled={option.type === 'lookup' && lookupTaken}>
                          {t(option.labelKey)}
                        </option>
                      ))}
                    </select>
                  </label>
                  <button type="button" onClick={() => { setEditing(newRule(effectiveNewType, current)); }}>
                    + {t('rules.add')}
                  </button>
                </div>
              )}
            </>
          )}
        </section>
      </div>
      <RowFiltersSection
        filters={data.rowFilters}
        columns={mappedColumns}
        tables={tables}
        editing={editing}
        onEdit={setEditing}
        onSave={save}
        onRemove={removeFilter}
      />
    </>
  );
}
