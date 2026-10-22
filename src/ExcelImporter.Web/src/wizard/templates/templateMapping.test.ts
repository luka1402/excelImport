import { initialWizardData, type WizardData } from '../types';
import { mappingKeyOf } from '../steps/mapping/mappingLogic';
import { fromApplyResponse, toSaveRequest } from './templateMapping';
import type { ApplyTemplateResponse, TemplateSummary } from './templatesApi';

const data: WizardData = {
  ...initialWizardData,
  fileId: 'f1',
  sheet: 'List1',
  sheetIndex: 0,
  headerRowIndex: null,
  connectionId: '3',
  tableSchema: 'dbo',
  table: 'Prodaja',
  mapping: {
    Kupac: { target: 'Kupac', suggested: false },
    Preskočena: { target: '', suggested: false },
  },
  constants: {
    Status: { value: 'Aktivan', now: false },
    Datum: { value: '', now: true },
    Prazna: { value: '', now: false },
  },
  rules: [
    { id: 'a', type: 'conditionalValue', targetField: 'Kupac', conditions: [{ field: 'Kupac', operator: 'isEmpty', value: '' }], value: 'N/A', elseValue: '' },
    { id: 'b', type: 'transform', targetField: 'Kupac', kind: 'trim', format: '', separator: '' },
    { id: 'c', type: 'transform', targetField: 'Datum', kind: 'dateFormat', format: 'dd.MM.yyyy', separator: '' },
  ],
  rowFilters: [{ id: 'd', type: 'rowFilter', action: 'exclude', conditions: [{ field: 'Kupac', operator: 'eq', value: 'x' }] }],
};

describe('toSaveRequest', () => {
  const request = toSaveRequest(data, { name: '  Moj šablon ', sourceColumns: ['Kupac', 'Preskočena'], headerRow: 2 });

  it('šalje samo mapirane kolone i konstante sa vrednošću', () => {
    expect(request.name).toBe('Moj šablon');
    expect(request.connectionId).toBe(3);
    expect(request.mappings).toEqual([{ sourceColumn: 'Kupac', targetColumn: 'Kupac' }]);
    expect(request.constants).toEqual([
      { targetColumn: 'Status', value: { literal: 'Aktivan' } },
      { targetColumn: 'Datum', value: { dynamic: 'Now' } },
    ]);
  });

  it('pravila šalje u obliku koji server čita, bez lokalnih identifikatora i bez nepotrebnih polja', () => {
    expect(request.rules).toEqual([
      { type: 'conditionalValue', version: 1, targetField: 'Kupac', conditions: [{ field: 'Kupac', operator: 'isEmpty' }], value: 'N/A' },
      { type: 'transform', version: 1, targetField: 'Kupac', kind: 'trim' },
      { type: 'transform', version: 1, targetField: 'Datum', kind: 'dateFormat', format: 'dd.MM.yyyy' },
      { type: 'rowFilter', version: 1, action: 'exclude', conditions: [{ field: 'Kupac', operator: 'eq', value: 'x' }] },
    ]);
  });
});

describe('fromApplyResponse', () => {
  const template: TemplateSummary = { id: 7, name: 'Prodaja', connectionId: 3, schemaName: 'dbo', tableName: 'Prodaja', updatedAt: '2026-10-20T10:00:00Z' };
  const response: ApplyTemplateResponse = {
    templateId: 7,
    isUsable: true,
    schemaIssues: [],
    hasColumnWarning: true,
    newColumns: ['Nova'],
    missingColumns: ['Stara'],
    mappings: [{ sourceColumn: 'Kupac', targetColumn: 'Kupac' }],
    constants: [
      { targetColumn: 'Status', value: { literal: 'Aktivan', dynamic: null } },
      { targetColumn: 'Datum', value: { literal: null, dynamic: 'Today' } },
    ],
    rules: [
      { type: 'defaultValue', targetField: 'Status', value: 'X' },
      { type: 'conditionalValue', targetField: 'Kupac', conditions: [{ field: 'Kupac', operator: 'isEmpty', value: null }], value: 'N/A', elseValue: null },
      { type: 'rowFilter', action: 'include', conditions: [{ field: 'Kupac', operator: 'neq', value: 'y' }] },
    ],
  };
  const patch = fromApplyResponse(template, response, { fileId: 'f1', sheetIndex: 0, headerRowIndex: null });

  it('popunjava odredište, mapiranje i konstante', () => {
    expect(patch.connectionId).toBe('3');
    expect(patch.table).toBe('Prodaja');
    expect(patch.mapping).toEqual({ Kupac: { target: 'Kupac', suggested: false } });
    expect(patch.constants).toEqual({ Status: { value: 'Aktivan', now: false }, Datum: { value: '', now: true } });
  });

  it('deli pravila na kolone i filtere redova', () => {
    expect(patch.rules?.map((rule) => rule.type)).toEqual(['defaultValue', 'conditionalValue']);
    expect(patch.rowFilters).toHaveLength(1);
    const conditional = patch.rules?.[1];
    expect(conditional?.type === 'conditionalValue' && conditional.elseValue).toBe('');
    expect(new Set([...(patch.rules ?? []), ...(patch.rowFilters ?? [])].map((rule) => rule.id)).size).toBe(3);
  });

  it('postavlja ključ mapiranja koji korak Mapiranje prepoznaje, da ne pravi novi predlog', () => {
    expect(patch.mappingKey).toBe(
      mappingKeyOf({ fileId: 'f1', sheetIndex: 0, headerRowIndex: null, connectionId: '3', tableSchema: 'dbo', table: 'Prodaja' }),
    );
  });

  it('pamti razlike kolona za upozorenje', () => {
    expect(patch.appliedTemplate).toEqual({ id: 7, name: 'Prodaja', newColumns: ['Nova'], missingColumns: ['Stara'] });
  });
});
