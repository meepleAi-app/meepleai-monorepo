/**
 * Libro Game (gamebook) detection helper.
 *
 * Iter 1.A introduced a hardcoded `title === 'Nanolith'` check inside
 * NanolithCampaignCTA. Iter 2 centralizes that detection so multiple
 * surfaces (library card badge, game-detail CTA, mini-nav) share one
 * decision point.
 *
 * Future extension (Iter 4+): switch to a `shared_games.category` lookup
 * or a dedicated `is_libro_game` metadata flag once the backend exposes
 * it. Callers should remain unchanged.
 */

export interface LibroGameDetectInput {
  readonly gameTitle?: string | null;
}

/** Hardcoded allowlist — extend by category lookup later. */
const LIBRO_GAME_TITLES: ReadonlySet<string> = new Set(['Nanolith']);

export function isLibroGame({ gameTitle }: LibroGameDetectInput): boolean {
  if (!gameTitle) return false;
  return LIBRO_GAME_TITLES.has(gameTitle);
}
