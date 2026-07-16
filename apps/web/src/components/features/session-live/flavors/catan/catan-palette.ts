/**
 * Catan flavor palette — maps the session PlayerColor enum to display hsl
 * strings applied via inline style (see #2787 plan Global Constraints). Values
 * are a Catan-leaning piece palette derived from the mockup terrain set.
 *
 * A few piece/terrain hues fall near entity-token hues by coincidence, so they
 * trip `meepleai/no-inline-hsl-v2`. They are NOT entity tokens (a blue Catan
 * piece is not a chat entity), so migrating them to getEntityToken() would be
 * semantically wrong — each carries a line-level disable with a reason instead.
 */
import type { PlayerColor } from '@/lib/api/schemas/live-sessions.schemas';

import type { CatanTerrain } from './catan-state';

export const CATAN_NEUTRAL_HSL = 'hsl(0, 0%, 60%)';

const PALETTE: Record<PlayerColor, string> = {
  Red: 'hsl(0, 70%, 50%)',
  // eslint-disable-next-line meepleai/no-inline-hsl-v2 -- Catan Blue piece color, not the chat entity token
  Blue: 'hsl(215, 70%, 50%)',
  Green: 'hsl(140, 55%, 42%)',
  Yellow: 'hsl(45, 85%, 50%)',
  Purple: 'hsl(270, 55%, 55%)',
  // eslint-disable-next-line meepleai/no-inline-hsl-v2 -- Catan Orange piece color, not the game entity token
  Orange: 'hsl(28, 85%, 52%)',
  White: 'hsl(0, 0%, 88%)',
  Black: 'hsl(0, 0%, 22%)',
  Pink: 'hsl(330, 75%, 62%)',
  // eslint-disable-next-line meepleai/no-inline-hsl-v2 -- Catan Teal piece color, not the kb-teal/document entity token
  Teal: 'hsl(175, 60%, 42%)',
};

export function catanPieceColor(color: PlayerColor | string): string {
  return PALETTE[color as PlayerColor] ?? CATAN_NEUTRAL_HSL;
}

export const CATAN_TERRAIN_HSL: Record<CatanTerrain, string> = {
  wood: 'hsl(140, 40%, 40%)',
  brick: 'hsl(8, 55%, 52%)',
  sheep: 'hsl(80, 45%, 58%)',
  // eslint-disable-next-line meepleai/no-inline-hsl-v2 -- Catan wheat terrain color, not the agent entity token
  wheat: 'hsl(42, 80%, 57%)',
  ore: 'hsl(215, 12%, 58%)',
  desert: 'hsl(43, 42%, 70%)',
};

export function catanTerrainColor(terrain: CatanTerrain): string {
  return CATAN_TERRAIN_HSL[terrain];
}
