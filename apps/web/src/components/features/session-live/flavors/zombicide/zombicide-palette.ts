import type { WoundLevel } from './zombicide-state';

// The 3 Zombicide wound levels — inline hsl() applied via `style` (like the sibling palettes).
// Any hue that trips meepleai/no-inline-hsl-v2 carries a line-level disable with a reason.
const WOUND_HSL: Record<WoundLevel, string> = {
  0: 'hsl(142, 55%, 42%)',
  // eslint-disable-next-line meepleai/no-inline-hsl-v2 -- Zombicide wound level 1 colour, not the agent token
  1: 'hsl(38, 90%, 50%)',
  2: 'hsl(0, 70%, 48%)',
};

export function zombicideWoundColor(level: WoundLevel): string {
  return WOUND_HSL[level];
}
