/**
 * Game Rules Hook (Issue #4084)
 *
 * React Query hook for the game's published rule specifications. Consumed by
 * the "Regole" tab preview on `/games/[id]` — lazy, gated by tab.
 *
 * Before #4084 the tab passed a hardcoded `sections={[]}` literal instead of
 * calling anything: with a thousand rows in `rule_specs` the tab would have
 * rendered identically. This hook is the fix for that, not a guarantee that
 * rules exist — `GET /api/v1/games/{gameId}/rules` returns `[]` honestly when
 * none have been published, same as the full `/games/[id]/rules` page.
 */

import { useQuery } from '@tanstack/react-query';

import type { GameDetailRuleSection } from '@/components/features/game-detail/GameDetailRulesAccordion';
import { api } from '@/lib/api';
import type { RuleSpec } from '@/lib/api/schemas';

/**
 * Maps the game's rule specifications into the sections the tab renders.
 *
 * Uses only the LATEST spec (the backend orders `rule_specs` by `createdAt`
 * descending, so `ruleSpecs[0]` is it) and groups its atoms by `section`.
 * `RuleAtomDto.Section` is null for every atom the current PDF generator
 * produces (it isn't wired to write anything today — see #4084), so until a
 * real source populates `section`, this collapses to one bucket titled
 * `fallbackTitle` holding every atom's text. That is a correct, honest
 * reflection of what's in the data — this function does not hide or
 * truncate it, because a wall of text here is a sign the upstream data is
 * bad, not a UI bug to paper over.
 */
export function mapRuleSpecsToSections(
  ruleSpecs: readonly RuleSpec[] | undefined,
  fallbackTitle: string
): GameDetailRuleSection[] {
  const latest = ruleSpecs?.[0];
  if (!latest || latest.atoms.length === 0) {
    return [];
  }

  const bySection = new Map<string, string[]>();
  for (const atom of latest.atoms) {
    const key = atom.section?.trim() || fallbackTitle;
    const texts = bySection.get(key) ?? [];
    texts.push(atom.text);
    bySection.set(key, texts);
  }

  return Array.from(bySection.entries()).map(([title, texts], index) => ({
    id: `${latest.id}-${index}`,
    title,
    summary: texts.join('\n'),
  }));
}

export interface GameRulesOptions {
  /** External gating (lazy fetch only when the Rules tab is active). Default true. */
  enabled?: boolean;
}

export const GAME_RULES_QUERY_KEY = (gameId: string) => ['game-rules', gameId] as const;

/**
 * Fetches the published rule specifications for a game.
 * @param gameId Game ID (GUID format)
 * @param options `enabled` for lazy gating (default true).
 */
export function useGameRules(gameId: string, options?: GameRulesOptions) {
  const { enabled = true } = options ?? {};
  return useQuery<RuleSpec[], Error>({
    queryKey: GAME_RULES_QUERY_KEY(gameId),
    queryFn: () => api.games.getRules(gameId),
    staleTime: 5 * 60 * 1000,
    retry: 2,
    enabled: !!gameId && enabled,
  });
}
