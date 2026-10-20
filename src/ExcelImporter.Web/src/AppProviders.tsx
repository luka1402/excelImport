import { QueryClientProvider } from '@tanstack/react-query';
import { useState, type ReactNode } from 'react';
import { ErrorProvider, useErrors } from './errors/ErrorContext';
import { createQueryClient } from './queryClient';

function QueryProvider({ children }: { readonly children: ReactNode }) {
  const { reportError } = useErrors();
  const [client] = useState(() => createQueryClient(reportError));
  return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
}

export function AppProviders({ children }: { readonly children: ReactNode }) {
  return (
    <ErrorProvider>
      <QueryProvider>{children}</QueryProvider>
    </ErrorProvider>
  );
}
