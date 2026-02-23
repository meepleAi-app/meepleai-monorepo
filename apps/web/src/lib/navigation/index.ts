/**
 * Navigation Link Helpers
 * Issue #5039 — Consolidate User Routes
 *
 * Single source of truth for all internal navigation links.
 * Use these helpers instead of hardcoding paths to ensure
 * redirects and route changes are automatically picked up.
 */

// ─── Types ────────────────────────────────────────────────────────────────────

export interface NavigationLinks {
  // ── Home / Dashboard ──────────────────────────────────────────────────────
  home: string;

  // ── Library ───────────────────────────────────────────────────────────────
  library: string;
  libraryGames: string;       // personal games (default, was libraryPrivate)
  libraryCollection: string;  // shared catalog games
  libraryWishlist: string;
  /** @deprecated use libraryGames instead */
  libraryPrivate: string;
  /** @deprecated use /library?action=add instead */
  libraryPrivateAdd: string;
  libraryProposals: string;
  libraryPropose: string;
  libraryGame: (gameId: string) => string;
  libraryGameTab: (gameId: string, tab: LibraryGameTab) => string;

  // ── Discover / Community ──────────────────────────────────────────────────
  discover: string;
  discoverGame: (gameId: string) => string;
  discoverAdd: string;
  discoverProposals: string;

  // ── Agents ────────────────────────────────────────────────────────────────
  agents: string;
  agentSlots: string;
  agentDetail: (agentId: string) => string;

  // ── Chat ──────────────────────────────────────────────────────────────────
  chat: string;
  chatNew: string;
  chatThread: (threadId: string) => string;

  // ── Sessions ──────────────────────────────────────────────────────────────
  sessions: string;
  sessionsHistory: string;
  sessionDetail: (sessionId: string) => string;
  sessionsJoin: (token: string) => string;

  // ── Play Records ──────────────────────────────────────────────────────────
  playRecords: string;
  playRecordsNew: string;
  playRecordsStats: string;
  playRecordDetail: (id: string) => string;

  // ── Profile ───────────────────────────────────────────────────────────────
  profile: string;
  profileAchievements: string;
  profileBadges: string;
  profileSettings: string;
  profileNotifications: string;
  profileSecurity: string;

  // ── Notifications ─────────────────────────────────────────────────────────
  notifications: string;
}

export type LibraryGameTab =
  | 'overview'
  | 'agent'
  | 'toolkit'
  | 'faq'
  | 'reviews'
  | 'rules'
  | 'sessions'
  | 'strategies';

// ─── Implementation ───────────────────────────────────────────────────────────

/**
 * getNavigationLinks
 *
 * Returns the canonical URL for every user-facing route after
 * the Issue #5039 consolidation. Always import from here instead
 * of hardcoding paths in components.
 *
 * @example
 * import { getNavigationLinks } from '@/lib/navigation';
 * const links = getNavigationLinks();
 * router.push(links.libraryWishlist);
 */
export function getNavigationLinks(): NavigationLinks {
  return {
    // ── Home / Dashboard ────────────────────────────────────────────────────
    home: '/',

    // ── Library ─────────────────────────────────────────────────────────────
    library: '/library',
    libraryGames: '/library',                     // personal games (default, Issue #5167)
    libraryCollection: '/library?tab=collection', // shared catalog games (Issue #5167)
    libraryWishlist: '/library?tab=wishlist',
    libraryPrivate: '/library',                   // @deprecated → use libraryGames
    libraryPrivateAdd: '/library?action=add',     // @deprecated → use ?action=add drawer
    libraryProposals: '/discover?tab=proposals',
    libraryPropose: '/discover/propose',
    libraryGame: (gameId: string) => `/library/${gameId}`,
    libraryGameTab: (gameId: string, tab: LibraryGameTab) =>
      tab === 'overview'
        ? `/library/${gameId}`
        : `/library/${gameId}?tab=${tab}`,

    // ── Discover / Community ────────────────────────────────────────────────
    discover: '/discover',
    discoverGame: (gameId: string) => `/discover/${gameId}`,
    discoverAdd: '/discover/add',
    discoverProposals: '/discover?tab=proposals',

    // ── Agents ──────────────────────────────────────────────────────────────
    agents: '/agents',
    agentSlots: '/agents?tab=slots',
    agentDetail: (agentId: string) => `/agents/${agentId}`,

    // ── Chat ────────────────────────────────────────────────────────────────
    chat: '/chat',
    chatNew: '/chat/new',
    chatThread: (threadId: string) => `/chat/${threadId}`,

    // ── Sessions ────────────────────────────────────────────────────────────
    sessions: '/sessions',
    sessionsHistory: '/sessions?tab=history',
    sessionDetail: (sessionId: string) => `/sessions/${sessionId}`,
    sessionsJoin: (token: string) => `/sessions/join/${token}`,

    // ── Play Records ────────────────────────────────────────────────────────
    playRecords: '/play-records',
    playRecordsNew: '/play-records/new',
    playRecordsStats: '/play-records?tab=stats',
    playRecordDetail: (id: string) => `/play-records/${id}`,

    // ── Profile ─────────────────────────────────────────────────────────────
    profile: '/profile',
    profileAchievements: '/profile?tab=achievements',
    profileBadges: '/profile?tab=badges',
    profileSettings: '/profile?tab=settings',
    profileNotifications: '/profile?tab=settings&section=notifications',
    profileSecurity: '/profile?tab=settings&section=security',

    // ── Notifications ───────────────────────────────────────────────────────
    notifications: '/notifications',
  };
}
