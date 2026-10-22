import { useEffect, useMemo } from 'react';
import type { AnalyzedColumn, FileAnalysis } from '../../../api/files';
import { t } from '../../../messages.sr';
import { EmptyState, ErrorState, LoadingState } from '../../../ui/states';
import { useWizard } from '../../WizardContext';
import type { MappingChoice } from '../../types';
import { columnTypeLabel } from '../analysis/columnInfo';
import { useFileAnalysis } from '../analysis/useFileAnalysis';
import { useDatabaseSchema, type ColumnInfo, type TableInfo } from '../target/targetApi';
import { ConstantsSection } from './ConstantsSection';
import { MappingRow } from './MappingRow';
import { evaluateMapping, suggestMapping } from './mappingLogic';
import styles from './mapping.module.css';

/** Kolone sa nazivom; mapiranje ide po nazivu, pa se ponovljeni naziv uzima jednom. */
function mappableColumns(analysis: FileAnalysis): readonly AnalyzedColumn[] {
  const seen = new Set<string>();
  return analysis.columns.filter((column) => {
    if (column.name.trim() === '' || seen.has(column.name)) {
      return false;
    }
    seen.add(column.name);
    return true;
  });
}

function sampleValues(analysis: FileAnalysis, column: AnalyzedColumn): readonly (string | null)[] {
  const firstDataRow = analysis.headerRowIndex === null ? 0 : analysis.headerRowIndex + 1;
  return analysis.previewRows.slice(firstDataRow).map((row) => row[column.index] ?? null);
}

export function MappingStep() {
  const { data, updateData } = useWizard();
  const { state, retry } = useFileAnalysis({
    fileId: data.fileId,
    sheetIndex: data.sheetIndex,
    headerRowIndex: data.headerRowIndex,
  });
  const schema = useDatabaseSchema(data.connectionId);

  const analysis = state.status === 'ready' ? state.analysis : null;
  const columns = useMemo(() => (analysis ? mappableColumns(analysis) : null), [analysis]);
  const table: TableInfo | undefined = schema.data?.tables.find(
    (candidate) => candidate.schema === data.tableSchema && candidate.name === data.table,
  );
  const key = [data.fileId, data.sheetIndex, data.headerRowIndex, data.connectionId, data.tableSchema, data.table].join('|');
  const initialized = data.mappingKey === key;

  // Promena fajla, sheet-a, header-a ili tabele poništava mapiranje i pravi novi predlog.
  useEffect(() => {
    if (!columns || !table || initialized) {
      return;
    }
    const mapping: Record<string, MappingChoice> = {};
    for (const suggestion of suggestMapping(columns, table)) {
      mapping[suggestion.sourceColumn] = { target: suggestion.targetColumn, suggested: true };
    }
    updateData({ mapping, constants: {}, rules: [], rulesEditing: false, mappingKey: key });
  }, [columns, table, initialized, key, updateData]);

  const evaluation = useMemo(
    () => (table && initialized ? evaluateMapping(table, data.mapping, data.constants) : null),
    [table, initialized, data.mapping, data.constants],
  );
  const complete = evaluation?.isComplete ?? false;
  useEffect(() => {
    updateData({ mappingComplete: complete });
  }, [complete, updateData]);

  if (data.fileId === '') {
    return <EmptyState message={t('nav.disabledReason.file')} />;
  }
  if (data.table === '') {
    return <EmptyState message={t('mapping.empty.noTable')} />;
  }
  if (state.status === 'error' || schema.isError) {
    return (
      <ErrorState
        message={t('mapping.err.suggest')}
        onRetry={() => {
          retry();
          if (schema.isError) {
            void schema.refetch();
          }
        }}
      />
    );
  }
  if (!analysis || !columns || !table || !initialized || !evaluation) {
    return <LoadingState message={t('mapping.loading')} />;
  }
  if (columns.length === 0) {
    return <EmptyState message={t('mapping.empty.noColumns')} />;
  }

  const fillable = table.columns.filter((column) => !column.isIdentity && !column.isComputed);
  const columnByName = new Map<string, ColumnInfo>(fillable.map((column) => [column.name.toLowerCase(), column]));
  const suggestedCount = Object.values(data.mapping).filter((choice) => choice.suggested && choice.target !== '').length;
  const takenTargets = new Set(
    Object.values(data.mapping)
      .map((choice) => choice.target)
      .filter((target) => target !== ''),
  );

  const choose = (sourceColumn: string, target: string) => {
    updateData({ mapping: { ...data.mapping, [sourceColumn]: { target, suggested: false } } });
  };
  const acceptAll = () => {
    const confirmed = Object.fromEntries(
      Object.entries(data.mapping).map(([source, choice]) => [source, { ...choice, suggested: false }]),
    );
    updateData({ mapping: confirmed });
  };

  return (
    <div>
      {suggestedCount > 0 && (
        <div role="status" className={styles.banner}>
          <span>✔ {t('mapping.suggested', { n: suggestedCount, total: columns.length })}</span>
          <button type="button" onClick={acceptAll}>
            {t('mapping.acceptAll')}
          </button>
        </div>
      )}
      {evaluation.mappedCount === 0 && <p role="status">{t('mapping.empty.noneMapped')}</p>}
      <table className={styles.table}>
        <thead>
          <tr>
            <th scope="col">{t('mapping.col.excel')}</th>
            <th scope="col">{t('mapping.col.target')}</th>
            <th scope="col">{t('mapping.col.warning')}</th>
          </tr>
        </thead>
        <tbody>
          {columns.map((column) => {
            const choice = data.mapping[column.name];
            const target = choice ? columnByName.get(choice.target.toLowerCase()) : undefined;
            return (
              <MappingRow
                key={column.name}
                sourceName={column.name}
                typeLabel={columnTypeLabel(column)}
                column={column}
                sample={sampleValues(analysis, column)}
                targets={fillable}
                takenTargets={takenTargets}
                selected={target}
                suggested={choice?.suggested ?? false}
                onChange={(value) => {
                  choose(column.name, value);
                }}
              />
            );
          })}
        </tbody>
      </table>
      <ConstantsSection
        columns={fillable}
        mapping={data.mapping}
        constants={data.constants}
        onChange={(constants) => {
          updateData({ constants });
        }}
      />
    </div>
  );
}
