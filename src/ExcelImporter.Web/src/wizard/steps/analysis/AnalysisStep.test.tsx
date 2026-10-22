import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useEffect, type ReactNode } from 'react';
import { MemoryRouter } from 'react-router';
import { ApiError } from '../../../api/apiError';
import * as filesApi from '../../../api/files';
import { WizardProvider, useWizard } from '../../WizardContext';
import { AnalysisStep } from './AnalysisStep';
import { columnLetter, columnWarnings } from './columnInfo';

vi.mock('../../../api/files', () => ({ analyzeFile: vi.fn() }));

const analyzeFile = vi.mocked(filesApi.analyzeFile);

function column(index: number, name: string, overrides: Partial<filesApi.AnalyzedColumn> = {}): filesApi.AnalyzedColumn {
  return { index, name, type: 'Text', isEmpty: false, isMixed: false, nonEmptyCount: 2, ...overrides };
}

function analysis(overrides: Partial<filesApi.FileAnalysis> = {}): filesApi.FileAnalysis {
  return {
    sheets: [
      { index: 0, name: 'Prodaja', isHidden: false },
      { index: 1, name: 'Prazan', isHidden: false },
    ],
    selectedSheetIndex: 0,
    previewRows: [
      ['Kupac', 'Količina', 'Datum'],
      ['Petrović d.o.o.', '12', '03.02.2026'],
      ['Jović', null, '04.02.2026'],
    ],
    previewColumnsTruncated: false,
    headerRowIndex: 0,
    headerChosenManually: false,
    columns: [
      column(0, 'Kupac'),
      column(1, 'Količina', { type: 'WholeNumber', nonEmptyCount: 1 }),
      column(2, 'Datum', { type: 'Date' }),
    ],
    ...overrides,
  };
}

function Seed({ children }: { readonly children: ReactNode }) {
  const { data, updateData } = useWizard();
  useEffect(() => {
    updateData({ fileId: 'file-1', fileName: 'prodaja.xlsx' });
  }, [updateData]);
  return data.fileId === '' ? null : children;
}

function Probe() {
  const { data } = useWizard();
  return <span data-testid="data">{JSON.stringify({ usable: data.analysisUsable, sheet: data.sheet })}</span>;
}

function renderStep() {
  return render(
    <MemoryRouter>
      <WizardProvider>
        <Seed>
          <AnalysisStep />
          <Probe />
        </Seed>
      </WizardProvider>
    </MemoryRouter>,
  );
}

beforeEach(() => {
  analyzeFile.mockReset();
});

describe('AnalysisStep', () => {
  it('prikazuje učitavanje, pa kolone sa tipovima i pregled redova', async () => {
    analyzeFile.mockResolvedValue(analysis());
    renderStep();

    expect(screen.getByRole('status')).toHaveTextContent('Analiziram fajl…');

    const columns = await screen.findByRole('heading', { name: 'Kolone' });
    const table = within(columns.closest('section') as HTMLElement).getByRole('table');
    const rows = within(table).getAllByRole('row');
    expect(rows).toHaveLength(4);
    expect(within(rows[1] as HTMLElement).getAllByRole('cell').map((cell) => cell.textContent)).toEqual([
      'Kupac',
      'Tekst',
      '0 %',
    ]);
    expect(within(rows[2] as HTMLElement).getAllByRole('cell').map((cell) => cell.textContent)).toEqual([
      'Količina',
      'Ceo broj',
      '50 %',
    ]);
    expect(within(rows[3] as HTMLElement).getByText('Datum', { selector: 'td:nth-of-type(2)' })).toBeInTheDocument();

    expect(screen.getByRole('heading', { name: 'Pregled prvih 20 redova' })).toBeInTheDocument();
    expect(screen.getByText('Petrović d.o.o.')).toBeInTheDocument();
    expect(screen.getByText('Header je prepoznat u redu 1.')).toBeInTheDocument();
    await waitFor(() => {
      expect(screen.getByTestId('data')).toHaveTextContent('"usable":true');
    });
    expect(screen.getByTestId('data')).toHaveTextContent('"sheet":"Prodaja"');
  });

  it('promena sheet-a ponovo učitava analizu za izabrani sheet', async () => {
    const user = userEvent.setup();
    analyzeFile.mockResolvedValue(analysis());
    renderStep();
    await screen.findByRole('heading', { name: 'Kolone' });

    analyzeFile.mockResolvedValue(analysis({ selectedSheetIndex: 1, columns: [column(0, 'Šifra')] }));
    await user.selectOptions(screen.getByLabelText('Sheet'), 'Prazan');

    expect(await screen.findByText('Šifra')).toBeInTheDocument();
    expect(analyzeFile).toHaveBeenLastCalledWith(
      { fileId: 'file-1', sheetIndex: 1, headerRowIndex: null },
      expect.any(AbortSignal),
    );
  });

  it('ručna promena header reda šalje 0-based indeks i može da se vrati na automatski', async () => {
    const user = userEvent.setup();
    analyzeFile.mockResolvedValue(analysis());
    renderStep();
    const input = await screen.findByLabelText(/Red sa nazivima kolona/);

    analyzeFile.mockResolvedValue(analysis({ headerRowIndex: 2, headerChosenManually: true }));
    await user.clear(input);
    await user.type(input, '3');
    await user.click(screen.getByRole('button', { name: 'Promeni red' }));

    expect(await screen.findByText('Red sa nazivima kolona je izabran ručno.')).toBeInTheDocument();
    expect(analyzeFile).toHaveBeenLastCalledWith(
      { fileId: 'file-1', sheetIndex: null, headerRowIndex: 2 },
      expect.any(AbortSignal),
    );

    analyzeFile.mockResolvedValue(analysis());
    await user.click(screen.getByRole('button', { name: 'Vrati automatsko prepoznavanje' }));
    expect(await screen.findByText('Header je prepoznat u redu 1.')).toBeInTheDocument();
  });

  it('odbija broj reda van opsega bez poziva servera', async () => {
    const user = userEvent.setup();
    analyzeFile.mockResolvedValue(analysis());
    renderStep();
    const input = await screen.findByLabelText(/Red sa nazivima kolona/);
    const callsBefore = analyzeFile.mock.calls.length;

    await user.clear(input);
    await user.type(input, '21');
    await user.click(screen.getByRole('button', { name: 'Promeni red' }));

    expect(screen.getByRole('alert')).toHaveTextContent('Unesite broj reda od 1 do 20.');
    expect(input).toHaveAttribute('aria-invalid', 'true');
    expect(analyzeFile).toHaveBeenCalledTimes(callsBefore);
  });

  it('greška analize prikazuje poruku i Pokušaj ponovo ponavlja zahtev', async () => {
    const user = userEvent.setup();
    analyzeFile.mockRejectedValueOnce(new ApiError(400, { title: 'interno' }));
    renderStep();

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Analiza fajla nije uspela. Pokušajte ponovo ili izaberite drugi fajl.',
    );
    expect(screen.getByRole('button', { name: 'Izaberi drugi fajl' })).toBeInTheDocument();
    expect(screen.getByTestId('data')).toHaveTextContent('"usable":false');

    analyzeFile.mockResolvedValue(analysis());
    await user.click(screen.getByRole('button', { name: 'Pokušaj ponovo' }));

    expect(await screen.findByRole('heading', { name: 'Kolone' })).toBeInTheDocument();
  });

  it('prazan sheet prikazuje poruku, zadržava izbor sheet-a i ne dozvoljava nastavak', async () => {
    analyzeFile.mockResolvedValue(
      analysis({ previewRows: [], headerRowIndex: null, columns: [column(0, 'Kolona 1', { isEmpty: true, nonEmptyCount: 0 })] }),
    );
    renderStep();

    expect(await screen.findByText('Izabrani sheet je prazan. Izaberite drugi sheet.')).toBeInTheDocument();
    expect(screen.getByLabelText('Sheet')).toBeInTheDocument();
    expect(screen.getByTestId('data')).toHaveTextContent('"usable":false');
  });

  it('fajl bez sheet-ova prikazuje „Fajl ne sadrži podatke."', async () => {
    analyzeFile.mockResolvedValue(analysis({ sheets: [], columns: [], previewRows: [], headerRowIndex: null }));
    renderStep();

    expect(await screen.findByText('Fajl ne sadrži podatke.')).toBeInTheDocument();
  });

  it('upozorava na ponovljene i neimenovane kolone', async () => {
    analyzeFile.mockResolvedValue(
      analysis({ columns: [column(0, 'Cena'), column(1, ''), column(2, 'cena')] }),
    );
    renderStep();

    expect(await screen.findByText(/Naziv kolone se ponavlja: „Cena" \(kolone A, C\)\./)).toBeInTheDocument();
    expect(screen.getByText(/Kolona B nema naziv\./)).toBeInTheDocument();
  });

  it('mešane i prazne kolone imaju poseban opis tipa', async () => {
    analyzeFile.mockResolvedValue(
      analysis({
        columns: [column(0, 'A', { isMixed: true }), column(1, 'B', { isEmpty: true, nonEmptyCount: 0 }), column(2, 'C')],
      }),
    );
    renderStep();

    expect(await screen.findByText('Tekst (mešani tipovi)')).toBeInTheDocument();
    expect(screen.getByText('Tekst (prazna kolona)')).toBeInTheDocument();
  });
});

describe('columnInfo', () => {
  it('columnLetter prati Excel oznake', () => {
    expect([0, 25, 26, 27, 701, 702].map(columnLetter)).toEqual(['A', 'Z', 'AA', 'AB', 'ZZ', 'AAA']);
  });

  it('columnWarnings je prazan kada su nazivi jedinstveni', () => {
    expect(columnWarnings([column(0, 'A'), column(1, 'B')])).toEqual([]);
  });
});
