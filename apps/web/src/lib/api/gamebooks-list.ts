/**
 * Gamebooks list API client (Issue #869).
 *
 * Endpoint: GET /api/v1/gamebooks
 *
 * Returns the list of gamebook cards displayed on the SP6 libro game
 * `/gamebook` index page. Replaces the v1 carryover stub
 * `gamebookIndexFixtures.default.gamebooks` consumed by the legacy
 * `useGamebooks` hook.
 *
 * Backend handler: `GetUserGamebooksQueryHandler` (UserLibrary BC).
 */

import { z } from 'zod';

import { gamebookCardDataSchema, type GamebookCardData } from '@/lib/gamebook-index/schemas';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

const responseSchema = z.array(gamebookCardDataSchema);

export async function fetchUserGamebooks(
  signal?: AbortSignal
): Promise<readonly GamebookCardData[]> {
  const res = await fetch(`${API_BASE}/api/v1/gamebooks`, {
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
    throw new Error(`Gamebooks list API error ${res.status}: ${detail || res.statusText}`);
  }

  const raw = (await res.json()) as unknown;
  return responseSchema.parse(raw);
}
