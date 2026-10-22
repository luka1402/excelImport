import type { ColumnRule, LookupDraft, RowFilterDraft } from '../../types';
import type { ColumnInfo, TableInfo } from '../target/targetApi';
import { describeRule, isDateFormatValid, operatorNeedsValue, operators, validateRule } from './rulesLogic';

function column(name: string, dataType: string, maxLength: number | null = null): ColumnInfo {
  return { name, ordinal: 1, dataType, isNullable: true, maxLength, isIdentity: false, isComputed: false };
}

const columns = new Map<string, ColumnInfo>([
  ['Kupac', column('Kupac', 'nvarchar', 5)],
  ['Kolicina', column('Kolicina', 'int')],
  ['Datum', column('Datum', 'datetime')],
]);

describe('operatori', () => {
  it('nudi svih deset operatora', () => {
    expect(operators).toHaveLength(10);
  });

  it('je prazno / nije prazno ne koriste vrednost', () => {
    expect(operatorNeedsValue('isEmpty')).toBe(false);
    expect(operatorNeedsValue('isNotEmpty')).toBe(false);
    expect(operatorNeedsValue('eq')).toBe(true);
  });
});

describe('validateRule', () => {
  const conditional = (overrides: Partial<Extract<ColumnRule, { type: 'conditionalValue' }>> = {}): ColumnRule => ({
    id: '1',
    type: 'conditionalValue',
    targetField: 'Kolicina',
    conditions: [{ field: 'Kolicina', operator: 'gt', value: '5' }],
    value: '1',
    elseValue: '',
    ...overrides,
  });

  it('ispravno uslovno pravilo nema grešaka', () => {
    expect(validateRule(conditional(), columns).hasErrors).toBe(false);
  });

  it('traži kolonu i vrednost uslova', () => {
    const errors = validateRule(conditional({ conditions: [{ field: '', operator: 'eq', value: '' }] }), columns);
    expect(errors.conditions[0]?.field).toBe('Izaberite kolonu.');
    const noValue = validateRule(conditional({ conditions: [{ field: 'Kolicina', operator: 'eq', value: ' ' }] }), columns);
    expect(noValue.conditions[0]?.value).toBe('Unesite vrednost.');
  });

  it('ne traži vrednost za je prazno', () => {
    const errors = validateRule(conditional({ conditions: [{ field: 'Kolicina', operator: 'isEmpty', value: '' }] }), columns);
    expect(errors.hasErrors).toBe(false);
  });

  it('numerička kolona traži broj, osim za sadrži i počinje sa', () => {
    const bad = validateRule(conditional({ conditions: [{ field: 'Kolicina', operator: 'gt', value: 'abc' }] }), columns);
    expect(bad.conditions[0]?.value).toBe('Vrednost mora biti broj.');
    const text = validateRule(conditional({ conditions: [{ field: 'Kolicina', operator: 'contains', value: 'abc' }] }), columns);
    expect(text.hasErrors).toBe(false);
  });

  it('proverava vrednost i opcioni „u suprotnom" prema tipu ciljne kolone', () => {
    const errors = validateRule(conditional({ value: '', elseValue: 'x' }), columns);
    expect(errors.value).toBe('Unesite vrednost.');
    expect(errors.elseValue).toBe('Vrednost mora biti broj.');
  });

  it('podrazumevana vrednost je obavezna i mora odgovarati tipu', () => {
    const rule = (value: string): ColumnRule => ({ id: '2', type: 'defaultValue', targetField: 'Datum', value });
    expect(validateRule(rule(''), columns).value).toBe('Unesite vrednost.');
    expect(validateRule(rule('nije datum'), columns).value).toBe('Vrednost mora biti ispravan datum.');
    expect(validateRule(rule('2026-01-31'), columns).hasErrors).toBe(false);
  });

  it('tekst duži od kolone daje grešku tipa', () => {
    const rule: ColumnRule = { id: '3', type: 'defaultValue', targetField: 'Kupac', value: 'predugo' };
    expect(validateRule(rule, columns).value).toBe('Vrednost ne odgovara tipu kolone (nvarchar).');
  });

  it('transformacija datuma traži ispravan format, a decimalna separator', () => {
    const date = (format: string): ColumnRule => ({ id: '4', type: 'transform', targetField: 'Datum', kind: 'dateFormat', format, separator: '' });
    expect(validateRule(date(''), columns).format).toBe('Format datuma nije ispravan.');
    expect(validateRule(date('dd.MM.yyyy'), columns).hasErrors).toBe(false);
    const decimal: ColumnRule = { id: '5', type: 'transform', targetField: 'Kolicina', kind: 'decimalSeparator', format: '', separator: '' };
    expect(validateRule(decimal, columns).separator).toBe('Izaberite decimalni separator.');
    expect(validateRule({ ...decimal, separator: ',' }, columns).hasErrors).toBe(false);
  });

  it('trim/upper/lower nemaju dodatna polja za proveru', () => {
    const rule: ColumnRule = { id: '6', type: 'transform', targetField: 'Kupac', kind: 'upper', format: '', separator: '' };
    expect(validateRule(rule, columns).hasErrors).toBe(false);
  });
});

describe('lookup i filter redova', () => {
  const tables: TableInfo[] = [
    {
      schema: 'dbo',
      name: 'Grad',
      columns: [column('Id', 'int'), column('Naziv', 'nvarchar', 50)],
      primaryKey: ['Id'],
      foreignKeys: [],
    },
  ];
  const lookup = (overrides: Partial<LookupDraft> = {}): LookupDraft => ({
    id: '7',
    type: 'lookup',
    targetField: 'Kolicina',
    sourceField: 'Kupac',
    schema: 'dbo',
    table: 'Grad',
    keyColumn: 'Naziv',
    returnColumn: 'Id',
    ...overrides,
  });

  it('ispravan lookup nema grešaka', () => {
    expect(validateRule(lookup(), columns, tables).hasErrors).toBe(false);
  });

  it('traži izbor tabele i kolona', () => {
    const errors = validateRule(lookup({ sourceField: '', table: '', keyColumn: '', returnColumn: '' }), columns, tables);
    expect(errors.lookup).toEqual({
      sourceField: 'Izaberite kolonu.',
      table: 'Izaberite tabelu.',
      keyColumn: 'Izaberite ključnu kolonu.',
      returnColumn: 'Izaberite povratnu kolonu.',
    });
  });

  it('odbija tabelu i kolone koje ne postoje u šemi', () => {
    expect(validateRule(lookup({ table: 'Nema' }), columns, tables).lookup.table).toBe('Tabela više ne postoji u šemi baze.');
    expect(validateRule(lookup({ keyColumn: 'Nema' }), columns, tables).lookup.keyColumn).toBe(
      'Kolona ne postoji u izabranoj tabeli.',
    );
  });

  it('filter traži bar jedan ispravan uslov i opisuje se rečenicom', () => {
    const filter: RowFilterDraft = {
      id: '8',
      type: 'rowFilter',
      action: 'exclude',
      conditions: [
        { field: 'Kolicina', operator: 'lt', value: '1' },
        { field: 'Kupac', operator: 'isEmpty', value: '' },
      ],
    };
    expect(validateRule(filter, columns).hasErrors).toBe(false);
    expect(describeRule(filter)).toBe('Preskoči redove gde Kolicina manje od „1" i Kupac je prazno');
    expect(describeRule({ ...filter, action: 'include' })).toMatch(/^Uvezi samo redove gde /);
    expect(validateRule({ ...filter, conditions: [{ field: '', operator: 'eq', value: '' }] }, columns).hasErrors).toBe(true);
  });
});

describe('isDateFormatValid', () => {
  it.each([
    ['dd.MM.yyyy', true],
    ['yyyy-MM-dd HH:mm', true],
    ['', false],
    ['abc', false],
    ['...', false],
  ])('%s → %s', (format, expected) => {
    expect(isDateFormatValid(format)).toBe(expected);
  });
});

describe('describeRule', () => {
  it('opisuje uslovno pravilo sa i bez „u suprotnom"', () => {
    const rule: ColumnRule = {
      id: '1',
      type: 'conditionalValue',
      targetField: 'Status',
      conditions: [
        { field: 'Status', operator: 'eq', value: 'Da' },
        { field: 'Kolicina', operator: 'isEmpty', value: '' },
      ],
      value: '1',
      elseValue: '',
    };
    expect(describeRule(rule)).toBe('Ako Status je jednako „Da" i Kolicina je prazno onda „1"');
    expect(describeRule({ ...rule, elseValue: '0' })).toContain('inače „0"');
  });

  it('opisuje podrazumevanu vrednost i transformacije', () => {
    expect(describeRule({ id: '2', type: 'defaultValue', targetField: 'A', value: 'x' })).toBe('Podrazumevana vrednost: „x"');
    expect(describeRule({ id: '3', type: 'transform', targetField: 'A', kind: 'trim', format: '', separator: '' })).toBe(
      'Transformacija: Ukloni razmake',
    );
    expect(describeRule({ id: '4', type: 'transform', targetField: 'A', kind: 'dateFormat', format: 'dd.MM.yyyy', separator: '' })).toBe(
      'Transformacija: Format datuma (dd.MM.yyyy)',
    );
  });
});
