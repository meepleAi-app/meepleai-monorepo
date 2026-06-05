import { z } from 'zod';

/**
 * Issue #936 (G1) — Provider probe result.
 * Outcome=Success means token authenticated + provider reachable.
 * modelAvailable is informational and only populated when caller passes ?model=X.
 */
export const ProviderProbeResultSchema = z.object({
  providerName: z.string(),
  tokenConfigured: z.boolean(),
  tokenAuthenticated: z.boolean(),
  modelAvailable: z.boolean().nullable(),
  expectedModel: z.string().nullable(),
  tokenFingerprint: z.string().nullable(),
  errorCode: z.string().nullable(),
  errorMessage: z.string().nullable(),
  latencyMs: z.number(),
  probedAt: z.string(),
});

export type ProviderProbeResult = z.infer<typeof ProviderProbeResultSchema>;

/**
 * Issue #936 (G2) — Provider quota / balance.
 * 200 with quotaSupported:false for unsupported providers (no fake values).
 */
export const ProviderQuotaSchema = z.object({
  providerName: z.string(),
  quotaSupported: z.boolean(),
  tokenConfigured: z.boolean(),
  usedUsd: z.number().nullable(),
  limitUsd: z.number().nullable(),
  remainingUsd: z.number().nullable(),
  resetAt: z.string().nullable(),
  errorCode: z.string().nullable(),
  errorMessage: z.string().nullable(),
  fetchedAt: z.string(),
  cacheTtlSeconds: z.number(),
});

export type ProviderQuota = z.infer<typeof ProviderQuotaSchema>;

/**
 * Static list of provider names registered in backend DI.
 * Mirrors `InfrastructureServiceExtensions.cs` registration block.
 */
export const KNOWN_PROVIDERS = ['openrouter', 'deepseek', 'ollama-local'] as const;
export type ProviderName = (typeof KNOWN_PROVIDERS)[number];

/**
 * Issue #1859 — Rotate provider API key (superadmin + step-up 2FA).
 *
 * Request: typed-confirm input (`confirmedProviderName`) must match the URL
 * provider name; the new key is sent in `newApiKey`. The BE probes the new
 * key against the live provider before persisting (fail-fast on bad keys).
 */
export interface RotateProviderKeyRequest {
  readonly newApiKey: string;
  readonly confirmedProviderName: string;
}

/**
 * Issue #1859 — Rotate provider API key response.
 *
 * On success the BE returns a short fingerprint of the new key (display once),
 * the rotation timestamp, and the timestamp at which the previous key was
 * disabled (used for audit display).
 */
export interface RotateProviderKeyResponse {
  readonly providerName: string;
  readonly newKeyFingerprint: string;
  readonly rotatedAt: string;
  readonly previousKeyDisabledAt: string;
}
