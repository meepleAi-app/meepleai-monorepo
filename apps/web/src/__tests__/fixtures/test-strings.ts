// apps/web/src/__tests__/fixtures/test-strings.ts
/**
 * Centralised UI string constants for test assertions.
 *
 * RATIONALE: Components use hardcoded Italian strings (not useIntl).
 * Centralising here means a language change requires updating ONE file,
 * not 400+ test files.
 *
 * CONVENTION: When writing NEW tests, prefer getByRole over getByText.
 * Use these constants only when a role-based selector is not available.
 *
 * @see docs/superpowers/plans/2026-04-12-test-suite-resilience.md
 */

// ─── Empty States ────────────────────────────────────────────────────────────
export const EMPTY = {
  notifications: 'Nessuna notifica',
  notificationsOfType: 'Nessuna notifica di questo tipo',
  notificationsUnread: 'Nessuna notifica non letta',
  sessions: 'Nessuna partita',
  sessionsActive: 'Nessuna partita in corso',
  proposals: 'Nessuna Proposta',
  photos: 'Nessuna foto ancora',
  tiers: 'Nessun tier trovato.',
  toolkit: 'Nessun toolkit configurato',
} as const;

// ─── Loading States ───────────────────────────────────────────────────────────
export const LOADING = {
  proposals: 'Caricamento proposte...',
  tiers: 'Caricamento tier',
  saving: 'Salvataggio',
} as const;

// ─── Error States ─────────────────────────────────────────────────────────────
export const ERROR = {
  loading: 'Errore di Caricamento',
  network: 'Errore di rete',
  tiersLoad: 'Errore nel caricamento dei tier.',
} as const;

// ─── Greetings / Onboarding ───────────────────────────────────────────────────
export const GREETING = {
  welcome: 'Benvenuto!',
  activeSession: 'Hai una partita in corso',
} as const;

// ─── Profile / Library ───────────────────────────────────────────────────────
export const PROFILE = {
  gameHistory: 'Storia di gioco',
} as const;

// ─── Admin ───────────────────────────────────────────────────────────────────
export const ADMIN = {
  editTierPrefix: 'Modifica tier: ',
} as const;

// ─── Session ─────────────────────────────────────────────────────────────────
export const SESSION = {
  exitButton: '← Esci',
} as const;
