import { t } from '../messages.sr';
import type { WizardStepDefinition } from './types';
import styles from './wizard.module.css';

interface Props {
  readonly steps: readonly WizardStepDefinition[];
  readonly currentIndex: number;
  readonly onSelect: (index: number) => void;
}

export function StepIndicator({ steps, currentIndex, onSelect }: Props) {
  const current = steps[currentIndex];
  return (
    <nav aria-label={t('nav.steps')} className={styles.indicator}>
      <ol>
        {steps.map((step, index) => {
          const isCurrent = index === currentIndex;
          const isDone = index < currentIndex;
          const label = `${(index + 1).toString()}. ${t(step.navLabelKey)}`;
          return (
            <li key={step.slug} aria-current={isCurrent ? 'step' : undefined}>
              {isDone ? (
                <button
                  type="button"
                  className={styles.stepLink}
                  onClick={() => {
                    onSelect(index);
                  }}
                >
                  <span aria-hidden="true">✔ </span>
                  {label}
                </button>
              ) : (
                <span aria-disabled={isCurrent ? undefined : 'true'} className={isCurrent ? styles.stepCurrent : styles.stepFuture}>
                  {label}
                </span>
              )}
            </li>
          );
        })}
      </ol>
      {current && (
        <div className={styles.compact}>
          <span>
            {t('nav.stepOf', { current: currentIndex + 1, total: steps.length, title: t(current.navLabelKey) })}
          </span>
          <progress max={steps.length} value={currentIndex + 1} aria-hidden="true" />
        </div>
      )}
    </nav>
  );
}
