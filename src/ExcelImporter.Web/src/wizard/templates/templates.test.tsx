import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { WizardProvider, useWizard } from '../WizardContext';
import type { WizardData } from '../types';
import { SaveTemplateSection } from './SaveTemplateSection';
import { TemplatePicker } from './TemplatePicker';

const summaries = [
  { id: 1, name: 'Prodaja', connectionId: 3, schemaName: 'dbo', tableName: 'Prodaja', updatedAt: '2026-10-20T10:00:00Z' },
  { id: 2, name: 'Zalihe', connectionId: 3, schemaName: 'dbo', tableName: 'Zalihe', updatedAt: '2026-10-21T10:00:00Z' },
];

const applyBody = {
  templateId: 1,
  isUsable: true,
  schemaIssues: [],
  hasColumnWarning: false,
  newColumns: [],
  missingColumns: [],
  mappings: [{ sourceColumn: 'Kupac', targetColumn: 'Kupac' }],
  rules: [{ type: 'defaultValue', version: 1, targetField: 'Status', value: 'X' }],
  constants: [],
};

const analysisBody = {
  sheets: [{ index: 0, name: 'List1', isHidden: false }],
  selectedSheetIndex: 0,
  previewRows: [['Kupac'], ['Ana']],
  previewColumnsTruncated: false,
  headerRowIndex: 0,
  headerChosenManually: false,
  columns: [{ index: 0, name: 'Kupac', type: 'Text', isEmpty: false, isMixed: false, nonEmptyCount: 1 }],
};

type Handler = (url: string, init?: RequestInit) => Response | undefined;

function stubFetch(handler: Handler) {
  const fetchMock = vi.fn<typeof fetch>((input, init) => {
    const url = typeof input === 'string' ? input : input instanceof URL ? input.href : input.url;
    return Promise.resolve(handler(url, init) ?? new Response(null, { status: 404 }));
  });
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

function Probe() {
  const { data } = useWizard();
  return (
    <pre data-testid="podaci">
      {JSON.stringify({ table: data.table, mapping: data.mapping, rules: data.rules.length, applied: data.appliedTemplate })}
    </pre>
  );
}

function renderUi(ui: React.ReactNode, initial: Partial<WizardData> = {}) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <WizardProvider initialData={{ fileId: 'f1', sheetIndex: 0, ...initial }}>
        {ui}
        <Probe />
      </WizardProvider>
    </QueryClientProvider>,
  );
}

const probe = () => JSON.parse(screen.getByTestId('podaci').textContent) as Record<string, unknown>;

describe('TemplatePicker', () => {
  it('prikazuje praznu listu kada nema šablona', async () => {
    stubFetch((url) => (url === '/api/templates' ? Response.json([]) : undefined));
    renderUi(<TemplatePicker />);
    expect(await screen.findByText(/Nema sačuvanih šablona/)).toBeInTheDocument();
  });

  it('prikazuje grešku učitavanja sa dugmetom za ponovni pokušaj', async () => {
    stubFetch(() => new Response(null, { status: 500 }));
    renderUi(<TemplatePicker />);
    expect(await screen.findByText('Šabloni nisu učitani.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Pokušaj ponovo/ })).toBeInTheDocument();
  });

  it('primenjuje šablon: popunjava odredište, mapiranje i pravila', async () => {
    const user = userEvent.setup();
    const fetchMock = stubFetch((url) => {
      if (url === '/api/templates') return Response.json(summaries);
      if (url.startsWith('/api/templates/1/apply')) return Response.json(applyBody);
      return undefined;
    });
    renderUi(<TemplatePicker />);

    await user.click(await screen.findByRole('button', { name: 'Primeni šablon Prodaja' }));

    expect(await screen.findByText(/Šablon „Prodaja" je primenjen/)).toBeInTheDocument();
    expect(probe()).toMatchObject({ table: 'Prodaja', mapping: { Kupac: { target: 'Kupac' } }, rules: 1 });
    expect(fetchMock).toHaveBeenCalledWith('/api/templates/1/apply?fileId=f1&sheet=0', expect.anything());
  });

  it('upozorava na razlike kolona i navodi nove i nedostajuće kolone', async () => {
    const user = userEvent.setup();
    stubFetch((url) => {
      if (url === '/api/templates') return Response.json(summaries);
      if (url.startsWith('/api/templates/1/apply')) {
        return Response.json({ ...applyBody, hasColumnWarning: true, newColumns: ['Nova'], missingColumns: ['Stara'] });
      }
      return undefined;
    });
    renderUi(<TemplatePicker />);

    await user.click(await screen.findByRole('button', { name: 'Primeni šablon Prodaja' }));

    expect(await screen.findByText(/Kolone fajla se razlikuju od šablona „Prodaja"/)).toBeInTheDocument();
    expect(screen.getByText(/Nove kolone \(nisu mapirane\): Nova/)).toBeInTheDocument();
    expect(screen.getByText(/Nedostaju|Kolone koje šablon očekuje, a nema ih u fajlu: Stara/)).toBeInTheDocument();
  });

  it('ne upozorava kada su kolone iste', async () => {
    const user = userEvent.setup();
    stubFetch((url) => {
      if (url === '/api/templates') return Response.json(summaries);
      if (url.startsWith('/api/templates/1/apply')) return Response.json(applyBody);
      return undefined;
    });
    renderUi(<TemplatePicker />);
    await user.click(await screen.findByRole('button', { name: 'Primeni šablon Prodaja' }));
    await screen.findByText(/je primenjen/);
    expect(screen.queryByText(/razlikuju/)).not.toBeInTheDocument();
  });

  it('ne primenjuje šablon koji ne odgovara ciljnoj bazi i prikazuje razloge', async () => {
    const user = userEvent.setup();
    stubFetch((url) => {
      if (url === '/api/templates') return Response.json(summaries);
      if (url.startsWith('/api/templates/1/apply')) {
        return Response.json({ ...applyBody, isUsable: false, schemaIssues: ['Tabela dbo.Prodaja ne postoji.'] });
      }
      return undefined;
    });
    renderUi(<TemplatePicker />);

    await user.click(await screen.findByRole('button', { name: 'Primeni šablon Prodaja' }));

    expect(await screen.findByText('Tabela dbo.Prodaja ne postoji.')).toBeInTheDocument();
    expect(probe()).toMatchObject({ table: '', applied: null });
  });

  it('prikazuje grešku kada primena ne uspe', async () => {
    const user = userEvent.setup();
    stubFetch((url) => (url === '/api/templates' ? Response.json(summaries) : undefined));
    renderUi(<TemplatePicker />);
    await user.click(await screen.findByRole('button', { name: 'Primeni šablon Prodaja' }));
    expect(await screen.findByText('Šablon nije primenjen. Pokušajte ponovo.')).toBeInTheDocument();
  });

  it('briše šablon tek posle potvrde i osvežava listu', async () => {
    const user = userEvent.setup();
    let templates = summaries;
    const fetchMock = stubFetch((url, init) => {
      if (url === '/api/templates') return Response.json(templates);
      if (url === '/api/templates/2' && init?.method === 'DELETE') {
        templates = summaries.slice(0, 1);
        return new Response(null, { status: 204 });
      }
      return undefined;
    });
    renderUi(<TemplatePicker />);

    await user.click(await screen.findByRole('button', { name: 'Obriši šablon Zalihe' }));
    expect(fetchMock).not.toHaveBeenCalledWith('/api/templates/2', expect.objectContaining({ method: 'DELETE' }));
    await user.click(screen.getByRole('button', { name: 'Da, obriši' }));

    await waitFor(() => {
      expect(screen.queryByText(/Zalihe/)).not.toBeInTheDocument();
    });
    expect(screen.getByText(/Prodaja/)).toBeInTheDocument();
  });

  it('otkazivanje brisanja ne briše šablon', async () => {
    const user = userEvent.setup();
    const fetchMock = stubFetch((url) => (url === '/api/templates' ? Response.json(summaries) : undefined));
    renderUi(<TemplatePicker />);

    await user.click(await screen.findByRole('button', { name: 'Obriši šablon Zalihe' }));
    await user.click(screen.getByRole('button', { name: 'Otkaži' }));

    expect(screen.getByRole('button', { name: 'Obriši šablon Zalihe' })).toBeInTheDocument();
    expect(fetchMock).not.toHaveBeenCalledWith('/api/templates/2', expect.anything());
  });

  it('prikazuje grešku kada brisanje ne uspe', async () => {
    const user = userEvent.setup();
    stubFetch((url) => (url === '/api/templates' ? Response.json(summaries) : undefined));
    renderUi(<TemplatePicker />);
    await user.click(await screen.findByRole('button', { name: 'Obriši šablon Zalihe' }));
    await user.click(screen.getByRole('button', { name: 'Da, obriši' }));
    expect(await screen.findByText('Šablon nije obrisan. Pokušajte ponovo.')).toBeInTheDocument();
  });
});

describe('SaveTemplateSection', () => {
  const seed: Partial<WizardData> = {
    connectionId: '3',
    tableSchema: 'dbo',
    table: 'Prodaja',
    sheet: 'List1',
    mapping: { Kupac: { target: 'Kupac', suggested: false } },
  };

  function analyzeHandler(url: string): Response | undefined {
    return url.startsWith('/api/files/f1/analyze') ? Response.json(analysisBody) : undefined;
  }

  it('traži naziv pre čuvanja', async () => {
    const user = userEvent.setup();
    const fetchMock = stubFetch(analyzeHandler);
    renderUi(<SaveTemplateSection />, seed);

    await user.click(screen.getByRole('button', { name: 'Sačuvaj kao šablon' }));

    expect(await screen.findByText('Unesite naziv šablona.')).toBeInTheDocument();
    expect(fetchMock).not.toHaveBeenCalledWith('/api/templates', expect.anything());
  });

  it('čuva šablon sa mapiranjem i kolonama fajla', async () => {
    const user = userEvent.setup();
    const fetchMock = stubFetch((url, init) => {
      if (url === '/api/templates' && init?.method === 'POST') return Response.json({ id: 9 }, { status: 201 });
      return analyzeHandler(url);
    });
    renderUi(<SaveTemplateSection />, seed);

    await user.type(screen.getByLabelText('Naziv šablona'), 'Moj šablon');
    await user.click(screen.getByRole('button', { name: 'Sačuvaj kao šablon' }));

    expect(await screen.findByText('Šablon „Moj šablon" je sačuvan.')).toBeInTheDocument();
    const post = fetchMock.mock.calls.find(([, init]) => init?.method === 'POST');
    expect(JSON.parse(post?.[1]?.body as string)).toMatchObject({
      name: 'Moj šablon',
      connectionId: 3,
      schemaName: 'dbo',
      tableName: 'Prodaja',
      sheetName: 'List1',
      headerRow: 0,
      sourceColumns: ['Kupac'],
      mappings: [{ sourceColumn: 'Kupac', targetColumn: 'Kupac' }],
    });
  });

  it('javlja da naziv već postoji', async () => {
    const user = userEvent.setup();
    stubFetch((url, init) => {
      if (url === '/api/templates' && init?.method === 'POST') {
        return Response.json({ title: 'Šablon sa imenom "Moj šablon" već postoji.' }, { status: 409 });
      }
      return analyzeHandler(url);
    });
    renderUi(<SaveTemplateSection />, seed);

    await user.type(screen.getByLabelText('Naziv šablona'), 'Moj šablon');
    await user.click(screen.getByRole('button', { name: 'Sačuvaj kao šablon' }));

    expect(await screen.findByText('Šablon sa imenom "Moj šablon" već postoji.')).toBeInTheDocument();
  });

  it('prikazuje opštu grešku kada čuvanje ne uspe', async () => {
    const user = userEvent.setup();
    stubFetch((url, init) => (url === '/api/templates' && init?.method === 'POST' ? new Response(null, { status: 500 }) : analyzeHandler(url)));
    renderUi(<SaveTemplateSection />, seed);

    await user.type(screen.getByLabelText('Naziv šablona'), 'Moj šablon');
    await user.click(screen.getByRole('button', { name: 'Sačuvaj kao šablon' }));

    expect(await screen.findByText('Šablon nije sačuvan. Pokušajte ponovo.')).toBeInTheDocument();
  });
});
