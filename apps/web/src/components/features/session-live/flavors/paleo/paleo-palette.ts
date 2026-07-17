import type { PaleoStatus } from './paleo-state';

// The 3 Paleo tribe statuses — inline hsl() applied via `style` (like catan/puerto-rico palettes).
// Any hue that trips meepleai/no-inline-hsl-v2 carries a line-level disable with a reason.
const STATUS_HSL: Record<PaleoStatus, string> = {
  alive: 'hsl(142, 55%, 42%)',
  // eslint-disable-next-line meepleai/no-inline-hsl-v2 -- Paleo wounded tribe color, not the agent entity token
  wounded: 'hsl(38, 90%, 50%)',
  dead: 'hsl(0, 0%, 45%)',
};

export function paleoStatusColor(status: PaleoStatus): string {
  return STATUS_HSL[status];
}
