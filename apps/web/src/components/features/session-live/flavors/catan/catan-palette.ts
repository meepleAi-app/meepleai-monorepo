/**
 * Catan flavor palette — maps the session PlayerColor enum to display hsl
 * strings applied via inline style (token-lint safe; see #2787 plan Global
 * Constraints). Values are a Catan-leaning piece palette derived from the
 * mockup terrain set.
 */
import type { PlayerColor } from '@/lib/api/schemas/live-sessions.schemas';

export const CATAN_NEUTRAL_HSL = 'hsl(0, 0%, 60%)';

const PALETTE: Record<PlayerColor, string> = {
  Red: 'hsl(0, 70%, 50%)',
  Blue: 'hsl(215, 70%, 50%)',
  Green: 'hsl(140, 55%, 42%)',
  Yellow: 'hsl(45, 85%, 50%)',
  Purple: 'hsl(270, 55%, 55%)',
  Orange: 'hsl(28, 85%, 52%)',
  White: 'hsl(0, 0%, 88%)',
  Black: 'hsl(0, 0%, 22%)',
  Pink: 'hsl(330, 75%, 62%)',
  Teal: 'hsl(175, 60%, 42%)',
};

export function catanPieceColor(color: PlayerColor | string): string {
  return PALETTE[color as PlayerColor] ?? CATAN_NEUTRAL_HSL;
}
