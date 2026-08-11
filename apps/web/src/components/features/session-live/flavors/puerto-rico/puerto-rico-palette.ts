import type { PuertoRicoGood } from './puerto-rico-state';

// The 5 Puerto Rico goods — inline hsl() applied via `style` (like catan/codenames palettes).
// Any hue that trips meepleai/no-inline-hsl-v2 carries a line-level disable with a reason.
const GOOD_HSL: Record<PuertoRicoGood, string> = {
  corn: 'hsl(48, 85%, 55%)',
  indigo: 'hsl(230, 55%, 52%)',
  sugar: 'hsl(0, 0%, 88%)',
  tobacco: 'hsl(28, 45%, 44%)',
  coffee: 'hsl(25, 40%, 26%)',
};

export function puertoRicoGoodColor(good: PuertoRicoGood): string {
  return GOOD_HSL[good];
}
