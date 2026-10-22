import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { WizardProvider, useWizard } from '../wizard/WizardContext';
import type { WizardData } from '../wizard/types';
import { RulesStep } from '../wizard/steps/rules/RulesStep';

// COA-59: ručni prolaz forme „Uslovi i pravila" sa pogrešnim unosom, automatizovan.

function col(name: string, dataType: string, maxLength: number | null = null) {
  return { name, ordinal: 1, dataType, isNullable: true, maxLength, isIdentity: false, isComputed: false };
}

const schemaBody = {
  tables: [
    {
      schema: 'dbo',
      name: 'Prodaja',
      columns: [col('Kupac', 'nvarchar', 5), col('Kolicina', 'int'), col('Cena', 'decimal'), col('Datum', 'datetime')],
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
    Cena: { target: 'Cena', suggested: false },
    Datum: { target: 'Datum', suggested: false },
  },
};

function Probe() {
  const { data } = useWizard();
  return <p data-testid="broj">{data.rules.length}</p>;
}

function renderStep() {
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
      <WizardProvider initialData={seed}>
        <RulesStep />
        <Probe />
      </WizardProvider>
    </QueryClientProvider>,
  );
}

async function openForm(user: ReturnType<typeof userEvent.setup>, column: string, ruleType: string) {
  await user.click(await screen.findByRole('button', { name: new RegExp(`^${column} \\(`) }));
  await user.click(screen.getByRole('radio', { name: 'Da' }));
  await user.selectOptions(screen.getByLabelText('Vrsta pravila'), ruleType);
  await user.click(screen.getByRole('button', { name: /Dodaj pravilo/ }));
}

const save = () => screen.getByRole('button', { name: 'Sačuvaj pravilo' });

describe('QA: pogrešan unos u formama pravila', () => {
  it('podrazumevana vrednost: samo razmaci, predugačak tekst, decimalni broj za int', async () => {
    const user = userEvent.setup();
    renderStep();
    await openForm(user, 'Kupac', 'Podrazumevana vrednost');
    const input = screen.getByLabelText('Ako je ćelija prazna upiši:');

    await user.type(input, '   ');
    await user.click(save());
    expect(screen.getByRole('alert')).toHaveTextContent('Unesite vrednost.');

    await user.clear(input);
    await user.type(input, 'predugacko');
    expect(screen.getByRole('alert')).toBeInTheDocument();
    expect(screen.getByTestId('broj')).toHaveTextContent('0');

    await user.clear(input);
    await user.type(input, 'Šćž😀');
    await user.click(save());
    expect(screen.getByTestId('broj')).toHaveTextContent('1');
  });

  it('int kolona odbija 1.5, 1e3 i preveliki broj; broj sa zarezom/tačkom za decimal', async () => {
    const user = userEvent.setup();
    renderStep();
    await openForm(user, 'Kolicina', 'Podrazumevana vrednost');
    await user.click(save());
    const input = screen.getByLabelText('Ako je ćelija prazna upiši:');
    for (const bad of ['1.5', '1e3', '--1', '١٢٣', '0x10']) {
      await user.clear(input);
      await user.type(input, bad);
      expect(screen.getByRole('alert'), `vrednost ${bad} treba da bude odbijena`).toHaveTextContent('Vrednost mora biti broj.');
    }
    await user.click(save());
    expect(screen.getByTestId('broj')).toHaveTextContent('0');
  });

  // Poznat defekt (prijavljen u COA-59): opseg tipa se ne proverava. Kada se ispravi, ukloniti `.fails`.
  it.fails('int kolona odbija vrednost van opsega tipa (2147483648)', async () => {
    const user = userEvent.setup();
    renderStep();
    await openForm(user, 'Kolicina', 'Podrazumevana vrednost');
    await user.type(screen.getByLabelText('Ako je ćelija prazna upiši:'), '2147483648');
    await user.click(save());
    expect(screen.getByRole('alert')).toBeInTheDocument();
  });

  it('XSS tekst se prikazuje kao običan tekst', async () => {
    const user = userEvent.setup();
    const { container } = renderStep();
    await openForm(user, 'Kupac', 'Podrazumevana vrednost');
    await user.type(screen.getByLabelText('Ako je ćelija prazna upiši:'), '<b>x');
    await user.click(save());
    expect(container.querySelector('b')).toBeNull();
    expect(screen.getByText(/<b>x/)).toBeInTheDocument();
  });

  it('datum kolona odbija tekst kao vrednost; format datuma odbija nevažeće tokene', async () => {
    const user = userEvent.setup();
    renderStep();
    await openForm(user, 'Datum', 'Podrazumevana vrednost');
    await user.type(screen.getByLabelText('Ako je ćelija prazna upiši:'), 'sutra');
    await user.click(save());
    expect(screen.getByRole('alert')).toHaveTextContent('Vrednost mora biti ispravan datum.');
    await user.click(screen.getByRole('button', { name: 'Otkaži' }));

    await user.selectOptions(screen.getByLabelText('Vrsta pravila'), 'Transformacija');
    await user.click(screen.getByRole('button', { name: /Dodaj pravilo/ }));
    await user.selectOptions(screen.getByLabelText('Transformacija'), 'Format datuma');
    for (const bad of ['   ', 'abc', 'DROP TABLE', '<script>', 'HH:mm']) {
      const format = screen.getByLabelText(/^Format datuma/);
      await user.clear(format);
      await user.type(format, bad);
      await user.click(save());
      expect(screen.getByRole('alert'), `format „${bad}" treba da bude odbijen`).toHaveTextContent('Format datuma nije ispravan.');
    }
    expect(screen.getByTestId('broj')).toHaveTextContent('0');
  });

  it('uslov: prazna kolona, operator sa tekstom na brojčanoj koloni i vrednost kada se operator vrati', async () => {
    const user = userEvent.setup();
    renderStep();
    await openForm(user, 'Kupac', 'Uslovna vrednost');
    await user.selectOptions(screen.getByLabelText('Kolona uslova 1'), '—');
    await user.type(screen.getByLabelText('Vrednost uslova 1'), 'x');
    await user.type(screen.getByLabelText('Vrednost kada su uslovi ispunjeni'), 'a');
    await user.click(save());
    expect(screen.getAllByRole('alert').map((a) => a.textContent)).toContain('Izaberite kolonu.');

    await user.selectOptions(screen.getByLabelText('Kolona uslova 1'), 'Kolicina');
    await user.selectOptions(screen.getByLabelText('Operator uslova 1'), 'sadrži');
    await user.clear(screen.getByLabelText('Vrednost uslova 1'));
    await user.type(screen.getByLabelText('Vrednost uslova 1'), 'abc');
    await user.click(save());
    expect(screen.getByTestId('broj')).toHaveTextContent('1');
  });

  it('vrednost „u suprotnom" se proverava po tipu, a prazna je dozvoljena', async () => {
    const user = userEvent.setup();
    renderStep();
    await openForm(user, 'Kolicina', 'Uslovna vrednost');
    await user.selectOptions(screen.getByLabelText('Kolona uslova 1'), 'Kupac');
    await user.type(screen.getByLabelText('Vrednost uslova 1'), 'x');
    await user.type(screen.getByLabelText('Vrednost kada su uslovi ispunjeni'), '1');
    await user.type(screen.getByLabelText(/^Vrednost kada uslovi nisu ispunjeni/), 'abc');
    await user.click(save());
    expect(screen.getByRole('alert')).toHaveTextContent('Vrednost mora biti broj.');
    await user.clear(screen.getByLabelText(/^Vrednost kada uslovi nisu ispunjeni/));
    await user.click(save());
    expect(screen.getByTestId('broj')).toHaveTextContent('1');
  });

  it('greške imaju aria-invalid i opis; polja imaju labele (tastatura/pristupačnost)', async () => {
    const user = userEvent.setup();
    renderStep();
    await openForm(user, 'Kupac', 'Podrazumevana vrednost');
    await user.click(save());
    const input = screen.getByLabelText('Ako je ćelija prazna upiši:');
    expect(input).toHaveAttribute('aria-invalid', 'true');
    expect(input).toHaveAccessibleDescription('Unesite vrednost.');
  });

  it('Enter u polju šalje formu i validira', async () => {
    const user = userEvent.setup();
    renderStep();
    await openForm(user, 'Kupac', 'Podrazumevana vrednost');
    await user.type(screen.getByLabelText('Ako je ćelija prazna upiši:'), '{Enter}');
    expect(screen.getByRole('alert')).toHaveTextContent('Unesite vrednost.');
    expect(screen.getByTestId('broj')).toHaveTextContent('0');
  });
});

