/**
 * Gamebook quota API client (#2750 C14, follow-up to #869b).
 *
 * Endpoint: GET /api/v1/users/me/quota
 *
 * Returns the authenticated user's monthly gamebook paragraph-translation quota
 * (display-only, no enforcement). Replaces the v1 carryover fixture stub in
 * `useQuotaInfo` (`gamebookIndexFixtures.default.quota`).
 *
 * Backend handler: `GetGamebookQuotaQueryHandler` (Administration BC).
 */

import { quotaInfoSchema, type QuotaInfo } from '@/lib/gamebook-index/schemas';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

export async function fetchUserQuota(signal?: AbortSignal): Promise<QuotaInfo> {
  const res = await fetch(`${API_BASE}/api/v1/users/me/quota`, {
    credentials: 'include',
    signal,
  });

  if (!res.ok) {
    let detail = '';
    try {
      detail = await res.text();
    } catch {
      /* ignore body read failure */
    }
    throw new Error(`Quota API error ${res.status}: ${detail || res.statusText}`);
  }

  const raw = (await res.json()) as unknown;
  return quotaInfoSchema.parse(raw);
}
