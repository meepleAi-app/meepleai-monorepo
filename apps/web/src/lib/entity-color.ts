/**
 * Backward-compat alias module for the handoff convention
 * `entityColor(type, alpha?) → 'hsl(...)' | 'hsla(...)'`.
 *
 * Re-exports the canonical runtime HSL accessor `entityHsl` from
 * `@/components/ui/data-display/meeple-card/tokens` under the name expected by
 * `admin-mockups/design_handoff/DESIGN_TOKENS.md § "Helper TypeScript"`.
 *
 * Created during DS-step 3c (2026-05-24). Post-review fix C1 (2026-05-24 PM):
 * `entityEmoji` is exposed as a callable function (matching the handoff
 * spec signature), NOT as the `entityIcon` Record from `tokens.ts`.
 *
 * @example
 *   import { entityColor, entityEmoji, type EntityType } from '@/lib/entity-color';
 *   <div style={{ color: entityColor('agent'), background: entityColor('agent', 0.12) }}>
 *     {entityEmoji('agent')} Wingspan Rules
 *   </div>
 *
 * For compile-time Tailwind class strings (preferred for `className`), use
 * `getEntityToken` from `@/components/ui/entity-tokens` instead.
 */

import { entityHsl, entityIcon } from '@/components/ui/data-display/meeple-card/tokens';
import type { MeepleEntityType } from '@/components/ui/data-display/meeple-card/types';

export { entityHsl as entityColor };

/**
 * Returns the emoji glyph for the given entity type.
 * Function wrapper around the `entityIcon` Record from `meeple-card/tokens`
 * so the handoff API contract (`entityEmoji(entity)`) matches a callable signature.
 *
 * @see {@link entityIcon} (`@/components/ui/data-display/meeple-card/tokens`) — the underlying Record source.
 */
export function entityEmoji(entity: MeepleEntityType): string {
  return entityIcon[entity];
}

export type { MeepleEntityType as EntityType } from '@/components/ui/data-display/meeple-card/types';
