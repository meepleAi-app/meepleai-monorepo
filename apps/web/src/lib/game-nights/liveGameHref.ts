/**
 * Where a game of a night is played — #4752 (the night → live game bridge, ADR-083 Direction A).
 *
 * A game started from a night is a `LiveGameSession`: players, scores, turns, the assistant and
 * «Termina partita» live at `/sessions/{liveSessionId}/live`. The night page (`/game-nights/{id}/live`)
 * is the night's own dashboard, not the place a game is played.
 */

/** The live game page of a game of a night. */
export function liveGameHref(liveSessionId: string): string {
  return `/sessions/${liveSessionId}/live`;
}

/**
 * Where to go once a game has been started from the night: straight to the live game it created or,
 * if the backend did not say (an older payload), to the night page — the one place that always exists.
 */
export function startedGameHref(
  gameNightId: string,
  started: { readonly liveSessionId?: string | null }
): string {
  return started.liveSessionId
    ? liveGameHref(started.liveSessionId)
    : `/game-nights/${gameNightId}/live`;
}
