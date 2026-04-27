/**
 * Public Alpha waitlist — API client.
 *
 * Wave A.2 (`/join` route migration). Spec §3.5 endpoint contract:
 *   POST /api/v1/waitlist
 *     200 → { position, estimatedWeeks }
 *     409 → { error: 'ALREADY_ON_LIST', position, estimatedWeeks }
 *     400/422 → { error: 'VALIDATION', fieldErrors? }
 *
 * Why direct `fetch` instead of `apiClient.post()`:
 * The shared `HttpClient` resolves the response body once inside
 * `createApiError()` to compose the user-friendly message and then throws —
 * which discards the 409 body fields (`position`, `estimatedWeeks`) we need
 * for the "already-on-list" UX. The waitlist surface is also unauthenticated,
 * idempotent against the email unique key, and doesn't benefit from the
 * shared retry/circuit-breaker layer (a stuck 5xx loop here would just spin
 * the visitor's spinner). So we keep the fetch local and explicit.
 */

import { getApiBase } from './core/httpClient';

export interface WaitlistPayload {
  readonly email: string;
  readonly name: string | null;
  readonly gamePreferenceId: string;
  readonly gamePreferenceOther: string | null;
  readonly newsletterOptIn: boolean;
}

/**
 * Discriminated union — covers both the 200 happy path and the 409
 * "already on list" branch in a single shape so the FSM can store one value.
 */
export type WaitlistResult =
  | { readonly alreadyOnList: false; readonly position: number; readonly estimatedWeeks: number }
  | { readonly alreadyOnList: true; readonly position: number; readonly estimatedWeeks: number };

/**
 * Errors that the form FSM treats as "validation/network/unknown" — i.e. show
 * a generic banner, preserve form data, allow retry. 409 is NOT thrown; it's
 * returned as a successful `WaitlistResult` with `alreadyOnList: true`.
 */
export class WaitlistValidationError extends Error {
  public readonly status: number;
  public readonly fieldErrors?: Readonly<Record<string, string>>;

  constructor(status: number, message: string, fieldErrors?: Readonly<Record<string, string>>) {
    super(message);
    this.name = 'WaitlistValidationError';
    this.status = status;
    this.fieldErrors = fieldErrors;
  }
}

interface ServerSuccessBody {
  position: number;
  estimatedWeeks: number;
}

interface ServerConflictBody {
  error?: string;
  position?: number;
  estimatedWeeks?: number;
}

interface ServerValidationBody {
  error?: string;
  fieldErrors?: Record<string, string>;
}

/**
 * POST /api/v1/waitlist with full body capture for 200 + 409.
 *
 * Throws `WaitlistValidationError` on 4xx (other than 409) and a generic
 * `Error` on 5xx / network / parse failures — both treated identically by the
 * FSM ("error" state, generic banner).
 */
export async function postWaitlistEntry(payload: WaitlistPayload): Promise<WaitlistResult> {
  const base = getApiBase();
  const url = `${base}/api/v1/waitlist`;

  let response: Response;
  try {
    response = await fetch(url, {
      method: 'POST',
      credentials: 'include',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    });
  } catch (cause) {
    // Network failure (offline, DNS, CORS preflight) — caller surfaces generic banner.
    throw new Error('Waitlist request failed before reaching the server', { cause });
  }

  if (response.status === 200) {
    const body = (await response.json()) as ServerSuccessBody;
    return {
      alreadyOnList: false,
      position: body.position,
      estimatedWeeks: body.estimatedWeeks,
    };
  }

  if (response.status === 409) {
    const body = (await response.json().catch(() => ({}))) as ServerConflictBody;
    return {
      alreadyOnList: true,
      position: body.position ?? 0,
      estimatedWeeks: body.estimatedWeeks ?? 1,
    };
  }

  if (response.status >= 400 && response.status < 500) {
    const body = (await response.json().catch(() => ({}))) as ServerValidationBody;
    throw new WaitlistValidationError(
      response.status,
      body.error ?? `Waitlist validation failed (${response.status})`,
      body.fieldErrors
    );
  }

  // 5xx and unexpected statuses → generic error.
  throw new Error(`Waitlist request failed with status ${response.status}`);
}
