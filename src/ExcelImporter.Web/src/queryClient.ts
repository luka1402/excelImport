import { MutationCache, QueryCache, QueryClient } from '@tanstack/react-query';

/** Svaka neobrađena greška upita ili mutacije ide u globalni prikaz grešaka. */
export function createQueryClient(reportError: (error: unknown) => void): QueryClient {
  return new QueryClient({
    queryCache: new QueryCache({ onError: reportError }),
    mutationCache: new MutationCache({ onError: reportError }),
    defaultOptions: { queries: { retry: 1, refetchOnWindowFocus: false } },
  });
}
