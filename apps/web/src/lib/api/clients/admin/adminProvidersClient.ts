/**
 * Admin Providers Sub-Client (Issue #936 — G1+G2 frontend integration)
 *
 * Probe + quota for LLM providers (OpenRouter, DeepSeek, Ollama-local).
 */

import {
  ProviderProbeResultSchema,
  ProviderQuotaSchema,
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
