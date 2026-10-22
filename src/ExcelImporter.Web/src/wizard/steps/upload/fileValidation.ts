import { ApiError } from '../../../api/apiError';
import { t, type MessageKey } from '../../../messages.sr';

export const maxFileSizeBytes = 50 * 1024 * 1024;

/** Klijentska provera pre slanja; vraća ključ poruke ili null kada je fajl u redu. */
export function validateFile(file: Pick<File, 'name' | 'size'>): MessageKey | null {
  if (!file.name.toLowerCase().endsWith('.xlsx')) {
    return 'upload.err.format';
  }
  if (file.size === 0) {
    return 'upload.err.empty';
  }
  if (file.size > maxFileSizeBytes) {
    return 'upload.err.size';
  }
  return null;
}

/** Serverski odbijen ili neuspeo upload u poruku iz kataloga; serverski tekst se ne prikazuje. */
export function uploadErrorMessage(error: unknown): string {
  if (!(error instanceof ApiError) || error.status === 0 || error.status >= 500) {
    return t('upload.err.network');
  }
  switch (error.status) {
    case 413:
      return t('upload.err.size');
    case 415:
      return t('upload.err.format');
    default:
      return t('upload.err.unreadable');
  }
}

const sizeFormat = new Intl.NumberFormat('sr-Latn', { maximumFractionDigits: 1 });

export function formatFileSize(bytes: number): string {
  if (bytes < 1024) {
    return `${sizeFormat.format(bytes)} B`;
  }
  if (bytes < 1024 * 1024) {
    return `${sizeFormat.format(bytes / 1024)} KB`;
  }
  return `${sizeFormat.format(bytes / (1024 * 1024))} MB`;
}
