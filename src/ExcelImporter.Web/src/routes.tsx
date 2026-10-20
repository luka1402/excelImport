import { Navigate, type RouteObject } from 'react-router';
import { AppLayout } from './AppLayout';
import { t } from './messages.sr';
import { EmptyState } from './ui/states';
import { WizardShell } from './wizard/WizardShell';
import { wizardSteps } from './wizard/steps';

export const routes: RouteObject[] = [
  {
    element: <AppLayout />,
    children: [
      { index: true, element: <Navigate to="/uvoz/fajl" replace /> },
      { path: 'uvoz/:korak', element: <WizardShell steps={wizardSteps} /> },
      { path: '*', element: <EmptyState message={t('common.notFound')} /> },
    ],
  },
];
