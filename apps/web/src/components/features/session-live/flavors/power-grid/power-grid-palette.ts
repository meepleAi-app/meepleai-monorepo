import type { PowerGridResource } from './power-grid-state';

// The 4 Power Grid resources — inline hsl() applied via `style` (like the sibling palettes).
// Any hue that trips meepleai/no-inline-hsl-v2 carries a line-level disable with a reason.
const RESOURCE_HSL: Record<PowerGridResource, string> = {
  coal: 'hsl(25, 30%, 30%)',
  oil: 'hsl(0, 0%, 18%)',
  garbage: 'hsl(75, 45%, 42%)',
  uranium: 'hsl(0, 70%, 48%)',
};

export function powerGridResourceColor(resource: PowerGridResource): string {
  return RESOURCE_HSL[resource];
}
