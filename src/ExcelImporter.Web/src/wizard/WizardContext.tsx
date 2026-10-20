import { createContext, use, useCallback, useMemo, useState, type ReactNode } from 'react';
import { initialWizardData, type WizardData } from './types';

interface WizardContextValue {
  readonly data: WizardData;
  readonly updateData: (patch: Partial<WizardData>) => void;
  /** Najdalji korak do kog je korisnik stigao; koraci iza njega nisu dostupni. */
  readonly furthestIndex: number;
  readonly markReached: (index: number) => void;
}

const WizardContext = createContext<WizardContextValue | null>(null);

export function WizardProvider({ children }: { readonly children: ReactNode }) {
  const [data, setData] = useState<WizardData>(initialWizardData);
  const [furthestIndex, setFurthestIndex] = useState(0);

  const updateData = useCallback((patch: Partial<WizardData>) => {
    setData((current) => ({ ...current, ...patch }));
  }, []);
  const markReached = useCallback((index: number) => {
    setFurthestIndex((current) => Math.max(current, index));
  }, []);

  const value = useMemo(
    () => ({ data, updateData, furthestIndex, markReached }),
    [data, updateData, furthestIndex, markReached],
  );
  return <WizardContext value={value}>{children}</WizardContext>;
}

export function useWizard(): WizardContextValue {
  const context = use(WizardContext);
  if (!context) {
    throw new Error('useWizard mora biti unutar WizardProvider-a.');
  }
  return context;
}
