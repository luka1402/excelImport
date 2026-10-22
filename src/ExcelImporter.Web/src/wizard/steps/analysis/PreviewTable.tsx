import { t } from '../../../messages.sr';
import { columnLetter } from './columnInfo';
import styles from './analysis.module.css';

interface Props {
  readonly rows: readonly (readonly (string | null)[])[];
  readonly headerRowIndex: number | null;
  readonly truncated: boolean;
}

export function PreviewTable({ rows, headerRowIndex, truncated }: Props) {
  const width = rows.reduce((max, row) => Math.max(max, row.length), 0);
  const letters = Array.from({ length: width }, (_, index) => columnLetter(index));

  return (
    <section aria-labelledby="analysis-preview-title">
      <h2 id="analysis-preview-title">{t('analysis.preview')}</h2>
      {truncated && <p>{t('analysis.previewTruncated')}</p>}
      <div className={styles.scroll} tabIndex={0} role="region" aria-labelledby="analysis-preview-title">
        <table className={styles.table}>
          <thead>
            <tr>
              <th scope="col">{t('analysis.rowNumber')}</th>
              {letters.map((letter) => (
                <th key={letter} scope="col">
                  {letter}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {rows.map((row, rowIndex) => (
              <tr key={rowIndex} className={rowIndex === headerRowIndex ? styles.headerRowHighlight : undefined}>
                <th scope="row">
                  {rowIndex + 1}
                  {rowIndex === headerRowIndex && <span className={styles.badge}> ({t('analysis.rowHeader')})</span>}
                </th>
                {letters.map((letter, columnIndex) => (
                  <td key={letter}>{row[columnIndex] ?? ''}</td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}
