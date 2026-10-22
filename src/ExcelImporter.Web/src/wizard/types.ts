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
}

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
