import type { AnalyzedColumn } from '../../../api/files';
import { t, type MessageKey } from '../../../messages.sr';

/** Oznaka kolone kao u Excelu: 0 → A, 25 → Z, 26 → AA. */
export function columnLetter(index: number): string {
  let letters = '';
  let remaining = index + 1;
  while (remaining > 0) {
    const offset = (remaining - 1) % 26;
    letters = String.fromCharCode(65 + offset) + letters;
    remaining = Math.floor((remaining - 1) / 26);
  }
  return letters;
}

export function columnTypeLabel(column: AnalyzedColumn): string {
  if (column.isEmpty) {
    return t('analysis.type.empty');
  }
  if (column.isMixed) {
    return t('analysis.type.mixed');
  }
  return t(`analysis.type.${column.type}` satisfies MessageKey);
}

/** Procenat praznih ćelija u koloni, računat nad redovima podataka. */
export function emptyPercent(column: AnalyzedColumn, dataRowCount: number): string {
  if (dataRowCount <= 0) {
    return '—';
  }
  const empty = Math.max(0, dataRowCount - column.nonEmptyCount);
  return `${new Intl.NumberFormat('sr-Latn', { maximumFractionDigits: 0 }).format((empty / dataRowCount) * 100)} %`;
}

export function columnWarnings(columns: readonly AnalyzedColumn[]): readonly string[] {
  const warnings: string[] = [];
  const byName = new Map<string, number[]>();
  for (const column of columns) {
    const name = column.name.trim();
    if (name === '') {
      warnings.push(t('analysis.warn.unnamed', { letter: columnLetter(column.index) }));
      continue;
    }
    const key = name.toLocaleLowerCase('sr-Latn');
    byName.set(key, [...(byName.get(key) ?? []), column.index]);
  }
  for (const indexes of byName.values()) {
    if (indexes.length > 1) {
      const first = columns.find((column) => column.index === indexes[0]);
      warnings.push(
        t('analysis.warn.duplicate', {
          name: first?.name.trim() ?? '',
          letters: indexes.map(columnLetter).join(', '),
        }),
      );
    }
  }
  return warnings;
}
