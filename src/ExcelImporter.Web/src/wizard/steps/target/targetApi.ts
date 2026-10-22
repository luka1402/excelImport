import { useQuery } from '@tanstack/react-query';
import { z } from 'zod';
import { apiRequest } from '../../../api/client';

// Ugovor prati C# modele iz Application/Schema (camelCase JSON). Klijent nikad ne dobija connection string.
const connectionSchema = z.object({
  id: z.number().int(),
  displayName: z.string(),
});

const columnSchema = z.object({
  name: z.string(),
  ordinal: z.number().int(),
  dataType: z.string(),
  isNullable: z.boolean(),
  maxLength: z.number().int().nullable(),
  isIdentity: z.boolean(),
  isComputed: z.boolean(),
});

const foreignKeySchema = z.object({
  name: z.string(),
  columns: z.array(z.string()),
  referencedSchema: z.string(),
  referencedTable: z.string(),
  referencedColumns: z.array(z.string()),
});

const tableSchema = z.object({
  schema: z.string(),
  name: z.string(),
  columns: z.array(columnSchema),
  primaryKey: z.array(z.string()),
  foreignKeys: z.array(foreignKeySchema),
});

const databaseSchemaResponse = z.object({ tables: z.array(tableSchema) });

export type ConnectionItem = z.infer<typeof connectionSchema>;
export type TableInfo = z.infer<typeof tableSchema>;
export type ColumnInfo = z.infer<typeof columnSchema>;
export type ForeignKeyInfo = z.infer<typeof foreignKeySchema>;

export function qualifiedName(table: Pick<TableInfo, 'schema' | 'name'>): string {
  return `${table.schema}.${table.name}`;
}

export function useConnections() {
  return useQuery({
    queryKey: ['connections'],
    queryFn: ({ signal }) => apiRequest('/api/connections', { signal, schema: z.array(connectionSchema) }),
    retry: false,
  });
}

export function useDatabaseSchema(connectionId: string) {
  return useQuery({
    queryKey: ['connections', connectionId, 'schema'],
    queryFn: ({ signal }) =>
      apiRequest(`/api/connections/${encodeURIComponent(connectionId)}/schema`, {
        signal,
        schema: databaseSchemaResponse,
      }),
    enabled: connectionId !== '',
    // Nedostupna baza se ne popravlja ponavljanjem; korisnik ima dugme „Pokušaj ponovo".
    retry: false,
  });
}