'use client';

/**
 * #1859 Phase 11 — useRotateProviderKey
 *
 * Hook for POST /api/v1/admin/providers/{name}/rotate-key.
 *
 * Wraps the BE rotate-key endpoint which:
 * - Requires SuperAdmin + step-up 2FA (strict cutover #1597, MaxAge=5 min)
 * - Probes the new key against the provider before persisting (fail-fast)
 * - Returns the new key's short fingerprint (display once) + audit timestamps
 *
 * **Error vocabulary** (subcodes / contracts):
 *   401 + subcode=step_up_required → StepUpTwoFactorModal opens; on success
 *                                    the caller automatically retries this mutation.
 *   401 + subcode=enroll_required  → toast "2FA non configurato"
 *   403                           → toast "Solo superadmin"
 *   400 + code=provider_name_mismatch → toast (typed-confirm mismatch)
 *   400 + code=invalid_key_format    → toast
 *   400 + code=invalid_provider      → toast
 *   409 + code=rate_limit_exceeded   → toast (24h rotation guard)
 *   502 + code=provider_probe_failed → toast (new key does not authenticate)
 *
 * The hook itself only normalizes the error into a tagged `RotateProviderKeyError`;
 * the UI (RotateKeyModal) is responsible for the per-kind toast / modal routing.
 */

import { useMutation, useQueryClient } from '@tanstack/react-query';

import { getApiBase } from '@/lib/api/core/httpClient';
import type {
  ProviderName,
  RotateProviderKeyRequest,
  RotateProviderKeyResponse,
} from '@/lib/api/schemas/providers';

import { providerKeys } from './useProviders';

/** Discriminator for the rotate-key failure modes the FE needs to distinguish. */
export type RotateProviderKeyErrorKind =
  | 'step_up_required'
  | 'enroll_required'
  | 'forbidden'
  | 'provider_name_mismatch'
  | 'invalid_key_format'
  | 'invalid_provider'
  | 'rate_limit_exceeded'
  | 'provider_probe_failed'
  | 'bad_request'
  | 'unknown';

export class RotateProviderKeyError extends Error {
  public readonly kind: RotateProviderKeyErrorKind;
  public readonly statusCode: number;
  /** Subcode / error code returned by the BE (snake_case). */
  public readonly serverCode?: string;
  /** Retry-after hint in seconds when present (rate-limit / locked). */
  public readonly retryAfterSeconds?: number;

  constructor(
    kind: RotateProviderKeyErrorKind,
    message: string,
    statusCode: number,
    options: { serverCode?: string; retryAfterSeconds?: number } = {}
  ) {
    super(message);
    this.name = 'RotateProviderKeyError';
    this.kind = kind;
    this.statusCode = statusCode;
    this.serverCode = options.serverCode;
    this.retryAfterSeconds = options.retryAfterSeconds;
  }
}

interface RotateKeyEndpointBody {
  readonly error?: string;
  readonly code?: string;
  readonly subcode?: string;
  readonly message?: string;
  readonly retryAfterSeconds?: number;
}

function classifyError(
  status: number,
  body: RotateKeyEndpointBody | undefined
): RotateProviderKeyErrorKind {
  const code = body?.code ?? body?.subcode ?? body?.error;
  if (status === 401) {
    if (code === 'enroll_required') return 'enroll_required';
    return 'step_up_required';
  }
  if (status === 403) return 'forbidden';
  if (status === 400) {
    if (code === 'provider_name_mismatch') return 'provider_name_mismatch';
    if (code === 'invalid_key_format') return 'invalid_key_format';
    if (code === 'invalid_provider') return 'invalid_provider';
    return 'bad_request';
  }
  if (status === 409) return 'rate_limit_exceeded';
  if (status === 502) return 'provider_probe_failed';
  return 'unknown';
}

async function postRotateKey(
  providerName: ProviderName,
  request: RotateProviderKeyRequest
): Promise<RotateProviderKeyResponse> {
  const res = await fetch(
    `${getApiBase()}/api/v1/admin/providers/${encodeURIComponent(providerName)}/rotate-key`,
    {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      credentials: 'include',
      body: JSON.stringify(request),
    }
  );

  if (res.ok) {
    return (await res.json()) as RotateProviderKeyResponse;
  }

  let body: RotateKeyEndpointBody | undefined;
  try {
    body = (await res.json()) as RotateKeyEndpointBody;
  } catch {
    body = undefined;
  }

  const kind = classifyError(res.status, body);
  const message = body?.message ?? `Rotate-key failed (HTTP ${res.status})`;
  throw new RotateProviderKeyError(kind, message, res.status, {
    serverCode: body?.code ?? body?.subcode ?? body?.error,
    retryAfterSeconds: body?.retryAfterSeconds,
  });
}

/**
 * Mutation hook for rotating a provider's API key.
 *
 * @example
 * const rotate = useRotateProviderKey('deepseek');
 * rotate.mutate(
 *   { newApiKey, confirmedProviderName: 'deepseek' },
 *   {
 *     onError: (err) => {
 *       if (err.kind === 'step_up_required') openStepUpModal();
 *       else toast.error(err.message);
 *     },
 *   }
 * );
 */
export function useRotateProviderKey(providerName: ProviderName) {
  const qc = useQueryClient();
  return useMutation<RotateProviderKeyResponse, RotateProviderKeyError, RotateProviderKeyRequest>({
    mutationFn: request => postRotateKey(providerName, request),
    onSuccess: () => {
      // Invalidate provider quota & probe data; the fingerprint and any
      // status surfaced from the previous key will refresh on next fetch.
      qc.invalidateQueries({ queryKey: providerKeys.quota(providerName) });
      qc.invalidateQueries({ queryKey: providerKeys.all });
    },
  });
}
