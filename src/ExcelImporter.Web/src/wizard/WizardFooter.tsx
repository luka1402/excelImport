import { t } from '../messages.sr';
import styles from './wizard.module.css';

interface Props {
  readonly canGoBack: boolean;
  readonly canGoNext: boolean;
  readonly showNext: boolean;
  readonly disabledReason: string | undefined;
  readonly onBack: () => void;
  readonly onNext: () => void;
}

const reasonId = 'wizard-next-reason';

export function WizardFooter({ canGoBack, canGoNext, showNext, disabledReason, onBack, onNext }: Props) {
  const reasonVisible = showNext && !canGoNext && disabledReason !== undefined;
  return (
    <footer className={styles.footer}>
      <button type="button" onClick={onBack} disabled={!canGoBack}>
        ← {t('nav.back')}
      </button>
      {showNext && (
        <div className={styles.next}>
          <button
            type="button"
            onClick={onNext}
            disabled={!canGoNext}
            aria-describedby={reasonVisible ? reasonId : undefined}
          >
            {t('nav.next')} →
          </button>
          {reasonVisible && <p id={reasonId}>{disabledReason}</p>}
        </div>
      )}
    </footer>
  );
}
