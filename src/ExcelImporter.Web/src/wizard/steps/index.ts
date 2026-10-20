import type { WizardStepDefinition } from '../types';
import { PlaceholderStep } from './PlaceholderStep';

export const wizardSteps: readonly WizardStepDefinition[] = [
  {
    slug: 'fajl',
    titleKey: 'step.file.title',
    navLabelKey: 'step.file.nav',
    hintKey: 'step.file.hint',
    Component: PlaceholderStep,
  },
  {
    slug: 'analiza',
    titleKey: 'step.analysis.title',
    navLabelKey: 'step.analysis.nav',
    hintKey: 'step.analysis.hint',
    Component: PlaceholderStep,
  },
  {
    slug: 'odredište',
    titleKey: 'step.target.title',
    navLabelKey: 'step.target.nav',
    hintKey: 'step.target.hint',
    Component: PlaceholderStep,
  },
  {
    slug: 'mapiranje',
    titleKey: 'step.mapping.title',
    navLabelKey: 'step.mapping.nav',
    hintKey: 'step.mapping.hint',
    Component: PlaceholderStep,
  },
  {
    slug: 'pravila',
    titleKey: 'step.rules.title',
    navLabelKey: 'step.rules.nav',
    hintKey: 'step.rules.hint',
    Component: PlaceholderStep,
  },
  {
    slug: 'pregled',
    titleKey: 'step.review.title',
    navLabelKey: 'step.review.nav',
    hintKey: 'step.review.hint',
    Component: PlaceholderStep,
  },
  {
    slug: 'uvoz',
    titleKey: 'step.import.title',
    navLabelKey: 'step.import.nav',
    hintKey: 'step.import.hint',
    Component: PlaceholderStep,
  },
];
