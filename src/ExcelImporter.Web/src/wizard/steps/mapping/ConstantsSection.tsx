import { t } from '../../../messages.sr';
import type { ConstantChoice, MappingChoice } from '../../types';
import type { ColumnInfo } from '../target/targetApi';
import { describeTarget } from './MappingRow';
import { emptyConstant, hasConstant, isConstantValid, isDateColumn, isRequired } from './mappingLogic';
import styles from './mapping.module.css';

interface Props {
  readonly columns: readonly ColumnInfo[];
  readonly mapping: Readonly<Record<string, MappingChoice>>;
  readonly constants: Readonly<Record<string, ConstantChoice>>;
  readonly onChange: (constants: Readonly<Record<string, ConstantChoice>>) => void;
}

export function ConstantsSection({ columns, mapping, constants, onChange }: Props) {
  const mapped = new Set(
    Object.values(mapping)
      .map((choice) => choice.target.toLowerCase())
      .filter((target) => target !== ''),
  );
  const unmapped = columns.filter((column) => !mapped.has(column.name.toLowerCase()));
  if (unmapped.length === 0) {
    return null;
  }

  const update = (name: string, patch: Partial<ConstantChoice>) => {
    onChange({ ...constants, [name]: { ...(constants[name] ?? emptyConstant), ...patch } });
  };

  return (
    <section aria-labelledby="mapping-unmapped-heading">
      <h2 id="mapping-unmapped-heading">{t('mapping.unmapped')}</h2>
      <ul>
        {unmapped.map((column) => {
          const constant = constants[column.name];
          const required = isRequired(column);
          const filled = hasConstant(constant);
          const invalid = constant !== undefined && filled && !isConstantValid(column, constant);
          return (
            <li key={column.name}>
              <strong>{describeTarget(column)}</strong>{' '}
              {required && <span>({t('mapping.required')})</span>}
              {!required && !filled && <small className={styles.note}>— {t('mapping.unmappedOptional')}</small>}
              <div>
                <input
                  type="text"
                  aria-label={t('mapping.const.value', { name: column.name })}
                  aria-invalid={invalid}
                  value={constant?.value ?? ''}
                  disabled={constant?.now === true}
                  onChange={(event) => {
                    update(column.name, { value: event.target.value, now: false });
                  }}
                />
                {isDateColumn(column) && (
                  <label>
                    <input
                      type="checkbox"
                      aria-label={t('mapping.const.nowFor', { name: column.name })}
                      checked={constant?.now ?? false}
                      onChange={(event) => {
                        update(column.name, { now: event.target.checked, value: '' });
                      }}
                    />{' '}
                    {t('mapping.const.now')}
                  </label>
                )}
              </div>
              {invalid && (
                <p role="alert" className={styles.invalid}>
                  {t('mapping.const.invalid', { type: column.dataType })}
                </p>
              )}
              {required && !filled && (
                <p className={styles.warning}>
                  <span aria-hidden="true">⚠ </span>
                  {t('mapping.warn.required', { name: column.name })}
                </p>
              )}
            </li>
          );
        })}
      </ul>
    </section>
  );
}
