import type { BackendTogglesState } from '@/dev-tools/types';

import { DevPanelClientError } from './devPanelErrors';

export { DevPanelClientError };

export interface PatchTogglesResponse {
  updated: string[];
  toggles: Record<string, boolean>;
}

const BASE_URL = '/dev/toggles';
const INTERNAL_HEADER = { 'X-Meepledev-Internal': '1' } as const;
const TIMEOUT_MS = 5000;

async function fetchJson<T>(input: string, init: RequestInit): Promise<T> {
  const controller = new AbortController();
  const timeoutId = setTimeout(() => controller.abort(), TIMEOUT_MS);
  try {
    const response = await fetch(input, {
      ...init,
      signal: controller.signal,
      headers: { ...INTERNAL_HEADER, 'Content-Type': 'application/json', ...(init.headers ?? {}) },
    });
    if (!response.ok) {
      let errorBody: { error?: string; message?: string } = {};
      try {
        errorBody = await response.json();
      } catch {
        /* ignore */
      }
      throw new DevPanelClientError(
        errorBody.message ?? `${input} failed with status ${response.status}`,
        response.status,
        { traceId: response.headers.get('X-Trace-Id') ?? undefined }
      );
    }
    return (await response.json()) as T;
  } catch (err) {
    if (err instanceof DevPanelClientError) throw err;
    throw new DevPanelClientError(err instanceof Error ? err.message : String(err), 0, {
      cause: err,
    });
  } finally {
    clearTimeout(timeoutId);
  }
}

export async function getToggles(): Promise<BackendTogglesState> {
  return fetchJson<BackendTogglesState>(BASE_URL, { method: 'GET' });
}

export async function patchToggles(
  toggles: Record<string, boolean>
): Promise<PatchTogglesResponse> {
  return fetchJson<PatchTogglesResponse>(BASE_URL, {
    method: 'PATCH',
    body: JSON.stringify({ toggles }),
  });
}

export async function resetToggles(): Promise<BackendTogglesState> {
  return fetchJson<BackendTogglesState>(`${BASE_URL}/reset`, { method: 'POST' });
}

export const devPanelClient = { getToggles, patchToggles, resetToggles } as const;
