import { t } from '../messages.sr';
import { useErrors } from './ErrorContext';
import styles from './errors.module.css';

export function GlobalErrorBanner() {
  const { error, clearError } = useErrors();
  if (!error) {
    return null;
  }
  const hasDetails = error.status !== undefined || error.traceId !== undefined;
  return (
    <div role="alert" className={styles.banner}>
      <p>{error.message}</p>
      {hasDetails && (
        <details>
          <summary>{t('common.details')}</summary>
          {error.status !== undefined && (
            <p>
              {t('error.status')}: {error.status}
            </p>
          )}
          {error.traceId !== undefined && (
            <p>
              {t('error.traceId')}: {error.traceId}
            </p>
          )}
        </details>
      )}
      <button type="button" onClick={clearError}>
        {t('common.dismiss')}
      </button>
    </div>
  );
}
