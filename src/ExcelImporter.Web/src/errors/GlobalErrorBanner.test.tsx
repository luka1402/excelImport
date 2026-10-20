import { QueryClientProvider, useQuery } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useState } from 'react';
import { ApiError } from '../api/apiError';
import { createQueryClient } from '../queryClient';
import { ErrorProvider, useErrors } from './ErrorContext';
import { GlobalErrorBanner } from './GlobalErrorBanner';

function Trigger({ error }: { readonly error: unknown }) {
  const { reportError } = useErrors();
  return (
    <button
      type="button"
      onClick={() => {
        reportError(error);
      }}
    >
      prijavi
    </button>
  );
}

describe('GlobalErrorBanner', () => {
  it('ne prikazuje ništa bez greške', () => {
    render(
      <ErrorProvider>
        <GlobalErrorBanner />
      </ErrorProvider>,
    );

    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('prikazuje poruku i tehničke detalje, i može da se zatvori', async () => {
    const user = userEvent.setup();
    render(
      <ErrorProvider>
        <GlobalErrorBanner />
        <Trigger error={new ApiError(500, { traceId: 'trace-1' })} />
      </ErrorProvider>,
    );

    await user.click(screen.getByRole('button', { name: 'prijavi' }));

    expect(screen.getByRole('alert')).toHaveTextContent('Došlo je do neočekivane greške.');
    expect(screen.getByText(/trace-1/)).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Zatvori' }));
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('ne otkriva poruku običnog Error-a', async () => {
    const user = userEvent.setup();
    render(
      <ErrorProvider>
        <GlobalErrorBanner />
        <Trigger error={new Error('connection string=secret')} />
      </ErrorProvider>,
    );

    await user.click(screen.getByRole('button', { name: 'prijavi' }));

    expect(screen.getByRole('alert')).not.toHaveTextContent('secret');
  });

  it('greška TanStack Query upita završava u globalnom prikazu', async () => {
    function Harness() {
      const { reportError } = useErrors();
      const [client] = useState(() => createQueryClient(reportError));
      client.setDefaultOptions({ queries: { retry: false } });
      return (
        <QueryClientProvider client={client}>
          <GlobalErrorBanner />
          <FailingQuery />
        </QueryClientProvider>
      );
    }
    function FailingQuery() {
      useQuery({
        queryKey: ['x'],
        queryFn: () => Promise.reject(new ApiError(503, { traceId: 'q-1' })),
      });
      return null;
    }

    render(
      <ErrorProvider>
        <Harness />
      </ErrorProvider>,
    );

    expect(await screen.findByRole('alert')).toHaveTextContent('Došlo je do neočekivane greške.');
  });
});
