import { z } from 'zod';
import { ApiError } from './apiError';
import { apiRequest } from './client';

function mockFetch(response: Response | Error) {
  const fetchMock = vi.fn<typeof fetch>(() =>
    response instanceof Error ? Promise.reject(response) : Promise.resolve(response),
  );
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('apiRequest', () => {
  it('vraća validiran JSON odgovor', async () => {
    mockFetch(Response.json({ id: 7 }));

    const result = await apiRequest('/api/x', { schema: z.object({ id: z.number() }) });

    expect(result).toEqual({ id: 7 });
  });

  it('šalje telo kao JSON sa odgovarajućim zaglavljem', async () => {
    const fetchMock = mockFetch(new Response(null, { status: 204 }));

    await apiRequest('/api/x', { method: 'POST', body: { a: 1 } });

    const init = fetchMock.mock.calls[0]?.[1];
    expect(init?.body).toBe('{"a":1}');
    expect(init?.headers).toMatchObject({ 'Content-Type': 'application/json' });
  });

  it('baca ApiError sa ProblemDetails i traceId za neuspešan status', async () => {
    mockFetch(Response.json({ title: 'Loše', detail: 'tajni detalj', traceId: 'abc' }, { status: 500 }));

    const error = await apiRequest('/api/x').catch((reason: unknown) => reason);

    expect(error).toBeInstanceOf(ApiError);
    expect(error).toMatchObject({ status: 500, traceId: 'abc' });
    expect((error as ApiError).userMessage).toBe('Došlo je do neočekivane greške. Pokušajte ponovo.');
  });

  it('prikazuje serverski detalj samo kada je namenjen korisniku', async () => {
    mockFetch(Response.json({ detail: 'Fajl je prazan.', userFacing: true }, { status: 400 }));

    const error = (await apiRequest('/api/x').catch((reason: unknown) => reason)) as ApiError;

    expect(error.userMessage).toBe('Fajl je prazan.');
  });

  it('pretvara mrežni pad u ApiError sa statusom 0', async () => {
    mockFetch(new TypeError('Failed to fetch'));

    const error = (await apiRequest('/api/x').catch((reason: unknown) => reason)) as ApiError;

    expect(error.status).toBe(0);
    expect(error.userMessage).toContain('Server nije dostupan');
  });

  it('podnosi telo greške koje nije JSON', async () => {
    mockFetch(new Response('<html>', { status: 502 }));

    const error = (await apiRequest('/api/x').catch((reason: unknown) => reason)) as ApiError;

    expect(error.status).toBe(502);
    expect(error.problem).toBeNull();
  });
});
