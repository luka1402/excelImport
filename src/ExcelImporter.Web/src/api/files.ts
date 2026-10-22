import { z } from 'zod';
import { ApiError, type ProblemDetails } from './apiError';
import { apiRequest } from './client';

const baseUrl = (import.meta.env.VITE_API_BASE_URL ?? '').replace(/\/$/, '');

const uploadResponseSchema = z.object({ fileId: z.guid() });

// Server enum serijalizuje kao broj (redosled iz ColumnType) ili kao tekst, zavisno od podešavanja JSON-a.
const columnTypeNames = ['Text', 'WholeNumber', 'DecimalNumber', 'Date', 'Boolean'] as const;
export type ColumnTypeName = (typeof columnTypeNames)[number];

const columnTypeSchema = z.union([z.enum(columnTypeNames), z.number().int().min(0).max(4)]).transform((value): ColumnTypeName => {
  if (typeof value === 'number') {
    return columnTypeNames[value] ?? 'Text';
  }
  return value;
});

const analysisSchema = z.object({
  sheets: z.array(z.object({ index: z.number().int(), name: z.string(), isHidden: z.boolean() })),
  selectedSheetIndex: z.number().int(),
  previewRows: z.array(z.array(z.string().nullable())),
  previewColumnsTruncated: z.boolean(),
  headerRowIndex: z.number().int().nullable(),
  headerChosenManually: z.boolean(),
  columns: z.array(
    z.object({
      index: z.number().int(),
      name: z.string(),
      type: columnTypeSchema,
      isEmpty: z.boolean(),
      isMixed: z.boolean(),
      nonEmptyCount: z.number().int(),
    }),
  ),
});

export type FileAnalysis = z.infer<typeof analysisSchema>;
export type AnalyzedColumn = FileAnalysis['columns'][number];

/** Putanja upload endpointa (T09); jedino mesto koje zna za nju. */
export const uploadPath = '/api/files';

export interface UploadOptions {
  readonly onProgress?: (percent: number) => void;
  readonly signal?: AbortSignal;
}

/**
 * Otprema fajl kao multipart/form-data (polje „file"). XMLHttpRequest je potreban jer fetch
 * ne izlaže napredak otpremanja.
 */
export function uploadFile(file: File, { onProgress, signal }: UploadOptions = {}): Promise<{ readonly fileId: string }> {
  return new Promise((resolve, reject) => {
    const request = new XMLHttpRequest();
    request.open('POST', `${baseUrl}${uploadPath}`);
    request.setRequestHeader('Accept', 'application/json');

    request.upload.onprogress = (event) => {
      if (event.lengthComputable && event.total > 0) {
        onProgress?.(Math.min(100, Math.round((event.loaded / event.total) * 100)));
      }
    };
    request.onerror = () => {
      reject(new ApiError(0, null));
    };
    request.onabort = () => {
      reject(new DOMException('Otpremanje je prekinuto.', 'AbortError'));
    };
    request.onload = () => {
      if (request.status < 200 || request.status >= 300) {
        reject(new ApiError(request.status, parseProblem(request.responseText)));
        return;
      }
      try {
        resolve(uploadResponseSchema.parse(JSON.parse(request.responseText)));
      } catch (cause) {
        reject(new ApiError(request.status, null, { cause }));
      }
    };

    if (signal) {
      if (signal.aborted) {
        reject(new DOMException('Otpremanje je prekinuto.', 'AbortError'));
        return;
      }
      signal.addEventListener('abort', () => {
        request.abort();
      }, { once: true });
    }

    const form = new FormData();
    form.append('file', file);
    request.send(form);
  });
}

function parseProblem(text: string): ProblemDetails | null {
  try {
    const parsed: unknown = JSON.parse(text);
    return typeof parsed === 'object' && parsed !== null ? parsed : null;
  } catch {
    return null;
  }
}

export interface AnalyzeParams {
  readonly fileId: string;
  readonly sheetIndex: number | null;
  readonly headerRowIndex: number | null;
}

export function analyzeFile({ fileId, sheetIndex, headerRowIndex }: AnalyzeParams, signal?: AbortSignal): Promise<FileAnalysis> {
  const query = new URLSearchParams();
  if (sheetIndex !== null) {
    query.set('sheet', sheetIndex.toString());
  }
  if (headerRowIndex !== null) {
    query.set('headerRow', headerRowIndex.toString());
  }
  const suffix = query.size > 0 ? `?${query.toString()}` : '';
  return apiRequest(`/api/files/${encodeURIComponent(fileId)}/analyze${suffix}`, {
    schema: analysisSchema,
    ...(signal ? { signal } : {}),
  });
}
