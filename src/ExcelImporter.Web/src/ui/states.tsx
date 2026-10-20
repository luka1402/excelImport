import type { ReactNode } from 'react';
import { t } from '../messages.sr';
import styles from './states.module.css';

export function LoadingState({ message = t('common.loading') }: { readonly message?: string }) {
  return (
    <div role="status" aria-live="polite" className={styles.state}>
      {message}
    </div>
  );
}

interface ErrorStateProps {
  readonly message: string;
  readonly onRetry?: () => void;
  readonly details?: ReactNode;
}

export function ErrorState({ message, onRetry, details }: ErrorStateProps) {
  return (
    <div role="alert" className={[styles.state, styles.error].join(' ')}>
      <p>{message}</p>
      {onRetry && (
        <button type="button" onClick={onRetry}>
          {t('nav.retry')}
        </button>
      )}
      {details && (
        <details>
          <summary>{t('common.details')}</summary>
          {details}
        </details>
      )}
    </div>
  );
}

interface EmptyStateProps {
  readonly message: string;
  readonly action?: ReactNode;
}

export function EmptyState({ message, action }: EmptyStateProps) {
  return (
    <div className={styles.state}>
      <p>{message}</p>
      {action}
    </div>
  );
}
