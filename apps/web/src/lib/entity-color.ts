/**
 * Backward-compat alias module for the handoff convention
 * `entityColor(type, alpha?) → 'hsl(...)' | 'hsla(...)'`.
 *
 * Re-exports the canonical runtime HSL accessor `entityHsl` from
 * `@/components/ui/data-display/meeple-card/tokens` under the name expected by
 * `admin-mockups/design_handoff/DESIGN_TOKENS.md § "Helper TypeScript"`.
 *
 * Created during DS-step 3c (2026-05-24).
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

export {
  entityHsl as entityColor,
  entityIcon as entityEmoji,
} from '@/components/ui/data-display/meeple-card/tokens';

export type { MeepleEntityType as EntityType } from '@/components/ui/data-display/meeple-card/types';
