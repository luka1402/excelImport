import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { AppProviders } from '../AppProviders';
import type { FileAnalysis } from '../api/files';
import { routes } from '../routes';

vi.mock('../api/files', () => ({
  uploadFile: vi.fn(() => Promise.resolve({ fileId: '3f2504e0-4f89-11d3-9a0c-0305e82c3301' })),
  analyzeFile: vi.fn(() =>
    Promise.resolve<FileAnalysis>({
      sheets: [{ index: 0, name: 'Prodaja', isHidden: false }],
      selectedSheetIndex: 0,
      previewRows: [['Kupac', 'Opis'], ['Marko', 'a']],
      previewColumnsTruncated: false,
      headerRowIndex: 0,
      headerChosenManually: false,
      columns: [
        { index: 0, name: 'Kupac', type: 'Text', isEmpty: false, isMixed: false, nonEmptyCount: 1 },
        { index: 1, name: 'Opis', type: 'Text', isEmpty: false, isMixed: false, nonEmptyCount: 1 },
      ],
    }),
  ),
}));

function col(name: string, isNullable: boolean) {
  return { name, ordinal: 1, dataType: 'nvarchar', isNullable, maxLength: 100, isIdentity: false, isComputed: false };
}

function table(name: string) {
  return { schema: 'dbo', name, columns: [col('Kupac', false), col('Opis', true), col('Status', false)], primaryKey: [], foreignKeys: [] };
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('korak Mapiranje: povratak unazad', () => {
  it('čuva ručne izbore i konstante pri povratku, a menjanje tabele pravi novi predlog', async () => {
    const user = userEvent.setup();
    vi.stubGlobal(
      'fetch',
      vi.fn<typeof fetch>((input) =>
        Promise.resolve(
          input === '/api/connections'
            ? Response.json([{ id: 1, displayName: 'Demo baza' }])
            : Response.json({ tables: [table('Prodaja'), table('Drugo')] }),
        ),
      ),
    );
    const router = createMemoryRouter(routes, { initialEntries: ['/uvoz/fajl'] });
    render(
      <AppProviders>
        <RouterProvider router={router} />
      </AppProviders>,
    );
    await user.upload(await screen.findByLabelText('Izaberite fajl'), new File(['x'], 'prodaja.xlsx'));
    await screen.findByText(/Izabrani fajl: prodaja\.xlsx/);
    await user.click(screen.getByRole('button', { name: /Dalje/ }));
    await screen.findByRole('heading', { name: 'Kolone' });
    await waitFor(() => {
      expect(screen.getByRole('button', { name: /Dalje/ })).toBeEnabled();
    });
    await user.click(screen.getByRole('button', { name: /Dalje/ }));
    await user.selectOptions(await screen.findByLabelText('Baza'), 'Demo baza');
    await user.selectOptions(await screen.findByLabelText('Tabela'), 'dbo.Prodaja');
    await user.click(screen.getByRole('button', { name: /Dalje/ }));

    // Obavezna „Status" nije mapirana: upozorenje i blokiran nastavak.
    await screen.findByText(/Obavezna kolona „Status" nije mapirana/);
    expect(screen.getByRole('button', { name: /Dalje/ })).toBeDisabled();

    await user.selectOptions(screen.getByLabelText('Ciljna kolona za Opis'), 'Status');
    await user.type(screen.getByLabelText('Konstanta za kolonu Opis'), 'x');
    await waitFor(() => {
      expect(screen.getByRole('button', { name: /Dalje/ })).toBeEnabled();
    });

    await user.click(screen.getByRole('button', { name: /Nazad/ }));
    await user.click(screen.getByRole('button', { name: /Dalje/ }));
    expect(await screen.findByLabelText('Ciljna kolona za Opis')).toHaveValue('Status');
    expect(screen.getByLabelText('Ciljna kolona za Kupac')).toHaveValue('Kupac');
    expect(screen.getByRole('button', { name: /Dalje/ })).toBeEnabled();

    // Druga tabela: mapiranje se poništava i pravi novi predlog.
    await user.click(screen.getByRole('button', { name: /Nazad/ }));
    await user.selectOptions(await screen.findByLabelText('Tabela'), 'dbo.Drugo');
    await user.click(screen.getByRole('button', { name: /Dalje/ }));
    expect(await screen.findByLabelText('Ciljna kolona za Opis')).toHaveValue('Opis');
  });
});
