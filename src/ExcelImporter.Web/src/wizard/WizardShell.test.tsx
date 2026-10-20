import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router';
import { z } from 'zod';
import { useWizardForm } from './useWizardForm';
import { WizardShell } from './WizardShell';
import type { WizardStepDefinition } from './types';

const fileSchema = z.object({ fileName: z.string() });
const sheetSchema = z.object({ sheet: z.string() });

function FileStep() {
  const { register } = useWizardForm(fileSchema, (data) => ({ fileName: data.fileName }));
  return (
    <label>
      Naziv fajla
      <input {...register('fileName')} />
    </label>
  );
}

function SheetStep() {
  const { register } = useWizardForm(sheetSchema, (data) => ({ sheet: data.sheet }));
  return (
    <label>
      Sheet
      <input {...register('sheet')} />
    </label>
  );
}

function EndStep() {
  return <p>Kraj</p>;
}

const steps: readonly WizardStepDefinition[] = [
  {
    slug: 'fajl',
    titleKey: 'step.file.title',
    navLabelKey: 'step.file.nav',
    hintKey: 'step.file.hint',
    Component: FileStep,
    isValid: (data) => data.fileName.trim().length > 0,
    disabledReasonKey: 'nav.disabledReason.file',
  },
  {
    slug: 'analiza',
    titleKey: 'step.analysis.title',
    navLabelKey: 'step.analysis.nav',
    hintKey: 'step.analysis.hint',
    Component: SheetStep,
  },
  {
    slug: 'kraj',
    titleKey: 'step.import.title',
    navLabelKey: 'step.import.nav',
    hintKey: 'step.import.hint',
    Component: EndStep,
  },
];

function LocationProbe() {
  return <output data-testid="location">{useLocation().pathname}</output>;
}

function renderWizard(initialPath = '/uvoz/fajl') {
  return render(
    <MemoryRouter initialEntries={[initialPath]}>
      <LocationProbe />
      <Routes>
        <Route path="/uvoz/:korak" element={<WizardShell steps={steps} />} />
      </Routes>
    </MemoryRouter>,
  );
}

describe('WizardShell', () => {
  it('prikazuje naslov trenutnog koraka i traku koraka sa oznakom aktivnog', () => {
    renderWizard();

    expect(screen.getByRole('heading', { level: 1, name: 'Izaberite Excel fajl' })).toBeInTheDocument();
    const items = screen.getAllByRole('listitem');
    expect(items).toHaveLength(3);
    expect(items[0]).toHaveAttribute('aria-current', 'step');
  });

  it('onemogućava Nazad na prvom koraku i Dalje dok korak nije validan, uz vidljiv razlog', async () => {
    const user = userEvent.setup();
    renderWizard();

    expect(screen.getByRole('button', { name: /Nazad/ })).toBeDisabled();
    const next = screen.getByRole('button', { name: /Dalje/ });
    expect(next).toBeDisabled();
    expect(next).toHaveAccessibleDescription('Izaberite fajl da biste nastavili.');

    await user.type(screen.getByLabelText('Naziv fajla'), 'prodaja.xlsx');
    expect(screen.getByRole('button', { name: /Dalje/ })).toBeEnabled();
    expect(screen.queryByText('Izaberite fajl da biste nastavili.')).not.toBeInTheDocument();
  });

  it('čuva unos pri kretanju napred i nazad', async () => {
    const user = userEvent.setup();
    renderWizard();

    await user.type(screen.getByLabelText('Naziv fajla'), 'prodaja.xlsx');
    await user.click(screen.getByRole('button', { name: /Dalje/ }));
    await user.type(screen.getByLabelText('Sheet'), 'Prodaja');
    await user.click(screen.getByRole('button', { name: /Nazad/ }));

    expect(screen.getByLabelText('Naziv fajla')).toHaveValue('prodaja.xlsx');

    await user.click(screen.getByRole('button', { name: /Dalje/ }));
    expect(screen.getByLabelText('Sheet')).toHaveValue('Prodaja');
    expect(screen.getByTestId('location')).toHaveTextContent('/uvoz/analiza');
  });

  it('prebacuje fokus na naslov novog koraka', async () => {
    const user = userEvent.setup();
    renderWizard();

    await user.type(screen.getByLabelText('Naziv fajla'), 'a.xlsx');
    await user.click(screen.getByRole('button', { name: /Dalje/ }));

    expect(screen.getByRole('heading', { level: 1, name: 'Analiza fajla' })).toHaveFocus();
    expect(document.title).toBe('Korak 2 od 3 — Analiza — Excel Importer');
  });

  it('dozvoljava klik samo na završene korake u traci koraka', async () => {
    const user = userEvent.setup();
    renderWizard();

    await user.type(screen.getByLabelText('Naziv fajla'), 'a.xlsx');
    await user.click(screen.getByRole('button', { name: /Dalje/ }));

    expect(screen.getByText('3. Uvoz')).toHaveAttribute('aria-disabled', 'true');
    await user.click(screen.getByRole('button', { name: /1\. Fajl/ }));

    expect(screen.getByTestId('location')).toHaveTextContent('/uvoz/fajl');
    expect(screen.getByLabelText('Naziv fajla')).toHaveValue('a.xlsx');
  });

  it('vraća na prvi korak kada se otvori korak koji još nije dostignut', () => {
    renderWizard('/uvoz/analiza');

    expect(screen.getByTestId('location')).toHaveTextContent('/uvoz/fajl');
  });

  it('vraća na prvi korak za nepoznat korak', () => {
    renderWizard('/uvoz/nepoznato');

    expect(screen.getByTestId('location')).toHaveTextContent('/uvoz/fajl');
  });

  it('na poslednjem koraku nema dugmeta Dalje', async () => {
    const user = userEvent.setup();
    renderWizard();

    await user.type(screen.getByLabelText('Naziv fajla'), 'a.xlsx');
    await user.click(screen.getByRole('button', { name: /Dalje/ }));
    await user.click(screen.getByRole('button', { name: /Dalje/ }));

    expect(screen.getByText('Kraj')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Dalje/ })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Nazad/ })).toBeEnabled();
  });
});
