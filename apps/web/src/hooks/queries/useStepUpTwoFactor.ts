'use client';

/**
 * #1859 Phase 10 — useStepUpTwoFactor
 *
 * Hook for performing a 2FA step-up verification on the current session.
 * Wraps POST /api/v1/auth/2fa/step-up with the body { code }.
 *
 * Used by StepUpTwoFactorModal when an admin command (e.g. rotate-key)
 * returns 401 with subcode "step_up_required". On success, the current
 * session's LastTotpVerifiedAt is refreshed server-side and the caller can
 * retry the original protected request.
 *
 * SP5 Admin Security S3 / T5 — wire contract: docs/api/2fa-step-up-protocol.md
 */

import { useMutation } from '@tanstack/react-query';

import { getApiBase } from '@/lib/api/core/httpClient';

export interface StepUpTwoFactorRequest {
  readonly code: string;
}

export interface StepUpTwoFactorResponse {
  readonly success: true;
  readonly lastTotpVerifiedAt?: string;
}

/**
 * Discriminator for non-success outcomes that the FE needs to distinguish.
 * - `invalid_code`: code mismatch / expired (401, subcode=invalid_code)
 * - `locked_out`: throttled (401, subcode=locked_out, retryAfterSeconds present)
 * - `unavailable`: store unavailable (503, transient)
 * - `unknown`: any other failure (network, 5xx)
 */
export type StepUpTwoFactorErrorKind = 'invalid_code' | 'locked_out' | 'unavailable' | 'unknown';

export class StepUpTwoFactorError extends Error {
  public readonly kind: StepUpTwoFactorErrorKind;
  public readonly retryAfterSeconds?: number;
  public readonly statusCode: number;

  constructor(
    kind: StepUpTwoFactorErrorKind,
    message: string,
    statusCode: number,
    retryAfterSeconds?: number
  ) {
    super(message);
    this.name = 'StepUpTwoFactorError';
    this.kind = kind;
    this.statusCode = statusCode;
    this.retryAfterSeconds = retryAfterSeconds;
  }
}

async function postStepUp(request: StepUpTwoFactorRequest): Promise<StepUpTwoFactorResponse> {
  const res = await fetch(`${getApiBase()}/api/v1/auth/2fa/step-up`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
    body: JSON.stringify(request),
  });

  if (res.ok) {
    return (await res.json()) as StepUpTwoFactorResponse;
  }

  // Parse error body — BE returns { error, subcode, message, retryAfterSeconds? }
  // per docs/api/2fa-step-up-protocol.md
  let subcode: string | undefined;
  let message = `Step-up verification failed (HTTP ${res.status})`;
  let retryAfterSeconds: number | undefined;
  try {
    const body = await res.json();
    subcode = body?.subcode;
    if (body?.message) {
      message = body.message;
    }
    if (typeof body?.retryAfterSeconds === 'number') {
      retryAfterSeconds = body.retryAfterSeconds;
    }
  } catch {
    // Body was not JSON; fall through with defaults.
  }

  let kind: StepUpTwoFactorErrorKind = 'unknown';
  if (res.status === 503) {
    kind = 'unavailable';
  } else if (res.status === 401) {
    if (subcode === 'locked_out') {
      kind = 'locked_out';
    } else if (subcode === 'invalid_code') {
      kind = 'invalid_code';
    } else {
      // Defensive: any other 401 (e.g. session expired) → treat as invalid_code
      // so the user is prompted to re-enter.
      kind = 'invalid_code';
    }
  }

  throw new StepUpTwoFactorError(kind, message, res.status, retryAfterSeconds);
}

/**
 * React Query mutation for 2FA step-up.
 *
 * @example
 * const stepUp = useStepUpTwoFactor();
 * stepUp.mutate(
 *   { code: '123456' },
 *   {
 *     onSuccess: () => retryOriginalRequest(),
 *     onError: (err) => {
 *       if (err instanceof StepUpTwoFactorError && err.kind === 'locked_out') {
 *         // show retry-after toast
 *       }
 *     },
 *   }
 * );
 */
export function useStepUpTwoFactor() {
  return useMutation<StepUpTwoFactorResponse, StepUpTwoFactorError, StepUpTwoFactorRequest>({
    mutationFn: postStepUp,
  });
}
