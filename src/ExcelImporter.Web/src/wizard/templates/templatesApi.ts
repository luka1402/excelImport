import { useQuery, useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import { apiRequest } from '../../api/client';

// Ugovor prati TemplateEndpoints na serveru (camelCase JSON). Pravila i konstante su JSON onako kako ih čuva server.

const templateSummarySchema = z.object({
  id: z.number().int(),
  name: z.string(),
  connectionId: z.number().int(),
  schemaName: z.string(),
  tableName: z.string(),
  updatedAt: z.string(),
});

const columnMappingSchema = z.object({ sourceColumn: z.string(), targetColumn: z.string() });

const constantSchema = z.object({
  targetColumn: z.string(),
  value: z.object({
    literal: z.string().nullish(),
    dynamic: z.enum(['Today', 'Now']).nullish(),
  }),
});

const conditionSchema = z.object({
  field: z.string(),
  operator: z.enum(['eq', 'neq', 'gt', 'lt', 'gte', 'lte', 'contains', 'startsWith', 'isEmpty', 'isNotEmpty']),
  value: z.string().nullish(),
});

const serverRuleSchema = z.discriminatedUnion('type', [
  z.object({
    type: z.literal('conditionalValue'),
    targetField: z.string(),
    conditions: z.array(conditionSchema),
    value: z.string().nullish(),
    elseValue: z.string().nullish(),
  }),
  z.object({ type: z.literal('defaultValue'), targetField: z.string(), value: z.string() }),
  z.object({
    type: z.literal('transform'),
    targetField: z.string(),
    kind: z.enum(['trim', 'upper', 'lower', 'dateFormat', 'decimalSeparator']),
    format: z.string().nullish(),
    separator: z.string().nullish(),
  }),
  z.object({
    type: z.literal('lookup'),
    targetField: z.string(),
    sourceField: z.string(),
    schema: z.string(),
    table: z.string(),
    keyColumn: z.string(),
    returnColumn: z.string(),
  }),
  z.object({ type: z.literal('rowFilter'), action: z.enum(['include', 'exclude']), conditions: z.array(conditionSchema) }),
]);

const applyResponseSchema = z.object({
  templateId: z.number().int(),
  isUsable: z.boolean(),
  schemaIssues: z.array(z.string()),
  hasColumnWarning: z.boolean(),
  newColumns: z.array(z.string()),
  missingColumns: z.array(z.string()),
  mappings: z.array(columnMappingSchema),
  rules: z.array(serverRuleSchema),
  constants: z.array(constantSchema),
});

export type TemplateSummary = z.infer<typeof templateSummarySchema>;
export type ApplyTemplateResponse = z.infer<typeof applyResponseSchema>;
export type ServerRule = z.infer<typeof serverRuleSchema>;
export type ServerCondition = z.infer<typeof conditionSchema>;
export type ServerConstant = z.infer<typeof constantSchema>;

/** Telo zahteva za čuvanje šablona; pravila i konstante su u obliku koji server čuva. */
export interface SaveTemplateRequest {
  readonly name: string;
  readonly connectionId: number;
  readonly schemaName: string;
  readonly tableName: string;
  readonly sheetName: string | null;
  readonly headerRow: number;
  readonly sourceColumns: readonly string[];
  readonly mappings: readonly { readonly sourceColumn: string; readonly targetColumn: string }[];
  readonly rules: readonly unknown[];
  readonly constants: readonly unknown[];
}

const templatesKey = ['templates'] as const;

export function useTemplates() {
  return useQuery({
    queryKey: templatesKey,
    queryFn: ({ signal }) => apiRequest('/api/templates', { signal, schema: z.array(templateSummarySchema) }),
    retry: false,
  });
}

/** Čuvanje, brisanje i primena su pozivi sa lokalnim prikazom greške, pa nisu mutacije globalnog keša. */
export function useTemplateActions() {
  const queryClient = useQueryClient();
  const refresh = () => queryClient.invalidateQueries({ queryKey: templatesKey });

  return {
    async save(request: SaveTemplateRequest): Promise<void> {
      await apiRequest('/api/templates', { method: 'POST', body: request });
      await refresh();
    },
    async remove(id: number): Promise<void> {
      await apiRequest(`/api/templates/${id.toString()}`, { method: 'DELETE' });
      await refresh();
    },
    apply(id: number, fileId: string, sheetIndex: number | null, headerRowIndex: number | null): Promise<ApplyTemplateResponse> {
      const query = new URLSearchParams({ fileId });
      if (sheetIndex !== null) {
        query.set('sheet', sheetIndex.toString());
      }
      if (headerRowIndex !== null) {
        query.set('headerRow', headerRowIndex.toString());
      }
      return apiRequest(`/api/templates/${id.toString()}/apply?${query.toString()}`, { schema: applyResponseSchema });
    },
  };
}
