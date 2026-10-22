import type { WizardStepDefinition } from '../types';
import { PlaceholderStep } from './PlaceholderStep';
import { AnalysisStep } from './analysis/AnalysisStep';
import { UploadStep } from './upload/UploadStep';
import { TargetStep } from './target/TargetStep';
import { MappingStep } from './mapping/MappingStep';
import { RulesStep } from './rules/RulesStep';

export const wizardSteps: readonly WizardStepDefinition[] = [
  {
    slug: 'fajl',
    titleKey: 'step.file.title',
    navLabelKey: 'step.file.nav',
    hintKey: 'step.file.hint',
    Component: UploadStep,
    isValid: (data) => data.fileId !== '',
    disabledReasonKey: 'nav.disabledReason.file',
  },
  {
    slug: 'analiza',
    titleKey: 'step.analysis.title',
    navLabelKey: 'step.analysis.nav',
    hintKey: 'step.analysis.hint',
    Component: AnalysisStep,
    isValid: (data) => data.analysisUsable,
    disabledReasonKey: 'nav.disabledReason.analysisSheet',
  },
  {
    slug: 'odredište',
    titleKey: 'step.target.title',
    navLabelKey: 'step.target.nav',
    hintKey: 'step.target.hint',
    Component: TargetStep,
    isValid: (data) => data.connectionId !== '' && data.table !== '',
    disabledReasonKey: 'nav.disabledReason.target',
  },
  {
    slug: 'mapiranje',
    titleKey: 'step.mapping.title',
    navLabelKey: 'step.mapping.nav',
    hintKey: 'step.mapping.hint',
    Component: MappingStep,
    isValid: (data) => data.mappingComplete,
    disabledReasonKey: 'nav.disabledReason.mapping',
  },
  {
    slug: 'pravila',
    titleKey: 'step.rules.title',
    navLabelKey: 'step.rules.nav',
    hintKey: 'step.rules.hint',
    Component: RulesStep,
    isValid: (data) => !data.rulesEditing,
    disabledReasonKey: 'nav.disabledReason.rules',
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
