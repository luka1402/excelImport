import { useCallback, useEffect, useState } from 'react';
import { analyzeFile, type AnalyzeParams, type FileAnalysis } from '../../../api/files';

export type AnalysisState =
  | { readonly status: 'loading' }
  | { readonly status: 'error' }
  | { readonly status: 'ready'; readonly analysis: FileAnalysis };

type Settled = { readonly key: string; readonly outcome: Exclude<AnalysisState, { status: 'loading' }> };

/**
 * Učitava analizu fajla. Greška se prikazuje lokalno u koraku (sa „Pokušaj ponovo"),
 * pa se ne koristi globalni QueryCache koji bi je prikazao i na vrhu stranice.
 * Rezultat važi samo za ključ zahteva za koji je stigao; za novi ključ stanje je „loading".
 */
export function useFileAnalysis({ fileId, sheetIndex, headerRowIndex }: AnalyzeParams): {
  readonly state: AnalysisState;
  readonly retry: () => void;
} {
  const [settled, setSettled] = useState<Settled | null>(null);
  const [attempt, setAttempt] = useState(0);
  const key = `${fileId}|${String(sheetIndex)}|${String(headerRowIndex)}|${attempt.toString()}`;

  useEffect(() => {
    const controller = new AbortController();
    analyzeFile({ fileId, sheetIndex, headerRowIndex }, controller.signal).then(
      (analysis) => {
        setSettled({ key, outcome: { status: 'ready', analysis } });
      },
      (error: unknown) => {
        if (!(error instanceof DOMException && error.name === 'AbortError')) {
          setSettled({ key, outcome: { status: 'error' } });
        }
      },
    );
    return () => {
      controller.abort();
    };
  }, [fileId, sheetIndex, headerRowIndex, key]);

  const retry = useCallback(() => {
    setAttempt((current) => current + 1);
  }, []);

  const state: AnalysisState = settled?.key === key ? settled.outcome : { status: 'loading' };
  return { state, retry };
}
