import type { ComponentType } from 'react';
import type { MessageKey } from '../messages.sr';

/**
 * Stanje cele čarobnjaka. Živi iznad koraka, pa nazad/napred ne gubi unos.
 * Naredni FE zadaci (T12, T15, T18, ...) proširuju ovaj tip svojim poljima.
 */
export interface WizardData {
  readonly fileName: string;
  readonly sheet: string;
  readonly table: string;
}

export const initialWizardData: WizardData = { fileName: '', sheet: '', table: '' };

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
