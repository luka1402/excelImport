import { t } from '../../../messages.sr';
import { EmptyState } from '../../../ui/states';
import { qualifiedName, type ColumnInfo, type TableInfo } from './targetApi';
import styles from './target.module.css';

function describeKey(table: TableInfo, column: ColumnInfo): string {
  if (table.primaryKey.includes(column.name)) {
    return t('target.columns.pk');
  }
  const foreignKey = table.foreignKeys.find((fk) => fk.columns.includes(column.name));
  return foreignKey
    ? t('target.columns.fk', { table: `${foreignKey.referencedSchema}.${foreignKey.referencedTable}` })
    : t('target.columns.none');
}

function describeLength(column: ColumnInfo): string {
  return column.maxLength === null ? t('target.columns.none') : column.maxLength.toString();
}

// Identity i computed kolone popunjava baza, pa se uvoz ne oslanja na njih čak i kada su NOT NULL.
function isRequired(column: ColumnInfo): boolean {
  return !column.isNullable && !column.isIdentity && !column.isComputed;
}

export function ColumnsTable({ table }: { readonly table: TableInfo }) {
  const title = t('target.columns.title', { table: qualifiedName(table) });
  if (table.columns.length === 0) {
    return <EmptyState message={t('target.columns.empty')} />;
  }
  return (
    <table className={styles.columns}>
      <caption>{title}</caption>
      <thead>
        <tr>
          <th scope="col">{t('target.columns.name')}</th>
          <th scope="col">{t('target.columns.type')}</th>
          <th scope="col">{t('target.columns.required')}</th>
          <th scope="col">{t('target.columns.key')}</th>
          <th scope="col">{t('target.columns.length')}</th>
        </tr>
      </thead>
      <tbody>
        {table.columns.map((column) => (
          <tr key={column.name}>
            <th scope="row">{column.name}</th>
            <td>{column.dataType}</td>
            <td>
              {isRequired(column) ? t('target.columns.yes') : t('target.columns.no')}
              {(column.isIdentity || column.isComputed) && (
                <small className={styles.note}> ({t('target.columns.generated')})</small>
              )}
            </td>
            <td>{describeKey(table, column)}</td>
            <td>{describeLength(column)}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}