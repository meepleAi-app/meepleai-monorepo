/**
 * mapNightLive — #2633 Slice B (LD-1).
 *
 * PURE projection from the shipped `GameNightLiveDto` (GET /game-nights/{id}/live)
 * onto the `NightLiveViewModel` the `NightLiveHub` renders. The single seam type
 * `NightLiveViewModel` is what Slice C extends (it fills currentGame + the diary
 * arrays + winner resolution by changing only its own branch, never the view wiring).
 *
 * Purity contract (LD-1, LD-7): the clock is INJECTED via `now`. This function
 * must never read `Date.now()`/`new Date()` without an argument, `performance.now()`,
 * `Math.random()`, or module-level mutable state — so every field is deterministic
 * and table-testable. The hook seeds `now` from a `useNow(60_000)` tick.
 */

import type {
  DiaryEvent,
  DiaryGameRef,
  DiaryPlayerRef,
  NightLiveHubCurrentGame,
  NightLiveHubNight,
  NightLiveStatus,
  PlannedGame,
  PlannedGameStatus,
} from '@/components/features/game-nights/live';
import type {
  GameNightLiveDto,
  GameNightSessionDto,
  GameNightSessionStatus,
  GameNightStatus,
} from '@/lib/api/schemas/game-nights.schemas';
import { hashToHue } from '@/lib/games/cover-utils';

/** The single seam type Slice B produces and Slice C extends. */
export interface NightLiveViewModel {
  readonly night: NightLiveHubNight;
  /** Raw GameNight lifecycle status — drives terminal-night routing (LD-14). */
  readonly nightStatus: GameNightStatus;
  /** WS1 DEC-9: gates the organizer-only "Avvia prossimo gioco" CTA. */
  readonly isViewerOrganizer: boolean;
  readonly status: NightLiveStatus;
  readonly current: number;
  readonly total: number;
  readonly elapsed: string;
  readonly confirmedPlayers?: number;
  readonly totalPlayers?: number;
  readonly plannedGames: readonly PlannedGame[];
  readonly currentGame: NightLiveHubCurrentGame | null;
  readonly diaryEvents: readonly DiaryEvent[];
  readonly diaryGames: readonly DiaryGameRef[];
  readonly diaryPlayers: readonly DiaryPlayerRef[];
}

const MINUTE_MS = 60_000;

/** Sessions that count as "played" for the progress counter (LD-6). */
const TERMINAL: ReadonlySet<GameNightSessionStatus> = new Set([
  'Completed',
  'Skipped',
  'Corrupted',
]);

/**
 * LD-4: total 5→3 projection. Exhaustive (the `never` default makes a new BE
 * status a compile break, not a silent `default → 'upcoming'` misrender).
 * Skipped/Corrupted collapse to `completed` (past/terminal), differentiated at
 * render time via `muted` (Skipped) and a non-empty fallback title (Corrupted).
 */
export function toPlannedGameStatus(status: GameNightSessionStatus): PlannedGameStatus {
  switch (status) {
    case 'Pending':
      return 'upcoming';
    case 'InProgress':
      return 'inprogress';
    case 'Completed':
      return 'completed';
    case 'Skipped':
      return 'completed';
    case 'Corrupted':
      return 'completed';
    default: {
      const exhaustive: never = status;
      return exhaustive;
    }
  }
}

/** LD-9: deterministic 2-stop cover gradient from the game id (BGG-ban compliant). */
function coverFromGameId(gameId: string): readonly [string, string] {
  const h1 = hashToHue(gameId);
  const h2 = (h1 + 40) % 360;
  return [`hsl(${h1} 40% 30%)`, `hsl(${h2} 45% 38%)`];
}

function minutesBetween(fromIso: string, toMs: number): number {
  const fromMs = new Date(fromIso).getTime();
  return Math.max(0, Math.floor((toMs - fromMs) / MINUTE_MS));
}

/**
 * LD-7: per-game elapsed as `'Nm'`.
 *  - Completed: CompletedAt − StartedAt (stable for any `now`)
 *  - InProgress: now − StartedAt (the only `now`-dependent value)
 *  - Pending/Skipped/Corrupted, or missing StartedAt: undefined
 */
function toActual(session: GameNightSessionDto, nowMs: number): string | undefined {
  if (!session.startedAt) return undefined;
  if (session.status === 'Completed') {
    if (!session.completedAt) return undefined;
    return `${minutesBetween(session.startedAt, new Date(session.completedAt).getTime())}m`;
  }
  if (session.status === 'InProgress') {
    return `${minutesBetween(session.startedAt, nowMs)}m`;
  }
  return undefined;
}

/** LD-4: Corrupted (or otherwise blank) titles never render empty. */
function toTitle(session: GameNightSessionDto): string {
  const trimmed = session.gameTitle.trim();
  if (trimmed.length > 0) return trimmed;
  return session.status === 'Corrupted' ? 'Partita non disponibile' : 'Partita';
}

/** LD-7: header elapsed as `'Hh Mm'` from the earliest StartedAt to `now`. */
function formatElapsed(fromMs: number, toMs: number): string {
  const total = Math.max(0, Math.floor((toMs - fromMs) / MINUTE_MS));
  return `${Math.floor(total / 60)}h ${total % 60}m`;
}

export function mapNightLiveToViewModel(dto: GameNightLiveDto, now: Date): NightLiveViewModel {
  const nowMs = now.getTime();
  const sessions = [...dto.sessions].sort((a, b) => a.playOrder - b.playOrder);

  const plannedGames: PlannedGame[] = sessions.map(session => ({
    id: session.sessionId, // LD-5: SessionId, not GameId
    title: toTitle(session),
    status: toPlannedGameStatus(session.status),
    order: session.playOrder,
    actual: toActual(session, nowMs),
    cover: coverFromGameId(session.gameId),
    winnerId: session.winnerId ?? undefined, // LD-2: carried for Slice C
    muted: session.status === 'Skipped' ? true : undefined, // LD-4
  }));

  const total = sessions.length;

  // LD-6: current = PlayOrder of the (lowest, if racy) InProgress session;
  // if none InProgress, the count of terminal sessions. Clamped, never throws.
  const inProgress = sessions.filter(s => s.status === 'InProgress');
  const rawCurrent =
    inProgress.length > 0
      ? Math.min(...inProgress.map(s => s.playOrder))
      : sessions.filter(s => TERMINAL.has(s.status)).length;
  const current = Math.max(0, Math.min(rawCurrent, total));

  // LD-7: elapsed from the earliest StartedAt across all sessions.
  const startTimes = sessions
    .map(s => s.startedAt)
    .filter((v): v is string => v != null)
    .map(v => new Date(v).getTime());
  const elapsed = startTimes.length > 0 ? formatElapsed(Math.min(...startTimes), nowMs) : '0h 0m';

  // LD-8: 'live' iff a session is running; no 'paused' signal from the BE.
  const status: NightLiveStatus = inProgress.length > 0 ? 'live' : 'transition';

  // Slice C1: currentGame is the InProgress session (lowest PlayOrder if racy).
  // `inProgress` preserves the play-order sort, so [0] is the lowest. emoji/score
  // stay undefined (fixture-only, D-SCORE/TIME); the diary is Slice C2/C4.
  const currentSession = inProgress[0];
  const currentGame: NightLiveHubCurrentGame | null = currentSession
    ? {
        id: currentSession.gameId,
        sessionId: currentSession.sessionId,
        title: toTitle(currentSession),
        cover: coverFromGameId(currentSession.gameId),
      }
    : null;

  return {
    night: { title: dto.title }, // LD-15: shortTitle/nightCode undefined in Slice B
    nightStatus: dto.status, // LD-14: raw lifecycle status for terminal routing
    isViewerOrganizer: dto.isViewerOrganizer, // WS1 DEC-9
    status,
    current,
    total,
    elapsed,
    confirmedPlayers: undefined, // LD-8: no RSVP fetch in Slice B
    totalPlayers: undefined,
    plannedGames,
    currentGame, // Slice C1
    diaryEvents: [], // LD-3: Slice C2/C4
    diaryGames: [],
    diaryPlayers: [],
  };
}
