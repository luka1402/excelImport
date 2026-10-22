import { useEffect } from 'react';
import { useNavigate } from 'react-router';
import { t } from '../../../messages.sr';
import { EmptyState, ErrorState, LoadingState } from '../../../ui/states';
import { useWizard } from '../../WizardContext';
import { ColumnsTable } from './ColumnsTable';
import { HeaderRowControl } from './HeaderRowControl';
import { PreviewTable } from './PreviewTable';
import { columnWarnings } from './columnInfo';
import { useFileAnalysis } from './useFileAnalysis';
import styles from './analysis.module.css';

/** Najviši red koji korisnik može da izabere kao header; poklapa se sa veličinom pregleda. */
const maxHeaderRow = 20;

export function AnalysisStep() {
  const { data, updateData } = useWizard();
  const navigate = useNavigate();
  const { state, retry } = useFileAnalysis({
    fileId: data.fileId,
    sheetIndex: data.sheetIndex,
    headerRowIndex: data.headerRowIndex,
  });

  const analysis = state.status === 'ready' ? state.analysis : null;
  const usable = analysis !== null && analysis.columns.some((column) => !column.isEmpty);
  const selectedName = analysis?.sheets.find((sheet) => sheet.index === analysis.selectedSheetIndex)?.name ?? '';

  useEffect(() => {
    if (state.status === 'ready') {
      updateData({ sheet: selectedName, analysisUsable: usable });
    } else {
      updateData({ analysisUsable: false });
    }
  }, [state.status, selectedName, usable, updateData]);

  const chooseAnotherFile = (
    <button
      type="button"
      onClick={() => {
        void navigate('/uvoz/fajl');
      }}
    >
      {t('upload.chooseAnother')}
    </button>
  );

  if (data.fileId === '') {
    return <EmptyState message={t('nav.disabledReason.file')} action={chooseAnotherFile} />;
  }
  if (state.status === 'loading') {
    return <LoadingState message={t('analysis.loading')} />;
  }
  if (state.status === 'error') {
    return (
      <div>
        <ErrorState message={t('analysis.err')} onRetry={retry} />
        {chooseAnotherFile}
      </div>
    );
  }

  const { analysis: loaded } = state;
  const nothingToShow = loaded.sheets.length === 0;
  const warnings = columnWarnings(loaded.columns);
  const firstDataRow = loaded.headerRowIndex === null ? 0 : loaded.headerRowIndex + 1;
  const sampleRowCount = Math.max(0, loaded.previewRows.length - firstDataRow);

  return (
    <div className={styles.analysis}>
      {nothingToShow ? (
        <EmptyState message={t('analysis.fileEmpty')} action={chooseAnotherFile} />
      ) : (
        <>
          <div className={styles.controls}>
            <div className={styles.field}>
              <label htmlFor="analysis-sheet">{t('analysis.sheet')}</label>
              <select
                id="analysis-sheet"
                value={loaded.selectedSheetIndex}
                onChange={(event) => {
                  // Drugi sheet ima drugu strukturu, pa ručni header ne važi dalje.
                  updateData({ sheetIndex: Number(event.target.value), headerRowIndex: null });
                }}
              >
                {loaded.sheets.map((sheet) => (
                  <option key={sheet.index} value={sheet.index}>
                    {sheet.isHidden ? t('analysis.sheetHidden', { name: sheet.name }) : sheet.name}
                  </option>
                ))}
              </select>
            </div>
            <HeaderRowControl
              key={`${loaded.selectedSheetIndex.toString()}-${String(loaded.headerRowIndex)}-${loaded.headerChosenManually.toString()}`}
              headerRowIndex={loaded.headerRowIndex}
              manual={loaded.headerChosenManually}
              max={maxHeaderRow}
              onChange={(headerRowIndex) => {
                updateData({ headerRowIndex });
              }}
            />
          </div>

          {!usable ? (
            <EmptyState message={t('analysis.sheetEmpty')} />
          ) : (
            <>
              {warnings.length > 0 && (
                <ul className={styles.warnings}>
                  {warnings.map((warning) => (
                    <li key={warning}>⚠ {warning}</li>
                  ))}
                </ul>
              )}
              <ColumnsTable columns={loaded.columns} sampleRowCount={sampleRowCount} />
              <PreviewTable
                rows={loaded.previewRows}
                headerRowIndex={loaded.headerRowIndex}
                truncated={loaded.previewColumnsTruncated}
              />
            </>
          )}
        </>
      )}
    </div>
  );
}
