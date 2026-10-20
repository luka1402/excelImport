import type { ZodType } from 'zod';
import { ApiError, type ProblemDetails } from './apiError';

export interface RequestOptions<TResponse> {
  readonly method?: 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE';
  /** Telo se šalje kao JSON; za FormData ostaje nepromenjeno. */
  readonly body?: unknown;
  readonly signal?: AbortSignal;
  /** Ako je zadat, odgovor se validira i tip je izveden iz šeme. */
  readonly schema?: ZodType<TResponse>;
}

const baseUrl = (import.meta.env.VITE_API_BASE_URL ?? '').replace(/\/$/, '');

async function readProblem(response: Response): Promise<ProblemDetails | null> {
  try {
    const parsed: unknown = await response.json();
    return typeof parsed === 'object' && parsed !== null ? parsed : null;
  } catch {
    // Telo greške nije JSON (npr. proxy stranica); status je dovoljan.
    return null;
  }
}

export async function apiRequest<TResponse = void>(
  path: string,
  options: RequestOptions<TResponse> = {},
): Promise<TResponse> {
  const { method = 'GET', body, signal, schema } = options;
  const isForm = body instanceof FormData;
  const headers: Record<string, string> = { Accept: 'application/json' };
  if (body !== undefined && !isForm) {
    headers['Content-Type'] = 'application/json';
  }

  let response: Response;
  try {
    const init: RequestInit = { method, headers };
    if (body !== undefined) {
      init.body = isForm ? body : JSON.stringify(body);
    }
    if (signal) {
      init.signal = signal;
    }
    response = await fetch(`${baseUrl}${path}`, init);
  } catch (cause) {
    if (cause instanceof DOMException && cause.name === 'AbortError') {
      throw cause;
    }
    throw new ApiError(0, null, { cause });
  }

  if (!response.ok) {
    throw new ApiError(response.status, await readProblem(response));
  }
  if (response.status === 204) {
    return undefined as TResponse;
  }

  const json: unknown = await response.json();
  return schema ? schema.parse(json) : (json as TResponse);
}
