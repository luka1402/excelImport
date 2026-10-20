import { useEffect, useRef } from 'react';
import { Navigate, useNavigate, useParams } from 'react-router';
import { t } from '../messages.sr';
import { StepIndicator } from './StepIndicator';
import { WizardProvider, useWizard } from './WizardContext';
import { WizardFooter } from './WizardFooter';
import type { WizardStepDefinition } from './types';
import styles from './wizard.module.css';

interface Props {
  readonly steps: readonly WizardStepDefinition[];
  /** Osnovna putanja čarobnjaka; korak se dodaje kao segment. */
  readonly basePath?: string;
}

export function WizardShell({ steps, basePath = '/uvoz' }: Props) {
  return (
    <WizardProvider>
      <WizardShellContent steps={steps} basePath={basePath} />
    </WizardProvider>
  );
}

function WizardShellContent({ steps, basePath }: Required<Props>) {
  const { korak } = useParams();
  const navigate = useNavigate();
  const { data, furthestIndex, markReached } = useWizard();
  const headingRef = useRef<HTMLHeadingElement>(null);

  const currentIndex = steps.findIndex((step) => step.slug === korak);
  const step = steps[currentIndex];

  useEffect(() => {
    if (!step) {
      return;
    }
    document.title = `${t('nav.stepOf', { current: currentIndex + 1, total: steps.length, title: t(step.navLabelKey) })} — ${t('app.title')}`;
    // Fokus na naslov novog koraka, da čitač ekrana najavi promenu.
    headingRef.current?.focus();
  }, [step, currentIndex, steps.length]);

  const first = steps[0];
  if (!step || !first) {
    return <Navigate to={`${basePath}/${first?.slug ?? ''}`} replace />;
  }
  // Osvežavanje ili ručni unos URL-a ne sme da preskoči korake čije podatke nemamo.
  if (currentIndex > furthestIndex) {
    return <Navigate to={`${basePath}/${steps[furthestIndex]?.slug ?? first.slug}`} replace />;
  }

  const goTo = (index: number) => {
    const target = steps[index];
    if (!target) {
      return;
    }
    markReached(index);
    void navigate(`${basePath}/${target.slug}`);
  };

  const isLast = currentIndex === steps.length - 1;
  const canGoNext = step.isValid ? step.isValid(data) : true;
  const StepComponent = step.Component;

  return (
    <section className={styles.shell}>
      <StepIndicator
        steps={steps}
        currentIndex={currentIndex}
        onSelect={(index) => {
          goTo(index);
        }}
      />
      <div className={styles.content}>
        <h1 ref={headingRef} tabIndex={-1}>
          {t(step.titleKey)}
        </h1>
        <p>{t(step.hintKey)}</p>
        <StepComponent />
      </div>
      <WizardFooter
        canGoBack={currentIndex > 0}
        canGoNext={canGoNext}
        showNext={!isLast}
        disabledReason={step.disabledReasonKey ? t(step.disabledReasonKey) : undefined}
        onBack={() => {
          goTo(currentIndex - 1);
        }}
        onNext={() => {
          goTo(currentIndex + 1);
        }}
      />
    </section>
  );
}
