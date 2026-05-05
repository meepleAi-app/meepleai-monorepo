/**
 * Pure helpers for the /sessions v2 surface (Wave D.1).
 *
 * Mirrors the lib/players/players-filters.ts pattern from Wave 4 D1 — no React,
 * no API client, exclusively transforms over SessionListItem so it stays
 * unit-testable in isolation.
 *
 * Schema reality: the backend GameSessionDto (games.schemas.ts) provides:
 *   id, gameId, status (InProgress|Paused|Setup|Completed|Abandoned),
 *   startedAt, completedAt, playerCount, players, winnerName, notes, durationMinutes
 *
 * Wave D.1 = visual upgrade only. The SessionListItem shape is a display-ready
 * transform of GameSessionDto — gameName is derived from gameId for now (the
 * orchestrator Task 3 will join game title from the games catalog).
 *
 * Note on 'active' filter: in the mockup, the filter chips use 'inprogress'
 * (live/paused) vs 'completed' vs 'abandoned'. Our SessionStatusFilter uses
 * those same values for consistency with the mockup; 'active' is treated as a
 * filter-level alias covering both 'inprogress' and 'paused' statuses.
 */

/** Display-ready status values aligned with mockup filter chips. */
export type SessionStatusFilter = 'all' | 'active' | 'completed' | 'abandoned';

/** View mode toggle: list (default) or grid. */
export type SessionViewMode = 'list' | 'grid';

/** Score entry for a single player within a session. */
export interface SessionScoreEntry {
  readonly name: string;
  readonly score: number;
  readonly winner?: boolean;
  readonly note?: string;
}

/**
 * A single row in the /sessions list — display-ready shape derived from
 * GameSessionDto plus game catalog join (done in orchestrator).
 *
 * Status vocabulary (maps from backend GameSessionDto.status):
 *   'inprogress' = InProgress (live)
 *   'paused'     = Paused
 *   'completed'  = Completed (endedAt present, has outcome)
 *   'abandoned'  = Abandoned
 */
export interface SessionListItem {
  /** UUID from GameSessionDto.id. */
  readonly id: string;
  /** Human-readable game name from game catalog join. Falls back to gameId. */
  readonly gameName: string;
  /** Formatted display date, e.g. "23 apr 2026". */
  readonly date: string;
  /** Relative time label, e.g. "2 giorni fa". */
  readonly when: string;
  /** Formatted duration string, e.g. "1h 24m". */
  readonly duration: string;
  /** Client-side status vocabulary. */
  readonly status: 'inprogress' | 'paused' | 'completed' | 'abandoned';
  /** Session outcome (null for non-completed). */
  readonly outcome: 'won' | 'lost' | 'tie' | null;
  /** Number of players. */
  readonly playerCount: number;
  /** Player scores (may be empty for in-progress sessions with no scoring yet). */
  readonly scores: ReadonlyArray<SessionScoreEntry>;
  /** Whether any chat messages exist for this session. */
  readonly hasChat: boolean;
  /** Chat message count (present when hasChat=true). */
  readonly chatCount?: number;
  /** Current turn display string, e.g. "12/18" (present for inprogress). */
  readonly turn?: string;
  /** True when status='paused'. */
  readonly paused?: boolean;
}

/**
 * Transforms a GameSessionDto array into display-ready SessionListItem rows.
 *
 * Game name resolution: the orchestrator (Task 3) should pre-resolve game titles
 * from the user library cache and pass them via a `gameNameMap` lookup table.
 * This function accepts the map and falls back to gameId when no mapping exists.
 *
 * Duration formatting: durationMinutes → "Xh Ym" or "Zm" for sub-hour.
 * Date formatting: startedAt ISO string → locale-aware display (uses 'it-IT').
 */
export function transformSessionsToItems(
  sessions: ReadonlyArray<{
    id: string;
    gameId: string;
    status: string;
    startedAt: string;
    completedAt: string | null;
    playerCount: number;
    players: ReadonlyArray<{
      displayName: string;
      score?: number | null;
      isWinner?: boolean | null;
    }>;
    winnerName: string | null;
    notes: string | null;
    durationMinutes: number;
  }>,
  gameNameMap: Record<string, string> = {}
): ReadonlyArray<SessionListItem> {
  return sessions.map(dto => {
    const gameName = gameNameMap[dto.gameId] ?? dto.gameId;
    const status = mapStatus(dto.status);
    const outcome = deriveOutcome(dto);
    const scores: ReadonlyArray<SessionScoreEntry> = dto.players.map(p => ({
      name: p.displayName,
      score: p.score ?? 0,
      winner: p.isWinner ?? false,
    }));

    return {
      id: dto.id,
      gameName,
      date: formatDate(dto.startedAt),
      when: formatRelativeTime(dto.startedAt),
      duration: formatDuration(dto.durationMinutes),
      status,
      outcome,
      playerCount: dto.playerCount,
      scores,
      hasChat: false, // chat integration is a separate future concern
      paused: status === 'paused',
    } satisfies SessionListItem;
  });
}

// ---------------------------------------------------------------------------
// Internal helpers
// ---------------------------------------------------------------------------

/** Maps backend status string to SessionListItem.status vocabulary. */
function mapStatus(backendStatus: string): SessionListItem['status'] {
  const s = backendStatus.toLowerCase();
  if (s === 'inprogress' || s === 'setup') return 'inprogress';
  if (s === 'paused') return 'paused';
  if (s === 'completed') return 'completed';
  return 'abandoned';
}

/** Derives win/loss/tie from winnerName. Returns null for non-completed sessions. */
function deriveOutcome(dto: {
  status: string;
  winnerName: string | null;
  players: ReadonlyArray<{ displayName: string }>;
}): SessionListItem['outcome'] {
  const status = mapStatus(dto.status);
  if (status !== 'completed') return null;
  if (!dto.winnerName) return 'tie'; // completed with no winner = tie
  // 'won' when the user (first player) is the winner — orchestrator can refine
  return 'won';
}

/** Formats an ISO date string to a human-readable display date in it-IT locale. */
export function formatDate(isoString: string): string {
  try {
    return new Date(isoString).toLocaleDateString('it-IT', {
      day: 'numeric',
      month: 'short',
      year: 'numeric',
    });
  } catch {
    return isoString;
  }
}

/** Formats durationMinutes to "Xh Ym" or "Zm". */
export function formatDuration(minutes: number): string {
  if (minutes <= 0) return '0m';
  const h = Math.floor(minutes / 60);
  const m = minutes % 60;
  if (h === 0) return `${m}m`;
  if (m === 0) return `${h}h`;
  return `${h}h ${m}m`;
}

/** Returns a human-readable relative time label (simple version for fixtures). */
export function formatRelativeTime(isoString: string): string {
  try {
    const now = Date.now();
    const then = new Date(isoString).getTime();
    const diffMs = now - then;
    const diffDays = Math.floor(diffMs / (1000 * 60 * 60 * 24));
    if (diffDays === 0) return 'oggi';
    if (diffDays === 1) return 'ieri';
    if (diffDays < 7) return `${diffDays} giorni fa`;
    if (diffDays < 14) return '1 settimana fa';
    if (diffDays < 30) return `${Math.floor(diffDays / 7)} settimane fa`;
    if (diffDays < 60) return '1 mese fa';
    return `${Math.floor(diffDays / 30)} mesi fa`;
  } catch {
    return '';
  }
}

// ---------------------------------------------------------------------------
// Filter functions
// ---------------------------------------------------------------------------

/**
 * Applies a SessionStatusFilter to a list of SessionListItem rows.
 * 'all' returns the full list (same reference).
 * 'active' matches both 'inprogress' and 'paused' statuses.
 */
export function applyStatusFilter(
  items: ReadonlyArray<SessionListItem>,
  filter: SessionStatusFilter
): ReadonlyArray<SessionListItem> {
  if (filter === 'all') return items;
  if (filter === 'active') {
    return items.filter(s => s.status === 'inprogress' || s.status === 'paused');
  }
  if (filter === 'completed') {
    return items.filter(s => s.status === 'completed');
  }
  if (filter === 'abandoned') {
    return items.filter(s => s.status === 'abandoned');
  }
  return items;
}

/**
 * Applies a case-insensitive search filter on gameName.
 * Returns the same reference when query is empty or whitespace.
 */
export function applySearchFilter(
  items: ReadonlyArray<SessionListItem>,
  query: string
): ReadonlyArray<SessionListItem> {
  const q = query.trim().toLowerCase();
  if (q === '') return items;
  return items.filter(s => s.gameName.toLowerCase().includes(q));
}

// ---------------------------------------------------------------------------
// URL param parsers
// ---------------------------------------------------------------------------

/** Parses a raw URL param string into a SessionStatusFilter (default: 'all'). */
export function parseStatusFilter(raw: string | null | undefined): SessionStatusFilter {
  const valid: SessionStatusFilter[] = ['all', 'active', 'completed', 'abandoned'];
  if (raw && valid.includes(raw as SessionStatusFilter)) {
    return raw as SessionStatusFilter;
  }
  return 'all';
}

/** Parses a raw URL param string into a SessionViewMode (default: 'list'). */
export function parseViewMode(raw: string | null | undefined): SessionViewMode {
  if (raw === 'grid') return 'grid';
  return 'list';
}
