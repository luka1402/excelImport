import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { WizardProvider } from '../../WizardContext';
import { TargetStep } from './TargetStep';

const connections = [
  { id: 1, displayName: 'Demo baza' },
  { id: 2, displayName: 'Druga baza' },
];

const schemaBody = {
  tables: [
    {
      schema: 'dbo',
      name: 'Prodaja',
      columns: [
        { name: 'Id', ordinal: 1, dataType: 'int', isNullable: false, maxLength: null, isIdentity: true, isComputed: false },
        { name: 'Kupac', ordinal: 2, dataType: 'nvarchar', isNullable: false, maxLength: 100, isIdentity: false, isComputed: false },
        { name: 'GradId', ordinal: 3, dataType: 'int', isNullable: true, maxLength: null, isIdentity: false, isComputed: false },
      ],
      primaryKey: ['Id'],
      foreignKeys: [
        { name: 'FK_Prodaja_Grad', columns: ['GradId'], referencedSchema: 'dbo', referencedTable: 'Grad', referencedColumns: ['Id'] },
      ],
    },
    { schema: 'dbo', name: 'Grad', columns: [], primaryKey: [], foreignKeys: [] },
  ],
};

type Handler = () => Response | Promise<Response>;

function stubApi(routes: Record<string, Handler>) {
  const fetchMock = vi.fn<typeof fetch>((input) => {
    const url = typeof input === 'string' ? input : input instanceof URL ? input.href : input.url;
    const handler = routes[url];
    return handler ? Promise.resolve(handler()) : Promise.resolve(new Response(null, { status: 404 }));
  });
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

function renderStep() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <WizardProvider>
        <TargetStep />
      </WizardProvider>
    </QueryClientProvider>,
  );
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('TargetStep', () => {
  it('prikazuje učitavanje, pa listu baza sa prikaznim nazivima', async () => {
    stubApi({ '/api/connections': () => Response.json(connections) });
    renderStep();

    expect(screen.getByRole('status')).toHaveTextContent('Učitavam listu baza…');
    const select = await screen.findByLabelText('Baza');
    expect(within(select).getAllByRole('option').map((option) => option.textContent)).toEqual([
      '— Izaberite bazu —',
      'Demo baza',
      'Druga baza',
    ]);
  });

  it('po izboru baze učitava šemu i pokazuje tabele, a po izboru tabele detalje kolona', async () => {
    const user = userEvent.setup();
    const fetchMock = stubApi({
      '/api/connections': () => Response.json(connections),
      '/api/connections/1/schema': () => Response.json(schemaBody),
    });
    renderStep();

    await user.selectOptions(await screen.findByLabelText('Baza'), 'Demo baza');
    await user.selectOptions(await screen.findByLabelText('Tabela'), 'dbo.Prodaja');

    expect(fetchMock).toHaveBeenCalledWith('/api/connections/1/schema', expect.anything());
    const table = await screen.findByRole('table', { name: 'Kolone tabele dbo.Prodaja' });
    const rows = within(table).getAllByRole('row').slice(1);
    expect(rows.map((row) => within(row).getAllByRole('cell').map((cell) => cell.textContent))).toEqual([
      ['int', 'Ne (popunjava baza)', 'PK', '—'],
      ['nvarchar', 'Da', '—', '100'],
      ['int', 'Ne', 'FK → dbo.Grad', '—'],
    ]);
  });

  it('filtrira tabele pretragom i javlja kad nijedna ne odgovara', async () => {
    const user = userEvent.setup();
    stubApi({
      '/api/connections': () => Response.json(connections),
      '/api/connections/1/schema': () => Response.json(schemaBody),
    });
    renderStep();

    await user.selectOptions(await screen.findByLabelText('Baza'), 'Demo baza');
    const search = await screen.findByLabelText('Pretraga tabela');
    await user.type(search, 'gra');
    expect(within(screen.getByLabelText('Tabela')).getAllByRole('option').map((o) => o.textContent)).toEqual([
      '— Izaberite tabelu —',
      'dbo.Grad',
    ]);

    await user.clear(search);
    await user.type(search, 'xyz');
    expect(screen.getByText('Nijedna tabela ne odgovara pretrazi „xyz".')).toBeInTheDocument();
  });

  it('promena baze poništava izabranu tabelu', async () => {
    const user = userEvent.setup();
    stubApi({
      '/api/connections': () => Response.json(connections),
      '/api/connections/1/schema': () => Response.json(schemaBody),
      '/api/connections/2/schema': () => Response.json({ tables: [] }),
    });
    renderStep();

    await user.selectOptions(await screen.findByLabelText('Baza'), 'Demo baza');
    await user.selectOptions(await screen.findByLabelText('Tabela'), 'dbo.Prodaja');
    await screen.findByRole('table');
    await user.selectOptions(screen.getByLabelText('Baza'), 'Druga baza');

    expect(await screen.findByText('Baza ne sadrži nijednu tabelu u koju je dozvoljen uvoz.')).toBeInTheDocument();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
  });

  it('prikazuje poruku i ponovni pokušaj kada baza nije dostupna', async () => {
    const user = userEvent.setup();
    let available = false;
    stubApi({
      '/api/connections': () => Response.json(connections),
      '/api/connections/1/schema': () =>
        available ? Response.json(schemaBody) : Response.json({ title: 'Baza nedostupna', status: 503 }, { status: 503 }),
    });
    renderStep();

    await user.selectOptions(await screen.findByLabelText('Baza'), 'Demo baza');
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Baza trenutno nije dostupna. Proverite da je servis pokrenut i pokušajte ponovo.',
    );

    available = true;
    await user.click(screen.getByRole('button', { name: 'Pokušaj ponovo' }));
    expect(await screen.findByLabelText('Tabela')).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('prikazuje grešku kada lista baza ne može da se učita', async () => {
    stubApi({ '/api/connections': () => new Response(null, { status: 500 }) });
    renderStep();

    expect(await screen.findByRole('alert')).toHaveTextContent('Lista baza ne može da se učita.');
    expect(screen.getByRole('button', { name: 'Pokušaj ponovo' })).toBeInTheDocument();
  });

  it('prikazuje prazno stanje kada nema podešenih baza', async () => {
    stubApi({ '/api/connections': () => Response.json([]) });
    renderStep();

    expect(await screen.findByText('Nema podešenih baza. Obratite se administratoru.')).toBeInTheDocument();
    expect(screen.queryByLabelText('Baza')).not.toBeInTheDocument();
  });

  it('ne prikazuje connection string ni kada ga server pogrešno vrati u detaljima greške', async () => {
    stubApi({
      '/api/connections': () => Response.json(connections),
      '/api/connections/1/schema': () =>
        Response.json({ title: 'Server=x;Password=tajna', status: 500, detail: 'Server=x;Password=tajna' }, { status: 500 }),
    });
    const user = userEvent.setup();
    renderStep();

    await user.selectOptions(await screen.findByLabelText('Baza'), 'Demo baza');
    await screen.findByRole('alert');

    expect(document.body.textContent).not.toContain('tajna');
  });
});