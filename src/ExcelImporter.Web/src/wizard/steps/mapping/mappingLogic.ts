import type { AnalyzedColumn } from '../../../api/files';
import type { ColumnInfo, TableInfo } from '../target/targetApi';

// Predlog i upozorenja prate C# klase MappingSuggester, ColumnNameNormalizer i MappingWarnings.
// Server ponovo validira mapiranje pri uvozu (MappingValidator), pa je ovo samo pomoć korisniku.

const similarityThreshold = 0.8;

const textTypes = new Set(['char', 'varchar', 'nchar', 'nvarchar', 'text', 'ntext']);
const integerTypes = new Set(['tinyint', 'smallint', 'int', 'bigint']);
const decimalTypes = new Set(['decimal', 'numeric', 'money', 'smallmoney', 'float', 'real']);
const dateTypes = new Set(['date', 'datetime', 'datetime2', 'smalldatetime']);

/** Ciljna kolona koja se puni iz fajla ili konstante; identity i computed puni baza. */
export function isFillable(column: ColumnInfo): boolean {
  return !column.isIdentity && !column.isComputed;
}

export function isRequired(column: ColumnInfo): boolean {
  return !column.isNullable && isFillable(column);
}

export function isDateColumn(column: ColumnInfo): boolean {
  return dateTypes.has(column.dataType.toLowerCase());
}

/** Bez velikih/malih slova, razmaka, interpunkcije i dijakritika; ostaju slova i cifre. */
export function normalizeColumnName(name: string): string {
  return name
    .normalize('NFD')
    .replace(/\p{M}/gu, '')
    .replace(/[đĐ]/g, 'd')
    .replace(/[^\p{L}\p{N}]/gu, '')
    .toLowerCase();
}

function levenshtein(left: string, right: string): number {
  let previous = Array.from({ length: right.length + 1 }, (_, index) => index);
  for (let i = 1; i <= left.length; i++) {
    const current = [i];
    for (let j = 1; j <= right.length; j++) {
      const substitution = (previous[j - 1] ?? 0) + (left[i - 1] === right[j - 1] ? 0 : 1);
      current.push(Math.min(substitution, (previous[j] ?? 0) + 1, (current[j - 1] ?? 0) + 1));
    }
    previous = current;
  }
  return previous[right.length] ?? 0;
}

function digitsOf(value: string): string {
  return value.replace(/\D/g, '');
}

function similarity(left: string, right: string): number {
  if (left === '' || right === '') {
    return 0;
  }
  if (left === right) {
    return 1;
  }
  // Nazivi koji se razlikuju po ciframa (adresa1, adresa2) su obično različite kolone.
  if (digitsOf(left) !== digitsOf(right)) {
    return 0;
  }
  return 1 - levenshtein(left, right) / Math.max(left.length, right.length);
}

export interface Suggestion {
  readonly sourceColumn: string;
  readonly targetColumn: string;
}

/** Najbolji parovi prvi; svaka kolona fajla i svaka ciljna kolona učestvuje u najviše jednom predlogu. */
export function suggestMapping(sources: readonly AnalyzedColumn[], table: TableInfo): readonly Suggestion[] {
  const targets = table.columns.filter(isFillable).map((column) => ({ name: column.name, key: normalizeColumnName(column.name) }));
  const candidates = sources
    .map((source) => ({ name: source.name, key: normalizeColumnName(source.name) }))
    .flatMap((source) => targets.map((target) => ({ source: source.name, target: target.name, score: similarity(source.key, target.key) })))
    .filter((candidate) => candidate.score >= similarityThreshold)
    // Array.sort je stabilan, pa pri istoj oceni važi redosled kolona u fajlu.
    .sort((a, b) => b.score - a.score);

  const usedSources = new Set<string>();
  const usedTargets = new Set<string>();
  const suggestions: Suggestion[] = [];
  for (const candidate of candidates) {
    const targetKey = candidate.target.toLowerCase();
    if (usedSources.has(candidate.source) || usedTargets.has(targetKey)) {
      continue;
    }
    usedSources.add(candidate.source);
    usedTargets.add(targetKey);
    suggestions.push({ sourceColumn: candidate.source, targetColumn: candidate.target });
  }
  return suggestions;
}

export function isTypeCompatible(source: AnalyzedColumn, target: ColumnInfo): boolean {
  if (source.isEmpty) {
    return true;
  }
  const dataType = target.dataType.toLowerCase();
  if (textTypes.has(dataType)) {
    return true;
  }
  if (source.isMixed || source.type === 'Text') {
    return dataType === 'uniqueidentifier';
  }
  switch (source.type) {
    case 'WholeNumber':
      return integerTypes.has(dataType) || decimalTypes.has(dataType);
    case 'DecimalNumber':
      return decimalTypes.has(dataType);
    case 'Date':
      return dateTypes.has(dataType);
    case 'Boolean':
      return dataType === 'bit';
  }
}

/** Najduža vrednost iz uzorka koja ne staje u tekstualnu ciljnu kolonu; null ako nema prekoračenja. */
export function textOverflowMax(target: ColumnInfo, sampleValues: readonly (string | null)[]): number | null {
  const max = target.maxLength;
  if (max === null || max < 0 || !textTypes.has(target.dataType.toLowerCase())) {
    return null;
  }
  return sampleValues.some((value) => value !== null && value.length > max) ? max : null;
}

export interface ConstantInput {
  readonly value: string;
  readonly now: boolean;
}

export const emptyConstant: ConstantInput = { value: '', now: false };

export function hasConstant(constant: ConstantInput | undefined): boolean {
  return constant !== undefined && (constant.now || constant.value.trim() !== '');
}

export interface MappingEvaluation {
  readonly mappedCount: number;
  /** Nemapirane obavezne kolone bez konstante; blokiraju nastavak. */
  readonly missingRequired: readonly string[];
  /** Nemapirane kolone čija konstanta ne odgovara tipu; blokiraju nastavak. */
  readonly invalidConstants: readonly string[];
  readonly isComplete: boolean;
}

export function evaluateMapping(
  table: TableInfo,
  mapping: Readonly<Record<string, { readonly target: string }>>,
  constants: Readonly<Record<string, ConstantInput>>,
): MappingEvaluation {
  const mappedTargets = new Set(
    Object.values(mapping)
      .map((choice) => choice.target.toLowerCase())
      .filter((target) => target !== ''),
  );
  const unmapped = table.columns.filter((column) => isFillable(column) && !mappedTargets.has(column.name.toLowerCase()));
  const missingRequired = unmapped.filter((column) => isRequired(column) && !hasConstant(constants[column.name])).map((column) => column.name);
  const invalidConstants = unmapped
    .filter((column) => {
      const constant = constants[column.name];
      return constant !== undefined && hasConstant(constant) && !isConstantValid(column, constant);
    })
    .map((column) => column.name);
  return {
    mappedCount: mappedTargets.size,
    missingRequired,
    invalidConstants,
    isComplete: mappedTargets.size > 0 && missingRequired.length === 0 && invalidConstants.length === 0,
  };
}

/** Da li literal odgovara tipu ciljne kolone; prazna vrednost se ne proverava. */
export function isConstantValid(column: ColumnInfo, constant: ConstantInput): boolean {
  if (constant.now) {
    return isDateColumn(column);
  }
  const value = constant.value.trim();
  if (value === '') {
    return true;
  }
  const dataType = column.dataType.toLowerCase();
  if (textTypes.has(dataType)) {
    return column.maxLength === null || column.maxLength < 0 || constant.value.length <= column.maxLength;
  }
  if (integerTypes.has(dataType)) {
    return /^[+-]?\d+$/.test(value);
  }
  if (decimalTypes.has(dataType)) {
    return /^[+-]?\d+([.,]\d+)?$/.test(value);
  }
  if (dateTypes.has(dataType)) {
    return !Number.isNaN(Date.parse(value));
  }
  if (dataType === 'bit') {
    return /^(true|false|0|1|da|ne)$/i.test(value);
  }
  if (dataType === 'uniqueidentifier') {
    return /^[0-9a-f]{8}-([0-9a-f]{4}-){3}[0-9a-f]{12}$/i.test(value);
  }
  return true;
}
