/**
 * useGameObjectivesCatalogue — Issue #2432.
 *
 * Single-source-of-truth for the `availableObjectives` list consumed by the
 * polymorphic `Objectives` scoring path (`ScoringPanelRenderer` + the host
 * editor). Replaces the previous module-level `MVP_OBJECTIVES_CATALOGUE`
 * constant so the lookup contract is centred at the hook layer; callers
 * depend on the return shape, not on the source.
 *
 * <para><b>Current behaviour</b>: the hook is a transitional stub. It returns
 * the same default catalogue for every `gameId` (or `null`) and does NOT yet
 * issue a network request. The labels are the legacy MVP placeholders kept
 * verbatim so render-paths and snapshot tests stay stable.</para>
 *
 * <para><b>Future behaviour</b> (separate follow-up issue): swap the body for
 * a TanStack Query call against a BE endpoint (e.g.
 * `GET /api/v1/games/{gameId}/objectives-catalogue`) — or, if the BE chooses
 * to inline the catalogue on the existing `GameSessionDto`, read it from
 * there via a different selector. Callers will not need to change.</para>
 */

export const DEFAULT_OBJECTIVES_CATALOGUE: readonly string[] = [
  'Vittoria',
  'Sopravvivenza',
  'Tesoro',
  'Boss',
  'Quest',
] as const;

/**
 * Returns the objectives catalogue applicable to the given game. Transitional
 * stub: every gameId resolves to {@link DEFAULT_OBJECTIVES_CATALOGUE} today;
 * the per-game lookup will be wired separately when the BE endpoint exists.
 *
 * @param gameId - The session's game identifier (currently unused; the
 *   parameter is preserved so the future implementation is a drop-in swap).
 * @returns A readonly list of objective labels — stable identity across
 *   re-renders to play well with `useMemo` dependency arrays in callers.
 */
export function useGameObjectivesCatalogue(_gameId: string | null | undefined): readonly string[] {
  return DEFAULT_OBJECTIVES_CATALOGUE;
}
