import type {
  ColumnRule,
  ConstantChoice,
  MappingChoice,
  RowFilterDraft,
  RuleCondition,
  WizardData,
} from '../types';
import { hasConstant, mappingKeyOf } from '../steps/mapping/mappingLogic';
import { newRuleId, operatorNeedsValue } from '../steps/rules/rulesLogic';
import type {
  ApplyTemplateResponse,
  SaveTemplateRequest,
  ServerCondition,
  ServerRule,
  TemplateSummary,
} from './templatesApi';

// Oblik pravila prati RuleSerializer na serveru (verzija 1); server odbija nepoznata polja.
const ruleVersion = 1;

function toServerCondition(condition: RuleCondition): ServerCondition {
  return operatorNeedsValue(condition.operator)
    ? { field: condition.field, operator: condition.operator, value: condition.value }
    : { field: condition.field, operator: condition.operator };
}

function toServerRule(rule: ColumnRule | RowFilterDraft): Record<string, unknown> {
  switch (rule.type) {
    case 'rowFilter':
      return { type: rule.type, version: ruleVersion, action: rule.action, conditions: rule.conditions.map(toServerCondition) };
    case 'conditionalValue':
      return {
        type: rule.type,
        version: ruleVersion,
        targetField: rule.targetField,
        conditions: rule.conditions.map(toServerCondition),
        value: rule.value,
        ...(rule.elseValue.trim() === '' ? {} : { elseValue: rule.elseValue }),
      };
    case 'defaultValue':
      return { type: rule.type, version: ruleVersion, targetField: rule.targetField, value: rule.value };
    case 'transform':
      return {
        type: rule.type,
        version: ruleVersion,
        targetField: rule.targetField,
        kind: rule.kind,
        ...(rule.kind === 'dateFormat' ? { format: rule.format } : {}),
        ...(rule.kind === 'decimalSeparator' ? { separator: rule.separator } : {}),
      };
    case 'lookup':
      return {
        type: rule.type,
        version: ruleVersion,
        targetField: rule.targetField,
        sourceField: rule.sourceField,
        schema: rule.schema,
        table: rule.table,
        keyColumn: rule.keyColumn,
        returnColumn: rule.returnColumn,
      };
  }
}

export interface TemplateSource {
  readonly name: string;
  /** Kolone fajla iz kog šablon nastaje; po njima se kasnije prepoznaje isti ili izmenjen fajl. */
  readonly sourceColumns: readonly string[];
  /** Header red kao 0-based indeks; pri automatskom prepoznavanju vrednost iz analize. */
  readonly headerRow: number;
}

/** Sadržaj čarobnjaka u obliku zahteva za čuvanje šablona. */
export function toSaveRequest(data: WizardData, source: TemplateSource): SaveTemplateRequest {
  const constants = Object.entries(data.constants)
    .filter(([, constant]) => hasConstant(constant))
    .map(([targetColumn, constant]) => ({
      targetColumn,
      value: constant.now ? { dynamic: 'Now' } : { literal: constant.value },
    }));
  return {
    name: source.name.trim(),
    connectionId: Number(data.connectionId),
    schemaName: data.tableSchema,
    tableName: data.table,
    sheetName: data.sheet === '' ? null : data.sheet,
    headerRow: source.headerRow,
    sourceColumns: source.sourceColumns,
    mappings: Object.entries(data.mapping)
      .filter(([, choice]) => choice.target !== '')
      .map(([sourceColumn, choice]) => ({ sourceColumn, targetColumn: choice.target })),
    rules: [...data.rules, ...data.rowFilters].map(toServerRule),
    constants,
  };
}

function fromServerCondition(condition: ServerCondition): RuleCondition {
  return { field: condition.field, operator: condition.operator, value: condition.value ?? '' };
}

function fromServerRule(rule: ServerRule): ColumnRule | RowFilterDraft {
  const id = newRuleId();
  switch (rule.type) {
    case 'rowFilter':
      return { id, type: rule.type, action: rule.action, conditions: rule.conditions.map(fromServerCondition) };
    case 'conditionalValue':
      return {
        id,
        type: rule.type,
        targetField: rule.targetField,
        conditions: rule.conditions.map(fromServerCondition),
        value: rule.value ?? '',
        elseValue: rule.elseValue ?? '',
      };
    case 'defaultValue':
      return { id, type: rule.type, targetField: rule.targetField, value: rule.value };
    case 'transform':
      return {
        id,
        type: rule.type,
        targetField: rule.targetField,
        kind: rule.kind,
        format: rule.format ?? '',
        separator: rule.separator === ',' || rule.separator === '.' ? rule.separator : '',
      };
    case 'lookup':
      return { ...rule, id };
  }
}

/**
 * Izmene čarobnjaka koje primena šablona daje. Ključ mapiranja se računa unapred, pa korak Mapiranje
 * zadržava primenjeno umesto da pravi novi predlog.
 */
export function fromApplyResponse(
  template: TemplateSummary,
  response: ApplyTemplateResponse,
  data: Pick<WizardData, 'fileId' | 'sheetIndex' | 'headerRowIndex'>,
): Partial<WizardData> {
  const mapping: Record<string, MappingChoice> = {};
  for (const { sourceColumn, targetColumn } of response.mappings) {
    mapping[sourceColumn] = { target: targetColumn, suggested: false };
  }
  const constants: Record<string, ConstantChoice> = {};
  for (const { targetColumn, value } of response.constants) {
    constants[targetColumn] = value.dynamic ? { value: '', now: true } : { value: value.literal ?? '', now: false };
  }
  const rules = response.rules.map(fromServerRule);

  const target = {
    connectionId: template.connectionId.toString(),
    tableSchema: template.schemaName,
    table: template.tableName,
  };
  return {
    ...target,
    mapping,
    constants,
    rules: rules.filter((rule): rule is ColumnRule => rule.type !== 'rowFilter'),
    rowFilters: rules.filter((rule): rule is RowFilterDraft => rule.type === 'rowFilter'),
    rulesEditing: false,
    // Potpunost mapiranja proverava korak Mapiranje kada se otvori.
    mappingComplete: false,
    mappingKey: mappingKeyOf({ ...data, ...target }),
    appliedTemplate: {
      id: template.id,
      name: template.name,
      newColumns: response.newColumns,
      missingColumns: response.missingColumns,
    },
  };
}
