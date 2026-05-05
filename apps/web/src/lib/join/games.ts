/**
 * Public Alpha waitlist — game-preference catalog & feature highlights.
 *
 * Wave A.2 (`/join` route migration). Spec §3.3 / §3.4 i18n binding.
 * Mockup: admin-mockups/design_files/sp3-join.jsx (TOP_GAMES + ALPHA_FEATURES).
 *
 * Both arrays expose i18n keys (`labelKey`, `titleKey`, `descKey`) — call sites
 * resolve via `useTranslation().t(entry.labelKey)` so EN/IT switch without touching
 * this catalog. The static `label` field is the IT fallback for tests / SSR until
 * intl context hydrates, and stays in sync with `messages/it.json#pages.join.games`.
 *
 * Keep IDs in lockstep with the backend allowlist
 * `apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/Waitlist/JoinWaitlistCommandValidator.cs#AllowedGameIds`.
 */

export interface GamePreference {
  /** Stable ID — must match backend `AllowedGameIds`. */
  readonly id: string;
  /** IT fallback label (mirrors `pages.join.games.<key>` in it.json). */
  readonly label: string;
  /** i18n key resolved at the call site via `t()`. */
  readonly labelKey: string;
  /** Decorative emoji — purely visual, not announced (aria-hidden in UI). */
  readonly emoji: string;
}

export interface AlphaFeature {
  /** Entity color token — drives `entityHsl(entity)` accent. */
  readonly entity: 'kb' | 'agent' | 'session';
  readonly emoji: string;
  /** i18n key for the card title. */
  readonly titleKey: string;
  /** i18n key for the short description. */
  readonly descKey: string;
}

/**
 * Top 10 most-requested games + `g-other` escape hatch.
 *
 * `g-other` MUST be last; UI relies on it to gate the free-text "specify" input.
 * Order is also the visual order of the listbox.
 */
export const TOP_GAMES: readonly GamePreference[] = [
  { id: 'g-azul', label: 'Azul', labelKey: 'pages.join.games.azul', emoji: '🔷' },
  { id: 'g-catan', label: 'I Coloni di Catan', labelKey: 'pages.join.games.catan', emoji: '🌾' },
  { id: 'g-wingspan', label: 'Wingspan', labelKey: 'pages.join.games.wingspan', emoji: '🦜' },
  { id: 'g-brass', label: 'Brass: Birmingham', labelKey: 'pages.join.games.brass', emoji: '🏭' },
  { id: 'g-gloomhaven', label: 'Gloomhaven', labelKey: 'pages.join.games.gloomhaven', emoji: '⚔️' },
  { id: 'g-arknova', label: 'Ark Nova', labelKey: 'pages.join.games.arknova', emoji: '🦒' },
  { id: 'g-spirit', label: 'Spirit Island', labelKey: 'pages.join.games.spirit', emoji: '🌋' },
  {
    id: 'g-7wonders',
    label: '7 Wonders Duel',
    labelKey: 'pages.join.games.sevenWonders',
    emoji: '🏛️',
  },
  {
    id: 'g-carcassonne',
    label: 'Carcassonne',
    labelKey: 'pages.join.games.carcassonne',
    emoji: '🏰',
  },
  { id: 'g-ticket', label: 'Ticket to Ride', labelKey: 'pages.join.games.ticket', emoji: '🚂' },
  { id: 'g-other', label: 'Altro…', labelKey: 'pages.join.games.other', emoji: '✦' },
] as const;

/** Sentinel ID — when selected, GamePreferenceSelect reveals the free-text input. */
export const GAME_OTHER_ID = 'g-other' as const;

/**
 * Three Alpha-program highlights rendered in `JoinHero` (and stacked below the form on mobile).
 * Order is intentional (KB → Agent → Session = onboarding flow).
 */
export const ALPHA_FEATURES: readonly AlphaFeature[] = [
  {
    entity: 'kb',
    emoji: '📚',
    titleKey: 'pages.join.features.kb.title',
    descKey: 'pages.join.features.kb.desc',
  },
  {
    entity: 'agent',
    emoji: '🤖',
    titleKey: 'pages.join.features.agent.title',
    descKey: 'pages.join.features.agent.desc',
  },
  {
    entity: 'session',
    emoji: '🎯',
    titleKey: 'pages.join.features.session.title',
    descKey: 'pages.join.features.session.desc',
  },
] as const;

/**
 * Type guard — `true` when the id is in the allowlist.
 * Backend validator is the source of truth; this is for client-side UX only
 * (disables submit early, prevents weird drift if imported in tests).
 */
export function isAllowedGameId(id: string): boolean {
  return TOP_GAMES.some(g => g.id === id);
}
