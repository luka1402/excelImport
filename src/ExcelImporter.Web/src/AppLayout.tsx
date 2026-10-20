import { Outlet } from 'react-router';
import { GlobalErrorBanner } from './errors/GlobalErrorBanner';
import { ErrorBoundary } from './errors/ErrorBoundary';
import { t } from './messages.sr';
import styles from './AppLayout.module.css';

export function AppLayout() {
  return (
    <>
      <header className={styles.header}>{t('app.title')}</header>
      <GlobalErrorBanner />
      <main className={styles.main}>
        <ErrorBoundary>
          <Outlet />
        </ErrorBoundary>
      </main>
    </>
  );
}
