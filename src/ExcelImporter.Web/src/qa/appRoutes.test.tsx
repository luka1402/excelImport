import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { AppProviders } from '../AppProviders';
import { routes } from '../routes';

function renderApp(path: string) {
  const router = createMemoryRouter(routes, { initialEntries: [path] });
  render(
    <AppProviders>
      <RouterProvider router={router} />
    </AppProviders>,
  );
  return router;
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('aplikacija sa pravim rutama i koracima', () => {
  it('koren preusmerava na prvi korak', async () => {
    const router = renderApp('/');
    expect(await screen.findByRole('heading', { level: 1, name: 'Izaberite Excel fajl' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/uvoz/fajl');
  });

  it('prolazi svih 7 koraka napred i nazad (uključujući korak sa dijakritikom u URL-u)', async () => {
    const user = userEvent.setup();
    vi.stubGlobal(
      'fetch',
      vi.fn<typeof fetch>((input) =>
        Promise.resolve(
          input === '/api/connections'
            ? Response.json([{ id: 1, displayName: 'Demo baza' }])
            : Response.json({ tables: [{ schema: 'dbo', name: 'Prodaja', columns: [], primaryKey: [], foreignKeys: [] }] }),
        ),
      ),
    );
    const router = renderApp('/uvoz/fajl');
    await screen.findByRole('heading', { level: 1 });

    for (let i = 0; i < 6; i++) {
      if (decodeURIComponent(router.state.location.pathname) === '/uvoz/odredište') {
        await user.selectOptions(await screen.findByLabelText('Baza'), 'Demo baza');
        await user.selectOptions(await screen.findByLabelText('Tabela'), 'dbo.Prodaja');
      }
      await user.click(screen.getByRole('button', { name: /Dalje/ }));
    }
    expect(screen.queryByRole('button', { name: /Dalje/ })).not.toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/uvoz/uvoz');

    for (let i = 0; i < 6; i++) {
      await user.click(screen.getByRole('button', { name: /Nazad/ }));
    }
    expect(router.state.location.pathname).toBe('/uvoz/fajl');
    expect(screen.getByRole('button', { name: /Nazad/ })).toBeDisabled();
  });

  it('direktan ulaz na kasniji korak vraća na prvi', async () => {
    const router = renderApp('/uvoz/mapiranje');
    await screen.findByRole('heading', { level: 1 });
    expect(router.state.location.pathname).toBe('/uvoz/fajl');
  });

  it('nepoznata putanja prikazuje poruku, ne pada', async () => {
    renderApp('/nema/ovde');
    expect(await screen.findByText(/nije pronađ|ne postoji/i)).toBeInTheDocument();
  });
});
