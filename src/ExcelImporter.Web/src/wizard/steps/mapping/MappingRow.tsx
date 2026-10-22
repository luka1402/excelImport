import { useId } from 'react';
import type { AnalyzedColumn } from '../../../api/files';
import { t } from '../../../messages.sr';
import { columnTypeLabel } from '../analysis/columnInfo';
import type { ColumnInfo } from '../target/targetApi';
import { isTypeCompatible, textOverflowMax } from './mappingLogic';
import styles from './mapping.module.css';

interface Props {
  readonly sourceName: string;
  readonly typeLabel: string;
  readonly column: AnalyzedColumn;
  /** Vrednosti iz pregleda fajla, za proveru dužine teksta. */
  readonly sample: readonly (string | null)[];
  readonly targets: readonly ColumnInfo[];
  readonly takenTargets: ReadonlySet<string>;
  readonly selected: ColumnInfo | undefined;
  readonly suggested: boolean;
  readonly onChange: (target: string) => void;
}

export function describeTarget(column: ColumnInfo): string {
  const length = column.maxLength !== null && column.maxLength >= 0 ? ` ${column.maxLength.toString()}` : '';
  return `${column.name} (${column.dataType}${length})`;
}

function rowWarnings(column: AnalyzedColumn, sample: readonly (string | null)[], target: ColumnInfo | undefined): string[] {
  if (!target) {
    return [];
  }
  const warnings: string[] = [];
  if (!isTypeCompatible(column, target)) {
    warnings.push(t('mapping.warn.type', { from: columnTypeLabel(column), to: target.dataType }));
  }
  const overflow = textOverflowMax(target, sample);
  if (overflow !== null) {
    warnings.push(t('mapping.warn.length', { max: overflow }));
  }
  return warnings;
}

export function MappingRow({ sourceName, typeLabel, column, sample, targets, takenTargets, selected, suggested, onChange }: Props) {
  const warningId = useId();
  const warnings = rowWarnings(column, sample, selected);
  return (
    <tr>
      <th scope="row">
        {sourceName} <small className={styles.note}>({typeLabel})</small>
        {suggested && <span className={styles.badge}>{t('mapping.suggestion')}</span>}
      </th>
      <td>
        <select
          aria-label={t('mapping.targetFor', { name: sourceName })}
          aria-describedby={warnings.length > 0 ? warningId : undefined}
          value={selected?.name ?? ''}
          onChange={(event) => {
            onChange(event.target.value);
          }}
        >
          <option value="">{t('mapping.skip')}</option>
          {targets.map((target) => {
            const takenByOther = takenTargets.has(target.name) && selected?.name !== target.name;
            return (
              <option key={target.name} value={target.name} disabled={takenByOther}>
                {describeTarget(target)}
                {takenByOther ? ` — ${t('mapping.alreadyMapped')}` : ''}
              </option>
            );
          })}
        </select>
      </td>
      <td id={warningId}>
        {warnings.length === 0 ? (
          '—'
        ) : (
          <ul>
            {warnings.map((warning) => (
              <li key={warning} className={styles.warning}>
                <span aria-hidden="true">⚠ </span>
                {warning}
              </li>
            ))}
          </ul>
        )}
      </td>
    </tr>
  );
}
