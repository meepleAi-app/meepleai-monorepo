export type EntityType =
  'game' | 'player' | 'session' | 'agent' | 'kb' | 'chat' | 'event' | 'toolkit' | 'tool';

export interface EntityToken {
  readonly bg: string;
  readonly bgSoft: string;
  readonly text: string;
  readonly border: string;
  readonly emoji: string;
  readonly label: string;
}

// Map "kb" -> tailwind class "document" (pre-existing naming)
const TAILWIND_KEY: Record<EntityType, string> = {
  game: 'game',
  player: 'player',
  session: 'session',
  agent: 'agent',
  kb: 'document',
  chat: 'chat',
  event: 'event',
  toolkit: 'toolkit',
  tool: 'tool',
};

const EMOJI: Record<EntityType, string> = {
  game: '🎲',
  player: '👤',
  session: '🎯',
  agent: '🤖',
  kb: '📚',
  chat: '💬',
  event: '🗓️',
  toolkit: '🧰',
  tool: '🔧',
};

const LABEL: Record<EntityType, string> = {
  game: 'Gioco',
  player: 'Giocatore',
  session: 'Sessione',
  agent: 'Agente',
  kb: 'Base di conoscenza',
  chat: 'Chat',
  event: 'Evento',
  toolkit: 'Toolkit',
  tool: 'Tool',
};

/**
 * Returns Tailwind class strings for entity theming (compile-time, ESLint-friendly).
 *
 * Use this helper for `className` props. For runtime inline styles
 * (`style={{ color: ..., background: ... }}`) where you need actual HSL values,
 * use `entityHsl()` from `@/components/ui/data-display/meeple-card/tokens` instead.
 *
 * @example
 *   const t = getEntityToken('game');
 *   <span className={`${t.bgSoft} ${t.text}`}>{t.emoji} {t.label}</span>
 *
 * @see {@link entityHsl} (`@/components/ui/data-display/meeple-card/tokens`) —
 *   runtime HSL accessor for inline-style use cases. Spec reference:
 *   `admin-mockups/design_handoff/DESIGN_TOKENS.md § "Helper TypeScript"`.
 */
export function getEntityToken(type: EntityType): EntityToken {
  const k = TAILWIND_KEY[type];
  return {
    bg: `bg-entity-${k}`,
    bgSoft: `bg-entity-${k}/10`,
    // `-text` variant, not the base entity color (#3917). The base hues are
    // calibrated for non-text use (borders, fills, decorative accents) and sit
    // around 3:1 on light surfaces; as TEXT on the `bgSoft` tint they fall just
    // under AA. Measured on /shared-games before this change:
    //   toolkit  #17823e on #e8f3ec = 4.29:1  (needs 4.5)
    //   agent    #9d6607 on #f5f0e6 = 4.26:1  (needs 4.5)
    // The `-text` variants exist precisely for this — see the per-token WCAG
    // annotations in design-tokens-canonical.css, one of which already records
    // "replaces text-entity-toolkit (4.16:1 fail on toolkit/12 bg)". They are
    // theme-aware (the canonical file overrides them under [data-theme=dark]),
    // so this is correct in both themes.
    text: `text-entity-${k}-text`,
    border: `border-entity-${k}`,
    emoji: EMOJI[type],
    label: LABEL[type],
  };
}

export const ENTITY_TOKENS: readonly EntityType[] = [
  'game',
  'player',
  'session',
  'agent',
  'kb',
  'chat',
  'event',
  'toolkit',
  'tool',
] as const;
