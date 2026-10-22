import { useState } from 'react';
import { t } from '../../../messages.sr';
import type { RowFilterDraft, RuleDraft } from '../../types';
import type { ColumnInfo, TableInfo } from '../target/targetApi';
import { RuleForm } from './RuleForm';
import { describeRule, emptyCondition, newRuleId } from './rulesLogic';
import styles from './rules.module.css';

interface Props {
  readonly filters: readonly RowFilterDraft[];
  readonly columns: ReadonlyMap<string, ColumnInfo>;
  readonly tables: readonly TableInfo[];
  /** Pravilo (ili filter) koje se trenutno uređuje; samo jedna forma može biti otvorena. */
  readonly editing: RuleDraft | null;
  readonly onEdit: (filter: RowFilterDraft | null) => void;
  readonly onSave: (filter: RowFilterDraft) => void;
  readonly onRemove: (id: string) => void;
}

function newFilter(firstColumn: string): RowFilterDraft {
  return { id: newRuleId(), type: 'rowFilter', action: 'include', conditions: [emptyCondition(firstColumn)] };
}

export function RowFiltersSection({ filters, columns, tables, editing, onEdit, onSave, onRemove }: Props) {
  const [deletingId, setDeletingId] = useState<string | null>(null);
  const isEditing = editing !== null;
  const editingFilter = editing?.type === 'rowFilter' ? editing : null;
  const form = (filter: RowFilterDraft) => (
    <RuleForm
      rule={filter}
      columns={columns}
      tables={tables}
      onSave={(saved) => {
        if (saved.type === 'rowFilter') {
          onSave(saved);
        }
      }}
      onCancel={() => {
        onEdit(null);
      }}
    />
  );

  return (
    <section aria-labelledby="row-filters-heading">
      <h2 id="row-filters-heading">{t('filters.heading')}</h2>
      <p className={styles.note}>{t('filters.intro')}</p>
      {filters.length === 0 && editingFilter === null && <p role="status">{t('filters.empty')}</p>}
      {filters.length > 0 && (
        <ol className={styles.ruleList} aria-label={t('filters.list')}>
          {filters.map((filter) => (
            <li key={filter.id}>
              {editingFilter?.id === filter.id ? (
                form(editingFilter)
              ) : (
                <>
                  <span>{describeRule(filter)}</span>
                  {deletingId === filter.id ? (
                    <span role="alertdialog" aria-label={t('filters.confirmDelete')}>
                      {t('filters.confirmDelete')}{' '}
                      <button type="button" onClick={() => { setDeletingId(null); }}>
                        {t('rules.cancel')}
                      </button>
                      <button
                        type="button"
                        onClick={() => {
                          onRemove(filter.id);
                          setDeletingId(null);
                        }}
                      >
                        {t('rules.confirmDeleteYes')}
                      </button>
                    </span>
                  ) : (
                    <span className={styles.actions}>
                      <button type="button" disabled={isEditing} onClick={() => { onEdit(filter); }}>
                        {t('rules.edit')}
                      </button>
                      <button
                        type="button"
                        aria-label={`${t('filters.remove')}: ${describeRule(filter)}`}
                        disabled={isEditing}
                        onClick={() => { setDeletingId(filter.id); }}
                      >
                        ✕
                      </button>
                    </span>
                  )}
                </>
              )}
            </li>
          ))}
        </ol>
      )}
      {editingFilter !== null && !filters.some((filter) => filter.id === editingFilter.id) && form(editingFilter)}
      {!isEditing && (
        <div className={styles.addRow}>
          <button
            type="button"
            onClick={() => {
              onEdit(newFilter([...columns.keys()][0] ?? ''));
            }}
          >
            + {t('filters.add')}
          </button>
        </div>
      )}
    </section>
  );
}
