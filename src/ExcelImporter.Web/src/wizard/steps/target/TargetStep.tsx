import { useId, useState } from 'react';
import { ApiError } from '../../../api/apiError';
import { t } from '../../../messages.sr';
import { EmptyState, ErrorState, LoadingState } from '../../../ui/states';
import { useWizard } from '../../WizardContext';
import { ColumnsTable } from './ColumnsTable';
import { qualifiedName, useConnections, useDatabaseSchema, type TableInfo } from './targetApi';
import styles from './target.module.css';

function ErrorDetails({ error }: { readonly error: Error }) {
  if (!(error instanceof ApiError)) {
    return null;
  }
  return (
    <ul>
      <li>
        {t('error.status')}: {error.status}
      </li>
      {error.traceId && (
        <li>
          {t('error.traceId')}: {error.traceId}
        </li>
      )}
    </ul>
  );
}

// Mrežna greška i 5xx znače da baza nije dostupna; ostalo je problem sa čitanjem šeme.
function schemaErrorMessage(error: Error): string {
  const unavailable = error instanceof ApiError && (error.status === 0 || error.status >= 500);
  return unavailable ? t('target.table.unavailable') : t('target.table.error');
}

export function TargetStep() {
  const { data, updateData } = useWizard();
  const connections = useConnections();
  const schema = useDatabaseSchema(data.connectionId);
  const [search, setSearch] = useState('');
  const ids = { connection: useId(), search: useId(), table: useId() };

  const selectConnection = (connectionId: string) => {
    // Tabela druge baze ne važi, pa se izbor tabele poništava.
    updateData({ connectionId, tableSchema: '', table: '' });
    setSearch('');
  };
  const selectTable = (tables: readonly TableInfo[], key: string) => {
    const table = tables.find((candidate) => qualifiedName(candidate) === key);
    updateData({ tableSchema: table?.schema ?? '', table: table?.name ?? '' });
  };

  if (connections.isPending) {
    return <LoadingState message={t('target.connection.loading')} />;
  }
  if (connections.isError) {
    return (
      <ErrorState
        message={t('target.connection.error')}
        onRetry={() => void connections.refetch()}
        details={<ErrorDetails error={connections.error} />}
      />
    );
  }
  if (connections.data.length === 0) {
    return <EmptyState message={t('target.connection.empty')} />;
  }

  const tables = schema.data?.tables ?? [];
  const query = search.trim().toLowerCase();
  const visibleTables = tables.filter((table) => qualifiedName(table).toLowerCase().includes(query));
  const selectedKey = data.table === '' ? '' : qualifiedName({ schema: data.tableSchema, name: data.table });
  const selectedTable = tables.find((table) => qualifiedName(table) === selectedKey);

  return (
    <div>
      <div className={styles.fields}>
        <label htmlFor={ids.connection}>{t('target.connection.label')}</label>
        <select
          id={ids.connection}
          value={data.connectionId}
          onChange={(event) => {
            selectConnection(event.target.value);
          }}
        >
          <option value="">{t('target.connection.placeholder')}</option>
          {connections.data.map((connection) => (
            <option key={connection.id} value={connection.id.toString()}>
              {connection.displayName}
            </option>
          ))}
        </select>

        {data.connectionId !== '' && schema.isSuccess && tables.length > 0 && (
          <>
            <label htmlFor={ids.search}>{t('target.table.search')}</label>
            <input
              id={ids.search}
              type="search"
              value={search}
              onChange={(event) => {
                setSearch(event.target.value);
              }}
            />
            <label htmlFor={ids.table}>{t('target.table.label')}</label>
            <select
              id={ids.table}
              value={selectedKey}
              onChange={(event) => {
                selectTable(tables, event.target.value);
              }}
            >
              <option value="">{t('target.table.placeholder')}</option>
              {visibleTables.map((table) => (
                <option key={qualifiedName(table)} value={qualifiedName(table)}>
                  {qualifiedName(table)}
                </option>
              ))}
            </select>
          </>
        )}
      </div>

      {data.connectionId !== '' && schema.isPending && <LoadingState message={t('target.table.loading')} />}
      {data.connectionId !== '' && schema.isError && (
        <ErrorState
          message={schemaErrorMessage(schema.error)}
          onRetry={() => void schema.refetch()}
          details={<ErrorDetails error={schema.error} />}
        />
      )}
      {schema.isSuccess && tables.length === 0 && <EmptyState message={t('target.table.empty')} />}
      {schema.isSuccess && tables.length > 0 && visibleTables.length === 0 && (
        <p role="status">{t('target.table.noMatch', { query: search.trim() })}</p>
      )}
      {selectedTable && <ColumnsTable table={selectedTable} />}
    </div>
  );
}