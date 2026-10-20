import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { createBrowserRouter, RouterProvider } from 'react-router';
import { AppProviders } from './AppProviders';
import { routes } from './routes';
import './index.css';

const router = createBrowserRouter(routes);
const container = document.getElementById('root');
if (!container) {
  throw new Error('Element #root nije pronađen.');
}

createRoot(container).render(
  <StrictMode>
    <AppProviders>
      <RouterProvider router={router} />
    </AppProviders>
  </StrictMode>,
);
