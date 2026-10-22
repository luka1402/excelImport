import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { WizardProvider, useWizard } from '../../WizardContext';
import type { WizardData } from '../../types';
import { MappingStep } from './MappingStep';

const fileId = '3f2504e0-4f89-11d3-9a0c-0305e82c3301';

function column(index: number, name: string, type: string, nonEmptyCount = 3) {
  return { index, name, type, isEmpty: false, isMixed: false, nonEmptyCount };
}

function analysisBody(overrides: { readonly columns?: readonly unknown[]; readonly previewRows?: readonly unknown[] } = {}) {
  return {
    sheets: [{ index: 0, name: 'List1', isHidden: false }],
    selectedSheetIndex: 0,
    previewRows: overrides.previewRows ?? [
      ['Kupac', 'Količina', 'Opis'],
      ['Marko', '2', 'a'],
    ],
    previewColumnsTruncated: false,
    headerRowIndex: 0,
    headerChosenManually: false,
    columns: overrides.columns ?? [column(0, 'Kupac', 'Text'), column(1, 'Količina', 'WholeNumber'), column(2, 'Opis', 'Text')],
  };
}

function targetColumn(name: string, dataType: string, isNullable: boolean, maxLength: number | null = null, isIdentity = false) {
  return { name, ordinal: 1, dataType, isNullable, maxLength, isIdentity, isComputed: false };
}

const schemaBody = {
  tables: [
    {
      schema: 'dbo',
      name: 'Prodaja',
      columns: [
        targetColumn('Id', 'int', false, null, true),
        targetColumn('Kupac', 'nvarchar', false, 100),
        targetColumn('Kolicina', 'int', true),
        targetColumn('GradId', 'int', true),
        targetColumn('Status', 'nvarchar', false, 20),
        targetColumn('Datum', 'datetime', true),
      ],
      primaryKey: ['Id'],
      foreignKeys: [],
    },
  ],
};

const seed: Partial<WizardData> = {
  fileId,
  connectionId: '1',
  tableSchema: 'dbo',
  table: 'Prodaja',
  analysisUsable: true,
};

function stubApi(analysis: () => Response = () => Response.json(analysisBody())) {
  const routes: Record<string, () => Response> = {
    [`/api/files/${fileId}/analyze`]: analysis,
    '/api/connections/1/schema': () => Response.json(schemaBody),
  };
  vi.stubGlobal(
    'fetch',
    vi.fn<typeof fetch>((input) => {
      const url = typeof input === 'string' ? input : input instanceof URL ? input.href : input.url;
      const handler = routes[url];
      return Promise.resolve(handler ? handler() : new Response(null, { status: 404 }));
    }),
  );
}

function Probe() {
  const { data } = useWizard();
  return <p data-testid="stanje">{data.mappingComplete ? 'kompletno' : 'nekompletno'}</p>;
}

function renderStep() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <WizardProvider initialData={seed}>
        <MappingStep />
        <Probe />
      </WizardProvider>
    </QueryClientProvider>,
  );
}

const targetSelect = (name: string) => screen.findByRole('combobox', { name: `Ciljna kolona za ${name}` });

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('MappingStep', () => {
  it('unapred popunjava predloge (bez obzira na dijakritike) i označava ih', async () => {
    stubApi();
    renderStep();

    expect(screen.getByText('Pripremam predlog mapiranja…')).toBeInTheDocument();
    expect(await targetSelect('Kupac')).toHaveValue('Kupac');
    expect(await targetSelect('Količina')).toHaveValue('Kolicina');
    expect(await targetSelect('Opis')).toHaveValue('');
    expect(screen.getByText(/Predloženo mapiranje za 2 od 3 kolona\./)).toBeInTheDocument();
    expect(screen.getAllByText('Predlog')).toHaveLength(2);
  });

  it('„Prihvati sve predloge" uklanja oznake predloga', async () => {
    const user = userEvent.setup();
    stubApi();
    renderStep();

    await user.click(await screen.findByRole('button', { name: 'Prihvati sve predloge' }));

    expect(screen.queryByText('Predlog')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Prihvati sve predloge' })).not.toBeInTheDocument();
    expect(await targetSelect('Kupac')).toHaveValue('Kupac');
  });

  it('ručna izmena predloga skida njegovu oznaku, a zauzeta ciljna kolona je onemogućena', async () => {
    const user = userEvent.setup();
    stubApi();
    renderStep();

    const opis = await targetSelect('Opis');
    expect(within(opis).getByRole('option', { name: /Kupac .*već mapirano/ })).toBeDisabled();

    await user.selectOptions(await targetSelect('Kupac'), '');
    expect(screen.getAllByText('Predlog')).toHaveLength(1);
    expect(within(opis).getByRole('option', { name: /Kupac \(nvarchar 100\)$/ })).toBeEnabled();
  });

  it('prikazuje upozorenje o tipu uz kolonu', async () => {
    const user = userEvent.setup();
    stubApi();
    renderStep();

    await user.selectOptions(await targetSelect('Opis'), 'GradId');

    expect(screen.getByText('Tip se ne poklapa: Excel „Tekst", baza „int".')).toBeInTheDocument();
  });

  it('upozorava kada vrednost iz pregleda ne staje u ciljnu kolonu', async () => {
    stubApi(() =>
      Response.json(
        analysisBody({
          previewRows: [
            ['Kupac', 'Količina', 'Opis'],
            ['x'.repeat(101), '2', 'a'],
          ],
        }),
      ),
    );
    renderStep();

    await targetSelect('Kupac');
    expect(screen.getByText('Tekst može biti duži od dozvoljenih 100 znakova.')).toBeInTheDocument();
  });

  it('obavezna nemapirana kolona upozorava i blokira nastavak dok se ne unese konstanta', async () => {
    const user = userEvent.setup();
    stubApi();
    renderStep();

    await targetSelect('Kupac');
    expect(screen.getByText('Obavezna kolona „Status" nije mapirana. Mapirajte je ili unesite konstantu.')).toBeInTheDocument();
    expect(screen.getByTestId('stanje')).toHaveTextContent('nekompletno');

    await user.type(screen.getByRole('textbox', { name: 'Konstanta za kolonu Status' }), 'Aktivan');

    expect(screen.queryByText(/Obavezna kolona „Status"/)).not.toBeInTheDocument();
    expect(screen.getByTestId('stanje')).toHaveTextContent('kompletno');
  });

  it('obavezna kolona se može pokriti i mapiranjem iz fajla', async () => {
    const user = userEvent.setup();
    stubApi();
    renderStep();

    await user.selectOptions(await targetSelect('Opis'), 'Status');

    expect(screen.getByTestId('stanje')).toHaveTextContent('kompletno');
    expect(screen.queryByRole('textbox', { name: 'Konstanta za kolonu Status' })).not.toBeInTheDocument();
  });

  it('konstanta pogrešnog tipa blokira nastavak', async () => {
    const user = userEvent.setup();
    stubApi();
    renderStep();

    await user.type(await screen.findByRole('textbox', { name: 'Konstanta za kolonu Status' }), 'Aktivan');
    await user.type(screen.getByRole('textbox', { name: 'Konstanta za kolonu GradId' }), 'abc');

    expect(screen.getByRole('alert')).toHaveTextContent('Vrednost ne odgovara tipu kolone (int).');
    expect(screen.getByTestId('stanje')).toHaveTextContent('nekompletno');
  });

  it('za kolonu datuma nudi „Današnji datum/vreme"', async () => {
    const user = userEvent.setup();
    stubApi();
    renderStep();

    await user.click(await screen.findByRole('checkbox', { name: 'Današnji datum/vreme za kolonu Datum' }));

    expect(screen.getByRole('textbox', { name: 'Konstanta za kolonu Datum' })).toBeDisabled();
    expect(screen.queryByRole('checkbox', { name: /kolonu GradId/ })).not.toBeInTheDocument();
  });

  it('nijedna mapirana kolona poručuje korisniku šta da uradi', async () => {
    const user = userEvent.setup();
    stubApi();
    renderStep();

    await user.selectOptions(await targetSelect('Kupac'), '');
    await user.selectOptions(await targetSelect('Količina'), '');

    expect(screen.getByText('Nijedna kolona nije povezana. Prihvatite predlog ili izaberite ciljne kolone.')).toBeInTheDocument();
  });

  it('greška pri učitavanju nudi ponovni pokušaj', async () => {
    const user = userEvent.setup();
    let failing = true;
    stubApi(() => (failing ? new Response(null, { status: 500 }) : Response.json(analysisBody())));
    renderStep();

    expect(await screen.findByRole('alert')).toHaveTextContent('Predlog mapiranja nije mogao da se napravi.');
    failing = false;
    await user.click(screen.getByRole('button', { name: 'Pokušaj ponovo' }));

    expect(await targetSelect('Kupac')).toHaveValue('Kupac');
  });

  it('fajl bez kolona prikazuje prazno stanje', async () => {
    stubApi(() => Response.json(analysisBody({ columns: [] })));
    renderStep();

    expect(await screen.findByText('U fajlu nema kolona za mapiranje. Vratite se na korak 2.')).toBeInTheDocument();
  });
});
