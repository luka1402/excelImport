import type { AnalyzedColumn } from '../../../api/files';
import type { ColumnInfo, TableInfo } from '../target/targetApi';
import { evaluateMapping, isConstantValid, isTypeCompatible, normalizeColumnName, suggestMapping, textOverflowMax } from './mappingLogic';

function source(index: number, name: string, type: AnalyzedColumn['type'] = 'Text', flags: Partial<AnalyzedColumn> = {}): AnalyzedColumn {
  return { index, name, type, isEmpty: false, isMixed: false, nonEmptyCount: 1, ...flags };
}

function target(name: string, dataType = 'nvarchar', extra: Partial<ColumnInfo> = {}): ColumnInfo {
  return { name, ordinal: 1, dataType, isNullable: true, maxLength: null, isIdentity: false, isComputed: false, ...extra };
}

function table(...columns: ColumnInfo[]): TableInfo {
  return { schema: 'dbo', name: 'T', columns, primaryKey: [], foreignKeys: [] };
}

describe('normalizeColumnName', () => {
  it('ignoriše slova, razmake, interpunkciju i dijakritike', () => {
    expect(normalizeColumnName('Šifra Kupca')).toBe('sifrakupca');
    expect(normalizeColumnName('sifra_kupca')).toBe('sifrakupca');
    expect(normalizeColumnName('ĐURĐE-ČAĆA.Ž')).toBe('durdecacaz');
  });
});

describe('suggestMapping', () => {
  it('uparuje iste i slične nazive, a svaku ciljnu kolonu najviše jednom', () => {
    const suggestions = suggestMapping(
      [source(0, 'Šifra Kupca'), source(1, 'Kolicinaa'), source(2, 'sifra kupca')],
      table(target('SifraKupca'), target('Kolicina')),
    );
    expect(suggestions).toEqual([
      { sourceColumn: 'Šifra Kupca', targetColumn: 'SifraKupca' },
      { sourceColumn: 'Kolicinaa', targetColumn: 'Kolicina' },
    ]);
  });

  it('ne predlaže nazive koji se razlikuju po ciframa ni identity/computed kolone', () => {
    const suggestions = suggestMapping(
      [source(0, 'Adresa1'), source(1, 'Id')],
      table(target('Adresa2'), target('Id', 'int', { isIdentity: true })),
    );
    expect(suggestions).toEqual([]);
  });
});

describe('isTypeCompatible', () => {
  it('tekst ide u tekstualne kolone, broj u numeričke, a datum samo u datumske', () => {
    expect(isTypeCompatible(source(0, 'a', 'Text'), target('x', 'int'))).toBe(false);
    expect(isTypeCompatible(source(0, 'a', 'Text'), target('x', 'varchar'))).toBe(true);
    expect(isTypeCompatible(source(0, 'a', 'DecimalNumber'), target('x', 'int'))).toBe(false);
    expect(isTypeCompatible(source(0, 'a', 'WholeNumber'), target('x', 'decimal'))).toBe(true);
    expect(isTypeCompatible(source(0, 'a', 'Date'), target('x', 'datetime2'))).toBe(true);
    expect(isTypeCompatible(source(0, 'a', 'Boolean'), target('x', 'bit'))).toBe(true);
    expect(isTypeCompatible(source(0, 'a', 'Text', { isMixed: true }), target('x', 'uniqueidentifier'))).toBe(true);
    expect(isTypeCompatible(source(0, 'a', 'Text', { isEmpty: true }), target('x', 'int'))).toBe(true);
  });
});

describe('textOverflowMax', () => {
  it('vraća max dužinu samo kada neka vrednost prelazi tekstualnu kolonu', () => {
    expect(textOverflowMax(target('x', 'nvarchar', { maxLength: 3 }), ['abc', 'abcd'])).toBe(3);
    expect(textOverflowMax(target('x', 'nvarchar', { maxLength: 3 }), ['abc', null])).toBeNull();
    expect(textOverflowMax(target('x', 'nvarchar', { maxLength: -1 }), ['abcd'])).toBeNull();
    expect(textOverflowMax(target('x', 'int', { maxLength: 3 }), ['abcd'])).toBeNull();
  });
});

describe('isConstantValid', () => {
  it('proverava literal prema tipu kolone', () => {
    expect(isConstantValid(target('x', 'int'), { value: '12', now: false })).toBe(true);
    expect(isConstantValid(target('x', 'int'), { value: '1.5', now: false })).toBe(false);
    expect(isConstantValid(target('x', 'decimal'), { value: '1,5', now: false })).toBe(true);
    expect(isConstantValid(target('x', 'datetime'), { value: '2026-10-22', now: false })).toBe(true);
    expect(isConstantValid(target('x', 'datetime'), { value: 'sutra', now: false })).toBe(false);
    expect(isConstantValid(target('x', 'bit'), { value: 'da', now: false })).toBe(true);
    expect(isConstantValid(target('x', 'nvarchar', { maxLength: 2 }), { value: 'abc', now: false })).toBe(false);
    expect(isConstantValid(target('x', 'int'), { value: '', now: true })).toBe(false);
    expect(isConstantValid(target('x', 'date'), { value: '', now: true })).toBe(true);
  });
});

describe('evaluateMapping', () => {
  const sales = table(target('Id', 'int', { isIdentity: true, isNullable: false }), target('Kupac', 'nvarchar', { isNullable: false }), target('Status', 'nvarchar', { isNullable: false }));

  it('nemapirana obavezna kolona bez konstante nije kompletna, sa konstantom jeste', () => {
    const mapping = { Kupac: { target: 'Kupac' } };
    expect(evaluateMapping(sales, mapping, {})).toMatchObject({ isComplete: false, missingRequired: ['Status'] });
    expect(evaluateMapping(sales, mapping, { Status: { value: 'A', now: false } }).isComplete).toBe(true);
  });

  it('bez ijedne mapirane kolone nije kompletna', () => {
    expect(evaluateMapping(table(target('A')), {}, {}).isComplete).toBe(false);
  });
});
