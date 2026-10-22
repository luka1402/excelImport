import type { ComponentType } from 'react';
import type { MessageKey } from '../messages.sr';

/**
 * Stanje cele čarobnjaka. Živi iznad koraka, pa nazad/napred ne gubi unos.
 * Naredni FE zadaci (T12, T15, T18, ...) proširuju ovaj tip svojim poljima.
 */
export interface WizardData {
  readonly fileName: string;
  /** Identifikator otpremljenog fajla na serveru; prazan dok fajl nije otpremljen. */
  readonly fileId: string;
  readonly fileSize: number;
  readonly sheet: string;
  /** 0-based indeks izabranog sheet-a; null dok analiza nije izabrala sheet. */
  readonly sheetIndex: number | null;
  /** 0-based indeks ručno izabranog header reda; null znači automatsko prepoznavanje. */
  readonly headerRowIndex: number | null;
  /** Izabrani sheet ima bar jednu kolonu sa podacima, pa je moguće nastaviti. */
  readonly analysisUsable: boolean;
  readonly connectionId: string;
  readonly tableSchema: string;
  readonly table: string;
  /** Mapiranje po nazivu kolone iz fajla; kolona bez unosa je preskočena. */
  readonly mapping: Readonly<Record<string, MappingChoice>>;
  /** Konstante po nazivu ciljne kolone koja nije mapirana iz fajla. */
  readonly constants: Readonly<Record<string, ConstantChoice>>;
  /** Ulazi za koje je mapiranje napravljeno; kada se promene, predlog se pravi iznova. */
  readonly mappingKey: string;
  /** Mapiranje je kompletno (obavezne kolone pokrivene, konstante ispravne), pa je moguće nastaviti. */
  readonly mappingComplete: boolean;
  /** Pravila po ciljnoj koloni, redosledom unosa; brišu se kada se mapiranje napravi iznova. */
  readonly rules: readonly ColumnRule[];
  /** Filteri redova; svi moraju biti zadovoljeni da bi red bio uvezen. Brišu se zajedno sa pravilima. */
  readonly rowFilters: readonly RowFilterDraft[];
  /** Forma pravila je otvorena (nova ili izmena); blokira nastavak dok se ne sačuva ili otkaže. */
  readonly rulesEditing: boolean;
  /** Šablon primenjen na ovaj fajl; null kada mapiranje nije iz šablona. */
  readonly appliedTemplate: AppliedTemplate | null;
}

export interface AppliedTemplate {
  readonly id: number;
  readonly name: string;
  /** Kolone fajla kojih nije bilo u fajlu iz kog je šablon nastao. */
  readonly newColumns: readonly string[];
  /** Kolone iz šablona kojih nema u ovom fajlu; njihova mapiranja nisu primenjena. */
  readonly missingColumns: readonly string[];
}

/** Nazivi prate ComparisonOperator i TransformKind na serveru (JSON vrednosti). */
export type ComparisonOperator =
  | 'eq'
  | 'neq'
  | 'gt'
  | 'lt'
  | 'gte'
  | 'lte'
  | 'contains'
  | 'startsWith'
  | 'isEmpty'
  | 'isNotEmpty';

export type TransformKind = 'trim' | 'upper' | 'lower' | 'dateFormat' | 'decimalSeparator';

export interface RuleCondition {
  /** Naziv ciljne kolone čija se vrednost proverava. */
  readonly field: string;
  readonly operator: ComparisonOperator;
  readonly value: string;
}

interface RuleBase {
  /** Lokalni identifikator za izmenu i brisanje; ne šalje se serveru. */
  readonly id: string;
  readonly targetField: string;
}

export interface ConditionalValueDraft extends RuleBase {
  readonly type: 'conditionalValue';
  readonly conditions: readonly RuleCondition[];
  readonly value: string;
  /** Prazno znači da kolona ostaje nepromenjena kada uslovi nisu ispunjeni. */
  readonly elseValue: string;
}

export interface DefaultValueDraft extends RuleBase {
  readonly type: 'defaultValue';
  readonly value: string;
}

export interface TransformDraft extends RuleBase {
  readonly type: 'transform';
  readonly kind: TransformKind;
  readonly format: string;
  readonly separator: ',' | '.' | '';
}

/** Vrednost izvorne kolone traži se u ključnoj koloni tabele ciljne baze; upisuje se povratna kolona. */
export interface LookupDraft extends RuleBase {
  readonly type: 'lookup';
  /** Mapirana kolona čija se vrednost traži. */
  readonly sourceField: string;
  readonly schema: string;
  readonly table: string;
  readonly keyColumn: string;
  readonly returnColumn: string;
}

export type ColumnRule = ConditionalValueDraft | DefaultValueDraft | TransformDraft | LookupDraft;

export type RowFilterAction = 'include' | 'exclude';

/** Filter nije vezan za jednu kolonu: uključuje ili isključuje redove čiji su svi uslovi ispunjeni. */
export interface RowFilterDraft {
  readonly id: string;
  readonly type: 'rowFilter';
  readonly action: RowFilterAction;
  readonly conditions: readonly RuleCondition[];
}

export type RuleDraft = ColumnRule | RowFilterDraft;

export interface MappingChoice {
  readonly target: string;
  /** Automatski predlog koji korisnik još nije potvrdio ni promenio. */
  readonly suggested: boolean;
}

export interface ConstantChoice {
  readonly value: string;
  /** Današnji datum/vreme, računa se u trenutku uvoza. */
  readonly now: boolean;
}

export const initialWizardData: WizardData = {
  fileName: '',
  fileId: '',
  fileSize: 0,
  sheet: '',
  sheetIndex: null,
  headerRowIndex: null,
  analysisUsable: false,
  connectionId: '',
  tableSchema: '',
  table: '',
  mapping: {},
  constants: {},
  mappingKey: '',
  mappingComplete: false,
  rules: [],
  rowFilters: [],
  rulesEditing: false,
  appliedTemplate: null,
};

export interface WizardStepDefinition {
  /** Segment u URL-u: /uvoz/:slug */
  readonly slug: string;
  readonly titleKey: MessageKey;
  readonly navLabelKey: MessageKey;
  readonly hintKey: MessageKey;
  readonly Component: ComponentType;
  /** Dalje je dozvoljeno samo kada je korak validan; podrazumevano uvek. */
  readonly isValid?: (data: WizardData) => boolean;
  /** Razlog prikazan ispod dugmeta „Dalje" dok korak nije validan. */
  readonly disabledReasonKey?: MessageKey;
}
