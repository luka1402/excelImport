import { useId, useState, type SyntheticEvent } from 'react';
import { t } from '../../../messages.sr';
import type {
  ColumnRule,
  ComparisonOperator,
  ConditionalValueDraft,
  DefaultValueDraft,
  RuleCondition,
  TransformDraft,
  TransformKind,
} from '../../types';
import type { ColumnInfo } from '../target/targetApi';
import {
  emptyCondition,
  operatorLabel,
  operatorNeedsValue,
  operators,
  transformKinds,
  transformLabel,
  validateRule,
  type RuleErrors,
} from './rulesLogic';
import styles from './rules.module.css';

interface Props {
  readonly rule: ColumnRule;
  /** Ciljne kolone po nazivu; koriste se za proveru tipa i za izbor kolone u uslovu. */
  readonly columns: ReadonlyMap<string, ColumnInfo>;
  readonly onSave: (rule: ColumnRule) => void;
  readonly onCancel: () => void;
}

interface FieldErrorProps {
  readonly id: string;
  readonly message: string | undefined;
}

function FieldError({ id, message }: FieldErrorProps) {
  return message === undefined ? null : (
    <p id={id} role="alert" className={styles.invalid}>
      {message}
    </p>
  );
}

interface TextFieldProps {
  readonly label: string;
  readonly value: string;
  readonly error: string | undefined;
  readonly hint?: string;
  readonly onChange: (value: string) => void;
}

function TextField({ label, value, error, hint, onChange }: TextFieldProps) {
  const errorId = useId();
  return (
    <div className={styles.field}>
      <label>
        {label}
        <input
          type="text"
          value={value}
          aria-invalid={error !== undefined}
          aria-describedby={error === undefined ? undefined : errorId}
          onChange={(event) => {
            onChange(event.target.value);
          }}
        />
      </label>
      {hint && <small className={styles.note}>{hint}</small>}
      <FieldError id={errorId} message={error} />
    </div>
  );
}

interface ConditionRowProps {
  readonly index: number;
  readonly condition: RuleCondition;
  readonly fields: readonly string[];
  readonly errors: RuleErrors['conditions'][number] | undefined;
  readonly removable: boolean;
  readonly onChange: (condition: RuleCondition) => void;
  readonly onRemove: () => void;
}

function ConditionRow({ index, condition, fields, errors, removable, onChange, onRemove }: ConditionRowProps) {
  const fieldErrorId = useId();
  const number = index + 1;
  return (
    <div className={styles.conditionRow}>
      <span>{index === 0 ? t('rules.if') : t('rules.addCondition')}</span>
      <div className={styles.field}>
        <select
          aria-label={t('rules.conditionColumn', { n: number })}
          aria-invalid={errors?.field !== undefined}
          aria-describedby={errors?.field === undefined ? undefined : fieldErrorId}
          value={condition.field}
          onChange={(event) => {
            onChange({ ...condition, field: event.target.value });
          }}
        >
          <option value="">—</option>
          {fields.map((field) => (
            <option key={field} value={field}>
              {field}
            </option>
          ))}
        </select>
        <FieldError id={fieldErrorId} message={errors?.field} />
      </div>
      <select
        aria-label={t('rules.conditionOperator', { n: number })}
        value={condition.operator}
        onChange={(event) => {
          const operator = event.target.value as ComparisonOperator;
          onChange({ ...condition, operator, value: operatorNeedsValue(operator) ? condition.value : '' });
        }}
      >
        {operators.map((operator) => (
          <option key={operator} value={operator}>
            {operatorLabel(operator)}
          </option>
        ))}
      </select>
      {operatorNeedsValue(condition.operator) && (
        <TextField
          label={t('rules.conditionValue', { n: number })}
          value={condition.value}
          error={errors?.value}
          onChange={(value) => {
            onChange({ ...condition, value });
          }}
        />
      )}
      {removable && (
        <button type="button" onClick={onRemove}>
          {t('rules.removeCondition', { n: number })}
        </button>
      )}
    </div>
  );
}

interface SubFormProps<TRule extends ColumnRule> {
  readonly rule: TRule;
  readonly errors: RuleErrors | null;
  readonly fields: readonly string[];
  readonly onChange: (rule: TRule) => void;
}

function ConditionalFields({ rule, errors, fields, onChange }: SubFormProps<ConditionalValueDraft>) {
  const setCondition = (index: number, condition: RuleCondition) => {
    onChange({ ...rule, conditions: rule.conditions.map((current, i) => (i === index ? condition : current)) });
  };
  return (
    <>
      {rule.conditions.map((condition, index) => (
        <ConditionRow
          // Uslovi nemaju identifikator; redosled je jedini ključ, a brisanje ponovo montira redove.
          key={index}
          index={index}
          condition={condition}
          fields={fields}
          errors={errors?.conditions[index]}
          removable={rule.conditions.length > 1}
          onChange={(updated) => {
            setCondition(index, updated);
          }}
          onRemove={() => {
            onChange({ ...rule, conditions: rule.conditions.filter((_, i) => i !== index) });
          }}
        />
      ))}
      <button
        type="button"
        onClick={() => {
          onChange({ ...rule, conditions: [...rule.conditions, emptyCondition(fields[0] ?? '')] });
        }}
      >
        + {t('rules.addCondition')}
      </button>
      <TextField
        label={t('rules.thenValue')}
        value={rule.value}
        error={errors?.value}
        onChange={(value) => {
          onChange({ ...rule, value });
        }}
      />
      <TextField
        label={`${t('rules.elseValue')} ${t('rules.optional')}`}
        value={rule.elseValue}
        error={errors?.elseValue}
        onChange={(elseValue) => {
          onChange({ ...rule, elseValue });
        }}
      />
    </>
  );
}

function DefaultFields({ rule, errors, onChange }: SubFormProps<DefaultValueDraft>) {
  return (
    <TextField
      label={t('rules.default.ifEmpty')}
      value={rule.value}
      error={errors?.value}
      onChange={(value) => {
        onChange({ ...rule, value });
      }}
    />
  );
}

function TransformFields({ rule, errors, onChange }: SubFormProps<TransformDraft>) {
  const separatorErrorId = useId();
  return (
    <>
      <div className={styles.field}>
        <label>
          {t('rules.transform.kind')}
          <select
            value={rule.kind}
            onChange={(event) => {
              onChange({ ...rule, kind: event.target.value as TransformKind });
            }}
          >
            {transformKinds.map((kind) => (
              <option key={kind} value={kind}>
                {transformLabel(kind)}
              </option>
            ))}
          </select>
        </label>
      </div>
      {rule.kind === 'dateFormat' && (
        <TextField
          label={t('rules.transform.format')}
          hint={t('rules.transform.formatHint')}
          value={rule.format}
          error={errors?.format}
          onChange={(format) => {
            onChange({ ...rule, format });
          }}
        />
      )}
      {rule.kind === 'decimalSeparator' && (
        <fieldset aria-describedby={errors?.separator === undefined ? undefined : separatorErrorId}>
          <legend>{t('rules.decimalSeparator')}</legend>
          {([',', '.'] as const).map((separator) => (
            <label key={separator}>
              <input
                type="radio"
                name="decimal-separator"
                checked={rule.separator === separator}
                onChange={() => {
                  onChange({ ...rule, separator });
                }}
              />{' '}
              {separator === ',' ? t('rules.separator.comma') : t('rules.separator.dot')}
            </label>
          ))}
          <FieldError id={separatorErrorId} message={errors?.separator} />
        </fieldset>
      )}
    </>
  );
}

export function RuleForm({ rule: initial, columns, onSave, onCancel }: Props) {
  const [rule, setRule] = useState<ColumnRule>(initial);
  const [submitted, setSubmitted] = useState(false);
  const errors = submitted ? validateRule(rule, columns) : null;
  const fields = [...columns.keys()];

  const submit = (event: SyntheticEvent) => {
    event.preventDefault();
    setSubmitted(true);
    if (!validateRule(rule, columns).hasErrors) {
      onSave(rule);
    }
  };

  return (
    <form noValidate onSubmit={submit} className={styles.form}>
      {rule.type === 'conditionalValue' && (
        <ConditionalFields rule={rule} errors={errors} fields={fields} onChange={setRule} />
      )}
      {rule.type === 'defaultValue' && <DefaultFields rule={rule} errors={errors} fields={fields} onChange={setRule} />}
      {rule.type === 'transform' && <TransformFields rule={rule} errors={errors} fields={fields} onChange={setRule} />}
      <div className={styles.actions}>
        <button type="button" onClick={onCancel}>
          {t('rules.cancel')}
        </button>
        <button type="submit">{t('rules.save')}</button>
      </div>
    </form>
  );
}
