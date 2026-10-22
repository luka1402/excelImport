import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { WizardProvider, useWizard } from '../../WizardContext';
import type { WizardData } from '../../types';
import { wizardSteps } from '../index';
import { RulesStep } from './RulesStep';

function targetColumn(name: string, dataType: string, maxLength: number | null = null) {
  return { name, ordinal: 1, dataType, isNullable: true, maxLength, isIdentity: false, isComputed: false };
}

const schemaBody = {
  tables: [
    {
      schema: 'dbo',
      name: 'Prodaja',
      columns: [
        targetColumn('Kupac', 'nvarchar', 100),
        targetColumn('Kolicina', 'int'),
        targetColumn('Status', 'nvarchar', 20),
        targetColumn('Nemapirana', 'nvarchar', 20),
      ],
      primaryKey: [],
      foreignKeys: [],
    },
  ],
};

const seed: Partial<WizardData> = {
  connectionId: '1',
  tableSchema: 'dbo',
  table: 'Prodaja',
  mappingKey: 'k',
  mappingComplete: true,
  mapping: {
    Kupac: { target: 'Kupac', suggested: false },
    Količina: { target: 'Kolicina', suggested: false },
    Status: { target: 'Status', suggested: false },
  },
};

function Probe() {
  const { data } = useWizard();
  return (
    <>
      <p data-testid="uredjuje">{String(data.rulesEditing)}</p>
      <p data-testid="broj">{data.rules.length}</p>
    </>
  );
}

function renderStep(initial: Partial<WizardData> = seed) {
  vi.stubGlobal(
    'fetch',
    vi.fn<typeof fetch>((input) => {
      const url = typeof input === 'string' ? input : input instanceof URL ? input.href : input.url;
      return Promise.resolve(url === '/api/connections/1/schema' ? Response.json(schemaBody) : new Response(null, { status: 404 }));
    }),
  );
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <WizardProvider initialData={initial}>
        <RulesStep />
        <Probe />
      </WizardProvider>
    </QueryClientProvider>,
  );
}

async function openColumn(user: ReturnType<typeof userEvent.setup>, name: string) {
  await user.click(await screen.findByRole('button', { name: new RegExp(`^${name} \\(`) }));
  await user.click(screen.getByRole('radio', { name: 'Da' }));
}

describe('RulesStep', () => {
  it('prikazuje samo mapirane kolone i pitanje o uslovu', async () => {
    renderStep();
    const nav = await screen.findByRole('navigation', { name: 'Kolone' });
    expect(within(nav).getAllByRole('button')).toHaveLength(3);
    expect(within(nav).queryByText(/Nemapirana/)).not.toBeInTheDocument();
    expect(screen.getByText('Da li postoji uslov?')).toBeInTheDocument();
    expect(screen.getByRole('radio', { name: 'Ne' })).toBeChecked();
  });

  it('prazna kolona nudi dodavanje pravila uz poruku da pravila nisu obavezna', async () => {
    const user = userEvent.setup();
    renderStep();
    await openColumn(user, 'Kupac');
    expect(screen.getByText(/Pravila nisu obavezna/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Dodaj pravilo/ })).toBeInTheDocument();
  });

  it('dodaje transformaciju, menja je i briše uz potvrdu', async () => {
    const user = userEvent.setup();
    renderStep();
    await openColumn(user, 'Kupac');

    await user.selectOptions(screen.getByLabelText('Vrsta pravila'), 'Transformacija');
    await user.click(screen.getByRole('button', { name: /Dodaj pravilo/ }));
    expect(screen.getByTestId('uredjuje')).toHaveTextContent('true');
    await user.click(screen.getByRole('button', { name: 'Sačuvaj pravilo' }));
    expect(screen.getByText('Transformacija: Ukloni razmake')).toBeInTheDocument();
    expect(screen.getByTestId('uredjuje')).toHaveTextContent('false');
    expect(screen.getByRole('radio', { name: 'Ne' })).toBeDisabled();

    await user.click(screen.getByRole('button', { name: 'Izmeni' }));
    await user.selectOptions(screen.getByLabelText('Transformacija'), 'Velika slova');
    await user.click(screen.getByRole('button', { name: 'Sačuvaj pravilo' }));
    expect(screen.getByText('Transformacija: Velika slova')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /^Ukloni pravilo/ }));
    expect(screen.getByText('Obrisati pravilo?')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Otkaži' }));
    expect(screen.getByTestId('broj')).toHaveTextContent('1');

    await user.click(screen.getByRole('button', { name: /^Ukloni pravilo/ }));
    await user.click(screen.getByRole('button', { name: 'Obriši' }));
    expect(screen.getByTestId('broj')).toHaveTextContent('0');
    expect(screen.getByText(/Pravila nisu obavezna/)).toBeInTheDocument();
  });

  it('format datuma i decimalni separator imaju validaciju na srpskom', async () => {
    const user = userEvent.setup();
    renderStep();
    await openColumn(user, 'Kupac');
    await user.selectOptions(screen.getByLabelText('Vrsta pravila'), 'Transformacija');
    await user.click(screen.getByRole('button', { name: /Dodaj pravilo/ }));

    await user.selectOptions(screen.getByLabelText('Transformacija'), 'Format datuma');
    await user.click(screen.getByRole('button', { name: 'Sačuvaj pravilo' }));
    expect(screen.getByRole('alert')).toHaveTextContent('Format datuma nije ispravan.');
    await user.type(screen.getByLabelText(/^Format datuma/), 'dd.MM.yyyy');

    await user.selectOptions(screen.getByLabelText('Transformacija'), 'Decimalni separator');
    await user.click(screen.getByRole('button', { name: 'Sačuvaj pravilo' }));
    expect(screen.getByRole('alert')).toHaveTextContent('Izaberite decimalni separator.');
    await user.click(screen.getByRole('radio', { name: 'zarez (,)' }));
    await user.click(screen.getByRole('button', { name: 'Sačuvaj pravilo' }));
    expect(screen.getByText('Transformacija: Decimalni separator (,)')).toBeInTheDocument();
  });

  it('podrazumevana vrednost je obavezna i proverava tip kolone', async () => {
    const user = userEvent.setup();
    renderStep();
    await openColumn(user, 'Kolicina');
    await user.selectOptions(screen.getByLabelText('Vrsta pravila'), 'Podrazumevana vrednost');
    await user.click(screen.getByRole('button', { name: /Dodaj pravilo/ }));

    await user.click(screen.getByRole('button', { name: 'Sačuvaj pravilo' }));
    expect(screen.getByRole('alert')).toHaveTextContent('Unesite vrednost.');
    await user.type(screen.getByLabelText('Ako je ćelija prazna upiši:'), 'abc');
    expect(screen.getByRole('alert')).toHaveTextContent('Vrednost mora biti broj.');
    await user.clear(screen.getByLabelText('Ako je ćelija prazna upiši:'));
    await user.type(screen.getByLabelText('Ako je ćelija prazna upiši:'), '0');
    await user.click(screen.getByRole('button', { name: 'Sačuvaj pravilo' }));
    expect(screen.getByText('Podrazumevana vrednost: „0"')).toBeInTheDocument();
  });

  it('uslovna vrednost nudi sve operatore, više uslova i sakriva vrednost za je prazno', async () => {
    const user = userEvent.setup();
    renderStep();
    await openColumn(user, 'Status');
    await user.click(screen.getByRole('button', { name: /Dodaj pravilo/ }));

    const operator = screen.getByLabelText('Operator uslova 1');
    expect(within(operator).getAllByRole('option').map((option) => option.textContent)).toEqual([
      'je jednako',
      'nije jednako',
      'veće od',
      'manje od',
      'veće ili jednako',
      'manje ili jednako',
      'sadrži',
      'počinje sa',
      'je prazno',
      'nije prazno',
    ]);

    await user.click(screen.getByRole('button', { name: 'Sačuvaj pravilo' }));
    expect(screen.getAllByRole('alert').map((alert) => alert.textContent)).toEqual(
      expect.arrayContaining(['Unesite vrednost.']),
    );

    await user.selectOptions(operator, 'je prazno');
    expect(screen.queryByLabelText('Vrednost uslova 1')).not.toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /^\+ I još uslov/ }));
    await user.selectOptions(screen.getByLabelText('Kolona uslova 2'), 'Kupac');
    await user.type(screen.getByLabelText('Vrednost uslova 2'), 'Marko');
    await user.type(screen.getByLabelText('Vrednost kada su uslovi ispunjeni'), '1');
    await user.type(screen.getByLabelText(/^Vrednost kada uslovi nisu ispunjeni/), '0');
    await user.click(screen.getByRole('button', { name: 'Sačuvaj pravilo' }));

    expect(screen.getByText('Ako Status je prazno i Kupac je jednako „Marko" onda „1", inače „0"')).toBeInTheDocument();
  });

  it('otkazivanje forme ne dodaje pravilo i vraća mogućnost nastavka', async () => {
    const user = userEvent.setup();
    renderStep();
    await openColumn(user, 'Kupac');
    await user.click(screen.getByRole('button', { name: /Dodaj pravilo/ }));
    await user.click(screen.getByRole('button', { name: 'Otkaži' }));
    expect(screen.getByTestId('broj')).toHaveTextContent('0');
    expect(screen.getByTestId('uredjuje')).toHaveTextContent('false');
  });

  it('korak je u čarobnjaku blokiran dok je forma otvorena', () => {
    const step = wizardSteps.find((candidate) => candidate.slug === 'pravila');
    expect(step?.isValid?.({ ...(seed as WizardData), rulesEditing: true })).toBe(false);
    expect(step?.isValid?.({ ...(seed as WizardData), rulesEditing: false })).toBe(true);
  });

  it('bez kompletnog mapiranja traži povratak na mapiranje', () => {
    renderStep({ ...seed, mappingComplete: false });
    expect(screen.getByText('Nijedna kolona nije mapirana. Vratite se na korak 4.')).toBeInTheDocument();
  });
});
