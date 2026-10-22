import { t, type MessageKey } from '../../../messages.sr';
import type { ColumnRule, ComparisonOperator, RuleCondition, TransformKind } from '../../types';
import type { ColumnInfo } from '../target/targetApi';
import { isConstantValid, isDateColumn } from '../mapping/mappingLogic';

// Pravila i poruke prate C# RuleValidators; server ponovo validira, ovo je pomoć korisniku.

export const operators: readonly ComparisonOperator[] = [
  'eq',
  'neq',
  'gt',
  'lt',
  'gte',
  'lte',
  'contains',
  'startsWith',
  'isEmpty',
  'isNotEmpty',
];

export const transformKinds: readonly TransformKind[] = ['trim', 'upper', 'lower', 'dateFormat', 'decimalSeparator'];

const operatorKeys: Record<ComparisonOperator, MessageKey> = {
  eq: 'rules.op.eq',
  neq: 'rules.op.neq',
  gt: 'rules.op.gt',
  lt: 'rules.op.lt',
  gte: 'rules.op.gte',
  lte: 'rules.op.lte',
  contains: 'rules.op.contains',
  startsWith: 'rules.op.startsWith',
  isEmpty: 'rules.op.isEmpty',
  isNotEmpty: 'rules.op.isNotEmpty',
};

const transformKeys: Record<TransformKind, MessageKey> = {
  trim: 'rules.transform.trim',
  upper: 'rules.transform.upper',
  lower: 'rules.transform.lower',
  dateFormat: 'rules.transform.dateFormat',
  decimalSeparator: 'rules.transform.decimal',
};

export const operatorLabel = (operator: ComparisonOperator): string => t(operatorKeys[operator]);
export const transformLabel = (kind: TransformKind): string => t(transformKeys[kind]);

/** Operatori „je prazno / nije prazno" ne koriste vrednost za poređenje. */
export function operatorNeedsValue(operator: ComparisonOperator): boolean {
  return operator !== 'isEmpty' && operator !== 'isNotEmpty';
}

const textOperators = new Set<ComparisonOperator>(['contains', 'startsWith']);
const integerTypes = new Set(['tinyint', 'smallint', 'int', 'bigint']);
const decimalTypes = new Set(['decimal', 'numeric', 'money', 'smallmoney', 'float', 'real']);

export function emptyCondition(field: string): RuleCondition {
  return { field, operator: 'eq', value: '' };
}

export function newRuleId(): string {
  return crypto.randomUUID();
}

/** Greška vrednosti koja se upisuje u ciljnu kolonu; null kada je vrednost ispravna. */
export function targetValueError(column: ColumnInfo | undefined, value: string): string | null {
  if (value.trim() === '') {
    return t('rules.val.value');
  }
  return column ? valueTypeError(column, value) : null;
}

function valueTypeError(column: ColumnInfo, value: string): string | null {
  if (isConstantValid(column, { value, now: false })) {
    return null;
  }
  const dataType = column.dataType.toLowerCase();
  if (integerTypes.has(dataType) || decimalTypes.has(dataType)) {
    return t('rules.val.number');
  }
  return isDateColumn(column) ? t('rules.val.date') : t('rules.val.type', { type: column.dataType });
}

function conditionErrors(condition: RuleCondition, columns: ReadonlyMap<string, ColumnInfo>): ConditionErrors {
  if (condition.field === '') {
    return { field: t('rules.val.column') };
  }
  if (!operatorNeedsValue(condition.operator)) {
    return {};
  }
  if (condition.value.trim() === '') {
    return { value: t('rules.val.value') };
  }
  const column = columns.get(condition.field);
  // Poređenje po tekstu (sadrži, počinje sa) važi za svaki tip; ostalo se poredi po tipu kolone.
  if (column && !textOperators.has(condition.operator)) {
    const error = valueTypeError(column, condition.value);
    return error === null ? {} : { value: error };
  }
  return {};
}

/** Dozvoljeni su tokeni .NET formata datuma i separatori; mora postojati bar dan, mesec ili godina. */
export function isDateFormatValid(format: string): boolean {
  const trimmed = format.trim();
  return /^[dMyHhmsfzt\s.\-/:,]+$/.test(trimmed) && /[dMy]/.test(trimmed);
}

export interface ConditionErrors {
  readonly field?: string;
  readonly value?: string;
}

export interface RuleErrors {
  readonly conditions: readonly ConditionErrors[];
  readonly value?: string;
  readonly elseValue?: string;
  readonly format?: string;
  readonly separator?: string;
  readonly hasErrors: boolean;
}

export function validateRule(rule: ColumnRule, columns: ReadonlyMap<string, ColumnInfo>): RuleErrors {
  const target = columns.get(rule.targetField);
  let conditions: readonly ConditionErrors[] = [];
  let value: string | undefined;
  let elseValue: string | undefined;
  let format: string | undefined;
  let separator: string | undefined;

  switch (rule.type) {
    case 'conditionalValue':
      conditions = rule.conditions.map((condition) => conditionErrors(condition, columns));
      value = targetValueError(target, rule.value) ?? undefined;
      // Vrednost „u suprotnom" je opciona, pa se proverava samo kada je uneta.
      if (rule.elseValue.trim() !== '' && target) {
        elseValue = valueTypeError(target, rule.elseValue) ?? undefined;
      }
      break;
    case 'defaultValue':
      value = targetValueError(target, rule.value) ?? undefined;
      break;
    case 'transform':
      if (rule.kind === 'dateFormat' && !isDateFormatValid(rule.format)) {
        format = t('rules.val.dateFormat');
      }
      if (rule.kind === 'decimalSeparator' && rule.separator === '') {
        separator = t('rules.val.separator');
      }
      break;
  }

  const hasErrors =
    conditions.some((error) => error.field !== undefined || error.value !== undefined) ||
    [value, elseValue, format, separator].some((error) => error !== undefined);
  return { conditions, value, elseValue, format, separator, hasErrors };
}

/** Jedna rečenica koja opisuje pravilo u listi pravila kolone. */
export function describeRule(rule: ColumnRule): string {
  switch (rule.type) {
    case 'conditionalValue': {
      const conditions = rule.conditions
        .map((condition) =>
          [condition.field, operatorLabel(condition.operator), operatorNeedsValue(condition.operator) ? `„${condition.value}"` : '']
            .filter((part) => part !== '')
            .join(' '),
        )
        .join(t('rules.summary.and'));
      return rule.elseValue.trim() === ''
        ? t('rules.summary.conditional', { conditions, value: rule.value })
        : t('rules.summary.conditionalElse', { conditions, value: rule.value, elseValue: rule.elseValue });
    }
    case 'defaultValue':
      return t('rules.summary.default', { value: rule.value });
    case 'transform': {
      const kind = transformLabel(rule.kind);
      if (rule.kind === 'dateFormat') {
        return t('rules.summary.transformFormat', { kind, detail: rule.format });
      }
      if (rule.kind === 'decimalSeparator') {
        return t('rules.summary.transformFormat', { kind, detail: rule.separator });
      }
      return t('rules.summary.transform', { kind });
    }
  }
}
