import { createContext, use, useCallback, useMemo, useState, type ReactNode } from 'react';
import { ApiError } from '../api/apiError';
import { t } from '../messages.sr';

export interface DisplayedError {
  readonly message: string;
  readonly status?: number;
  readonly traceId?: string;
}

interface ErrorContextValue {
  readonly error: DisplayedError | null;
  readonly reportError: (error: unknown) => void;
  readonly clearError: () => void;
}

const ErrorContext = createContext<ErrorContextValue | null>(null);

export function toDisplayedError(error: unknown): DisplayedError {
  if (error instanceof ApiError) {
    return { message: error.userMessage, status: error.status, ...(error.traceId ? { traceId: error.traceId } : {}) };
  }
  return { message: t('error.unexpected') };
}

export function ErrorProvider({ children }: { readonly children: ReactNode }) {
  const [error, setError] = useState<DisplayedError | null>(null);
  const reportError = useCallback((reported: unknown) => {
    setError(toDisplayedError(reported));
  }, []);
  const clearError = useCallback(() => {
    setError(null);
  }, []);
  const value = useMemo(() => ({ error, reportError, clearError }), [error, reportError, clearError]);
  return <ErrorContext value={value}>{children}</ErrorContext>;
}

export function useErrors(): ErrorContextValue {
  const context = use(ErrorContext);
  if (!context) {
    throw new Error('useErrors mora biti unutar ErrorProvider-a.');
  }
  return context;
}
