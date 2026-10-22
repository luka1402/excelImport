import type { AnalyzedColumn } from '../../../api/files';
import { t } from '../../../messages.sr';
import { columnLetter, columnTypeLabel, emptyPercent } from './columnInfo';
import styles from './analysis.module.css';

interface Props {
  readonly columns: readonly AnalyzedColumn[];
  /** Broj redova podataka nad kojima je prepoznat tip (uzorak iz pregleda). */
  readonly sampleRowCount: number;
}

export function ColumnsTable({ columns, sampleRowCount }: Props) {
  return (
    <section aria-labelledby="analysis-columns-title">
      <h2 id="analysis-columns-title">{t('analysis.columns')}</h2>
      <div className={styles.scroll}>
        <table className={styles.table}>
          <thead>
            <tr>
              <th scope="col">{t('analysis.col.letter')}</th>
              <th scope="col">{t('analysis.col.name')}</th>
              <th scope="col">{t('analysis.col.type')}</th>
              <th scope="col">{t('analysis.col.empty')}</th>
            </tr>
          </thead>
          <tbody>
            {columns.map((column) => (
              <tr key={column.index}>
                <th scope="row">{columnLetter(column.index)}</th>
                <td>{column.name}</td>
                <td>{columnTypeLabel(column)}</td>
                <td>{emptyPercent(column, sampleRowCount)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}
