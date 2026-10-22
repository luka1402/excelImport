import { zodResolver } from '@hookform/resolvers/zod';
import { useEffect } from 'react';
import { useForm, type FieldValues, type UseFormReturn } from 'react-hook-form';
import type { ZodType } from 'zod';
import { useWizard } from './WizardContext';
import type { WizardData } from './types';

/**
 * RHF forma koja se puni iz stanja carobnjaka i upisuje svaku izmenu nazad,
 * pa unos preživi odlazak na drugi korak. Sva polja moraju postojati u WizardData.
 */
export function useWizardForm<TValues extends FieldValues & Partial<WizardData>>(
  schema: ZodType<TValues>,
  pick: (data: WizardData) => TValues,
): UseFormReturn<TValues> {
  const { data, updateData } = useWizard();
  const form = useForm<TValues>({
    // zodResolver ima uži izlazni tip od RHF-a; vrednosti su ista struktura.
    resolver: zodResolver(schema as never),
    defaultValues: pick(data) as never,
    mode: 'onChange',
  });

  useEffect(() => {
    const subscription = form.watch((values) => {
      // RHF vraca DeepPartial, a mapiranje/konstante ovde nikad ne dolaze iz forme.
      updateData(values as Partial<WizardData>);
    });
    return () => {
      subscription.unsubscribe();
    };
  }, [form, updateData]);

  return form;
}
