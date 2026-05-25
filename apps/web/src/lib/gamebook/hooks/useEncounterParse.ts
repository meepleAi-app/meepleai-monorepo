'use client';

import { useMutation } from '@tanstack/react-query';

import {
  parseEncounter,
  type EncounterCheatsheet,
  type ParseEncounterBody,
} from '@/lib/api/gamebook-encounter';

/**
 * useEncounterParse — Issue #1484.
 *
 * Ephemeral, on-demand encounter cheatsheet parse. Modelled as a mutation (not
 * a query) because it is an explicit user action with no caching: per spec
 * §9.1 the Encounter Book result is session-scoped and never persisted, so
 * there is no query key to invalidate or revalidate.
 *
 * The route ids (`campaignId`, `photoId`) are bound at hook construction; the
 * per-invocation variables (`paragraphNumber`, `gameBookId`) are passed to
 * `mutate`. Errors are surfaced as {@link import('@/lib/api/gamebook-encounter').EncounterParseError}
 * so the orchestrator can branch on `status` (409 parse-fail / 404 missing).
 */
export function useEncounterParse(campaignId: string, photoId: string) {
  return useMutation<EncounterCheatsheet, Error, ParseEncounterBody>({
    mutationFn: vars => parseEncounter(campaignId, photoId, vars),
  });
}
