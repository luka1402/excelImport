import { t } from '../messages.sr';

export interface ProblemDetails {
  readonly type?: string;
  readonly title?: string;
  readonly status?: number;
  readonly detail?: string;
  readonly traceId?: string;
  /** Server postavlja true samo za poruke namenjene korisniku. */
  readonly userFacing?: boolean;
}

export class ApiError extends Error {
  /** HTTP status; 0 kada server nije odgovorio (mrežna greška). */
  readonly status: number;
  readonly problem: ProblemDetails | null;

  constructor(status: number, problem: ProblemDetails | null, options?: ErrorOptions) {
    super(problem?.title ?? `HTTP ${status.toString()}`, options);
    this.name = 'ApiError';
    this.status = status;
    this.problem = problem;
  }

  get traceId(): string | undefined {
    return this.problem?.traceId;
  }

  /** Poruka za korisnika: serverski detalj samo ako je izričito namenjen korisniku. */
  get userMessage(): string {
    if (this.status === 0) {
      return t('error.network');
    }
    if (this.problem?.userFacing === true && this.problem.detail) {
      return this.problem.detail;
    }
    return t('error.unexpected');
  }
}

export function toUserMessage(error: unknown): string {
  return error instanceof ApiError ? error.userMessage : t('error.unexpected');
}
