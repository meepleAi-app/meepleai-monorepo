/**
 * Admin Providers Sub-Client (Issue #936 — G1+G2 frontend integration)
 *
 * Probe + quota for LLM providers (OpenRouter, DeepSeek, Ollama-local).
 *
 * Note: `getCircuitBreakerStates` is exposed by `adminMonitorClient` and
 * `getLlmSystemConfig` by `adminAiClient` — both are spread into the unified
 * `api.admin` namespace, so providers-related FE consumers can use them
 * directly. They are NOT re-declared here to avoid type conflicts in the
 * spread composition.
 */

import {
  ProviderProbeResultSchema,
  ProviderQuotaSchema,
  ProviderQuotaListSchema,
  KNOWN_PROVIDERS,
  type ProviderProbeResult,
  type ProviderQuota,
  type ProviderName,
} from '../../schemas/providers';

import type { HttpClient } from '../../core/httpClient';

export function createAdminProvidersClient(http: HttpClient) {
  return {
    /**
     * GET /api/v1/admin/providers/{name}/quota — Admin or above.
     * Server cache 5 minutes; 200 with quotaSupported:false for unsupported providers.
     */
    async getProviderQuota(name: ProviderName): Promise<ProviderQuota | null> {
      return http.get(
        `/api/v1/admin/providers/${encodeURIComponent(name)}/quota`,
        ProviderQuotaSchema
      );
    },

    /**
     * GET /api/v1/admin/providers/quota — Admin or above. Issue #3043.
     * Aggregated quota over quota-capable providers (openrouter, deepseek) in a single
     * fetch; server cache 5min per provider. Does NOT include ollama-local (no quota API).
     */
    async getProvidersQuota(): Promise<ProviderQuota[]> {
      const result = await http.get('/api/v1/admin/providers/quota', ProviderQuotaListSchema);
      return result ?? [];
    },

    /**
     * POST /api/v1/admin/providers/{name}/probe — SuperAdmin only.
     * Optional `model` query parameter for explicit model availability check.
     * Rate-limited 10/min per user.
     */
    async probeProvider(name: ProviderName, model?: string): Promise<ProviderProbeResult> {
      const url = `/api/v1/admin/providers/${encodeURIComponent(name)}/probe${
        model ? `?model=${encodeURIComponent(model)}` : ''
      }`;
      return http.post(url, undefined, ProviderProbeResultSchema);
    },

    /** Static list of provider names registered in backend DI. */
    listKnownProviders(): readonly ProviderName[] {
      return KNOWN_PROVIDERS;
    },
  };
}

export type AdminProvidersClient = ReturnType<typeof createAdminProvidersClient>;
