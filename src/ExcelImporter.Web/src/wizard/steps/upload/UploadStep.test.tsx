import { fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router';
import { ApiError } from '../../../api/apiError';
import * as filesApi from '../../../api/files';
import { WizardProvider, useWizard } from '../../WizardContext';
import { UploadStep } from './UploadStep';
import { formatFileSize, maxFileSizeBytes, uploadErrorMessage, validateFile } from './fileValidation';

vi.mock('../../../api/files', () => ({ uploadFile: vi.fn() }));

const uploadFile = vi.mocked(filesApi.uploadFile);

function Probe() {
  const { data } = useWizard();
  return <span data-testid="file-id">{data.fileId}</span>;
}

function renderStep() {
  return render(
    <MemoryRouter>
      <WizardProvider>
        <UploadStep />
        <Probe />
      </WizardProvider>
    </MemoryRouter>,
  );
}

function xlsx(name = 'prodaja.xlsx', size = 2048) {
  const file = new File(['x'], name, { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
  Object.defineProperty(file, 'size', { value: size });
  return file;
}

beforeEach(() => {
  uploadFile.mockReset();
});

describe('UploadStep', () => {
  it('na početku prikazuje prazno stanje i dostupnu kontrolu za izbor fajla', () => {
    renderStep();

    expect(screen.getByText('Još niste izabrali fajl.')).toBeInTheDocument();
    expect(screen.getByLabelText('Izaberite fajl')).toBeInTheDocument();
    expect(screen.getByText('Dozvoljeno: .xlsx — najviše 50 MB')).toBeInTheDocument();
  });

  it('otprema ispravan fajl, prikazuje napredak i upisuje fileId u stanje', async () => {
    const user = userEvent.setup();
    let finish: (value: { fileId: string }) => void = () => undefined;
    uploadFile.mockImplementation((_file, options) => {
      options?.onProgress?.(78);
      return new Promise((resolve) => {
        finish = resolve;
      });
    });
    renderStep();

    await user.upload(screen.getByLabelText('Izaberite fajl'), xlsx());

    expect(await screen.findByText('Otpremanje: 78 %')).toBeInTheDocument();
    expect(screen.getByRole('progressbar')).toHaveValue(78);
    expect(screen.getByLabelText('Izaberite fajl')).toBeDisabled();

    finish({ fileId: '3f2504e0-4f89-11d3-9a0c-0305e82c3301' });

    expect(await screen.findByText('Izabrani fajl: prodaja.xlsx (2 KB)')).toBeInTheDocument();
    expect(screen.getByTestId('file-id')).toHaveTextContent('3f2504e0-4f89-11d3-9a0c-0305e82c3301');
  });

  it('otkazivanje prekida zahtev i ne prikazuje grešku', async () => {
    const user = userEvent.setup();
    uploadFile.mockImplementation(
      (_file, options) =>
        new Promise((_resolve, reject) => {
          options?.signal?.addEventListener('abort', () => {
            reject(new DOMException('prekinuto', 'AbortError'));
          });
        }),
    );
    renderStep();

    await user.upload(screen.getByLabelText('Izaberite fajl'), xlsx());
    await user.click(await screen.findByRole('button', { name: 'Otkaži' }));

    expect(await screen.findByText('Još niste izabrali fajl.')).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it.each([
    ['izvestaj.xls', 100, 'Fajl nije u podržanom formatu. Dozvoljen je samo .xlsx.'],
    ['prazan.xlsx', 0, 'Fajl je prazan.'],
    ['veliki.xlsx', maxFileSizeBytes + 1, 'Fajl je prevelik (najviše 50 MB).'],
  ])('klijentska provera odbija %s bez slanja na server', async (name, size, message) => {
    const user = userEvent.setup({ applyAccept: false });
    renderStep();

    await user.upload(screen.getByLabelText('Izaberite fajl'), xlsx(name, size));

    expect(await screen.findByRole('alert')).toHaveTextContent(message);
    expect(uploadFile).not.toHaveBeenCalled();
    expect(screen.getByTestId('file-id')).toBeEmptyDOMElement();
  });

  it('serverska greška se prikazuje porukom na srpskom, bez serverskog teksta', async () => {
    const user = userEvent.setup();
    uploadFile.mockRejectedValue(new ApiError(400, { title: 'Invalid central directory in C:\\temp\\x' }));
    renderStep();

    await user.upload(screen.getByLabelText('Izaberite fajl'), xlsx());

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Fajl ne može da se pročita. Moguće je da je oštećen ili zaštićen lozinkom.');
    expect(alert).not.toHaveTextContent('central directory');
  });

  it('mrežna greška daje poruku o vezi, a fajl može ponovo da se izabere', async () => {
    const user = userEvent.setup();
    uploadFile.mockRejectedValueOnce(new ApiError(0, null)).mockResolvedValueOnce({ fileId: 'a-id' });
    renderStep();
    const file = xlsx();

    await user.upload(screen.getByLabelText('Izaberite fajl'), file);
    expect(await screen.findByRole('alert')).toHaveTextContent('Otpremanje nije uspelo. Proverite vezu i pokušajte ponovo.');

    await user.upload(screen.getByLabelText('Izaberite fajl'), file);
    expect(await screen.findByText(/Izabrani fajl: prodaja\.xlsx/)).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('prihvata fajl prevučen na zonu', async () => {
    uploadFile.mockResolvedValue({ fileId: 'drop-id' });
    const { container } = renderStep();
    const zone = container.querySelector('[class*="dropzone"]');
    expect(zone).not.toBeNull();

    fireEvent.drop(zone as Element, { dataTransfer: { files: [xlsx('prevuceno.xlsx')] } });

    expect(await screen.findByText(/Izabrani fajl: prevuceno\.xlsx/)).toBeInTheDocument();
  });

  it('Ukloni briše izabrani fajl iz stanja', async () => {
    const user = userEvent.setup();
    uploadFile.mockResolvedValue({ fileId: 'a-id' });
    renderStep();

    await user.upload(screen.getByLabelText('Izaberite fajl'), xlsx());
    await user.click(await screen.findByRole('button', { name: 'Ukloni' }));

    expect(screen.getByTestId('file-id')).toBeEmptyDOMElement();
    expect(screen.getByText('Još niste izabrali fajl.')).toBeInTheDocument();
  });
});

describe('fileValidation', () => {
  it('ekstenzija se poredi bez obzira na velika slova', () => {
    expect(validateFile({ name: 'A.XLSX', size: 10 })).toBeNull();
  });

  it('uploadErrorMessage mapira statuse na poruke iz kataloga', () => {
    expect(uploadErrorMessage(new ApiError(413, null))).toBe('Fajl je prevelik (najviše 50 MB).');
    expect(uploadErrorMessage(new ApiError(415, null))).toBe('Fajl nije u podržanom formatu. Dozvoljen je samo .xlsx.');
    expect(uploadErrorMessage(new ApiError(503, null))).toContain('Otpremanje nije uspelo');
    expect(uploadErrorMessage(new Error('x'))).toContain('Otpremanje nije uspelo');
  });

  it('formatira veličinu fajla sa srpskim decimalnim zarezom', () => {
    expect(formatFileSize(500)).toBe('500 B');
    expect(formatFileSize(2.3 * 1024 * 1024)).toBe('2,3 MB');
  });
});
