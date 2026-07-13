import type { MeepleEntityType } from './types';

/**
 * Entity color tokens (HSL).
 *
 * Lightness values are tuned so each entity's solid color, when used as
 * background under white text in EntityBadge (text-[9px] font-extrabold),
 * meets WCAG 2.1 AA SC 1.4.3 contrast ratio ≥ 4.5:1 for normal-size text.
 *
 * Computed contrast (vs #ffffff):
 *   game     l39 → #c25405  4.60:1
 *   player   l58 → #8b56eb  5.67:1
 *   session  l55 → #5757c2  6.80:1
 *   agent    l33 → #a26907  4.61:1
 *   kb       l48 → #497aab  4.51:1
 *   chat     l55 → #2670e6  4.64:1
 *   event    l48 → #e70d32  4.67:1
 *   toolkit  l31 → #188640  4.64:1
 *   tool     l35 → #127da1  4.69:1
 *
 * Tracked under issue #636 (audit-and-fix EntityBadge + StatusBadge debt
 * surfaced by Wave B.1 / B.2 / B.3 a11y E2E exclusions).
 */
export const entityColors: Record<MeepleEntityType, { h: number; s: string; l: string }> = {
  game: { h: 25, s: '95%', l: '39%' },
  player: { h: 262, s: '83%', l: '58%' },
  session: { h: 240, s: '60%', l: '55%' },
  agent: { h: 38, s: '92%', l: '33%' },
  kb: { h: 210, s: '40%', l: '48%' },
  chat: { h: 220, s: '80%', l: '55%' },
  event: { h: 350, s: '89%', l: '48%' },
  toolkit: { h: 142, s: '70%', l: '31%' },
  tool: { h: 195, s: '80%', l: '35%' },
  // #1929 WP2: GameNight-specific entity, reuses event/rose palette
  gameNightEvent: { h: 350, s: '89%', l: '48%' },
};

/**
 * Canonical entity -> --c-* var name. gameNightEvent reuses event's rose palette.
 */
const CSS_ENTITY: Record<MeepleEntityType, string> = {
  game: 'game',
  player: 'player',
  session: 'session',
  agent: 'agent',
  kb: 'kb',
  chat: 'chat',
  event: 'event',
  toolkit: 'toolkit',
  tool: 'tool',
  gameNightEvent: 'event',
};

/** Entity var names that have a canonical --c-*-text (AA text) variant. */
const HAS_TEXT_VAR: ReadonlySet<string> = new Set([
  'game',
  'kb',
  'toolkit',
  'session',
  'event',
  'agent',
  'chat',
]);

/**
 * #2862 (C5): theme-aware, single-source entity color. Emits the canonical
 * `--c-*` CSS var (which has light + dark values) instead of a hardcoded,
 * light-only HSL. `alpha` (0..1) tints via modern `hsl(H S L / A)` syntax.
 */
export function entityHsl(entity: MeepleEntityType, alpha?: number): string {
  const v = `var(--c-${CSS_ENTITY[entity]})`;
  return alpha !== undefined ? `hsl(${v} / ${alpha})` : `hsl(${v})`;
}

/**
 * #2862 (C5): theme-aware AA-safe entity text color. Uses the canonical
 * `--c-*-text` var for entities that have one; falls back to the solid
 * `--c-*` for player/tool (matching the pre-C5 fallback behavior).
 */
export function entityHslText(entity: MeepleEntityType, alpha?: number): string {
  const name = CSS_ENTITY[entity];
  const suffix = HAS_TEXT_VAR.has(name) ? '-text' : '';
  const v = `var(--c-${name}${suffix})`;
  return alpha !== undefined ? `hsl(${v} / ${alpha})` : `hsl(${v})`;
}

export const entityLabel: Record<MeepleEntityType, string> = {
  game: 'Game',
  player: 'Player',
  session: 'Session',
  agent: 'Agent',
  kb: 'KB',
  chat: 'Chat',
  event: 'Event',
  toolkit: 'Toolkit',
  tool: 'Tool',
  gameNightEvent: 'Game Night',
};

export const entityIcon: Record<MeepleEntityType, string> = {
  game: '🎲',
  player: '👤',
  session: '🎯',
  agent: '🤖',
  kb: '📚',
  chat: '💬',
  event: '📅',
  toolkit: '🧰',
  tool: '🔧',
  gameNightEvent: '🌙',
};

/**
 * Named derived colors for an entity.
 *
 * Replaces inline `entityHsl(entity, 0.12)` calls with semantic names.
 * Use these when styling ConnectionChip, badges, popovers, etc.
 */
export interface EntityTokens {
  solid: string; // full color — icons, badge bg
  fill: string; // 0.12 — chip bg default
  border: string; // 0.35 — chip border default
  hover: string; // 0.22 — chip bg on hover
  glow: string; // 0.18 — box-shadow spread
  shadow: string; // 0.25 — box-shadow drop
  muted: string; // 0.06 — empty/disabled bg
  dashed: string; // 0.25 — dashed border empty state
  textOn: string; // text color on solid bg
}

export function entityTokens(entity: MeepleEntityType): EntityTokens {
  return {
    solid: entityHsl(entity),
    fill: entityHsl(entity, 0.12),
    border: entityHsl(entity, 0.35),
    hover: entityHsl(entity, 0.22),
    glow: entityHsl(entity, 0.18),
    shadow: entityHsl(entity, 0.25),
    muted: entityHsl(entity, 0.06),
    dashed: entityHsl(entity, 0.25),
    textOn: '#ffffff',
  };
}

/**
 * Status color tokens (hex bg + text).
 *
 * Each pair must meet WCAG 2.1 AA SC 1.4.3 ≥ 4.5:1 for the text-[9px]
 * font-bold StatusBadge surface.
 *
 * `idle` and `archived` previously used slate-500 (#64748b) on slate-100
 * (#f1f5f9), giving 4.34:1 — fails AA. Swapped to slate-600 (#475569)
 * which yields 6.92:1 (issue #636).
 */
export const statusColors: Record<string, { bg: string; text: string }> = {
  owned: { bg: '#dcfce7', text: '#166534' },
  wishlist: { bg: '#fef3c7', text: '#92400e' },
  active: { bg: '#dcfce7', text: '#166534' },
  idle: { bg: '#f1f5f9', text: '#475569' },
  archived: { bg: '#f1f5f9', text: '#475569' },
  processing: { bg: '#dbeafe', text: '#1e40af' },
  indexed: { bg: '#dcfce7', text: '#166534' },
  failed: { bg: '#fee2e2', text: '#991b1b' },
  inprogress: { bg: '#dbeafe', text: '#1e40af' },
  setup: { bg: '#fef3c7', text: '#92400e' },
  completed: { bg: '#f3e8ff', text: '#6b21a8' },
  paused: { bg: '#fef3c7', text: '#92400e' },
};
