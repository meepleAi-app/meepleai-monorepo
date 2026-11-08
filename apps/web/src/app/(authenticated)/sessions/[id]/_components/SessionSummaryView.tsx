/**
 * SessionSummaryView — Wave D.3 Tier M-L orchestrator (Issue #756).
 *
 * Composes 4 hook queries + visual-fixture URL override + URL state SSOT
 * (?diary= ?theme= ?fixture=) and renders the post-game summary 6-cell FSM
 * (loading | error | not-found | not-completed | default | partial).
 *
 * Brownfield FORK pattern (contract §1.0):
 *   Pre-D.3 the route `/sessions/[id]` rendered the LIVE session UI driven by
 *   `useSessionStore`. D.3 BIG-BANG replaces that with the post-game summary.
 *   Live UI now lives at `/sessions/[id]/live` (D.2 SHIPPED). When the session
 *   status is `InProgress` / `Paused` / `Setup`, the orchestrator redirects to
 *   `/sessions/[id]/live`.
 *
 * Hook composition (#4156 — the route id is a LiveGameSession id, decision D4):
 *   1. useLiveSession(sessionId)              — parent, LiveSessionDto; adapted at the
 *      boundary into the SessionDetailsDto shape the components speak, with the real
 *      totals and per-round scores from RoundScores (ADR-089 §1)
 *   2. useSessionVisionSnapshots(sessionId)   — sub-hook on /live-sessions, after parent success
 *   3. Diary: empty. The SessionTracking diary does not exist for a live session id, and the
 *      live diary (text notes) is not something the timeline can show yet
 *   4. Achievements: no backend → empty on real data (fixtures only under ?fixture=)
 *   - Chat highlights have no backend endpoint → empty array fallback,
 *     ChatHighlights renders empty state.
 *
 * URL state SSOT (contract §6, no useState mirror):
 *   ?diary=all|score|event|chat|photo  — DiaryFilter (default 'all')
 *   ?theme=light|dark                  — ShareCardTheme preview (default 'light')
 *   ?fixture=default|tied|abandoned|solo|empty-achievements|empty-photos
 *                                      — visual fixture override gated by
 *                                        STATE_OVERRIDE_ENABLED (foundation
 *                                        helper). Allows E2E specs to render
 *                                        deterministic states without backend.
 *
 * Gate A (ICU plural): all label strings resolved here via `useTranslation().t()`
 *   with `valuesOrDefault`. Pure components receive pre-resolved strings.
 *
 * Gate C (MeepleCard fit): all 11 feature components DIVERGE per Task 2 audit.
 *
 * Pattern blueprint: Wave D.2 SessionLiveView orchestrator (read+write+lazy).
 * D.3 differs in: 4-hook compose (vs 1-hook D.2 Foundation), brownfield FORK
 * status redirect, ?fixture= override (vs ?state= D.2), achievements stub.
 *
 * @see docs/frontend/contracts/sessions-id-summary-hooks.md §1 §2 §3 §6
 */

'use client';

import { Suspense, useCallback, useEffect, useMemo, useState, type ReactElement } from 'react';

import { useParams, usePathname, useRouter, useSearchParams } from 'next/navigation';

import {
  SummaryFlavorRenderer,
  hasSummaryFlavor,
} from '@/components/features/session-live/SummaryFlavorRenderer';
import {
  AchievementsCarousel,
  ChatHighlights,
  ConnectionBar,
  PhotosGallery,
  PlayAgainCta,
  ScoringBreakdownTable,
  SessionDiaryTimeline,
  SessionKpiGrid,
  SessionShareCard,
  SessionSummaryHero,
  type AchievementsCarouselLabels,
  type ChatHighlightsLabels,
  type ConnectionBarLabels,
  type ConnectionBarPip,
  type DiaryFilter,
  type DiaryTurnGroup,
  type KpiEntry,
  type PhotosGalleryLabels,
  type PlayAgainCtaLabels,
  type ScoringBreakdownTableLabels,
  type SessionDiaryTimelineLabels,
  type SessionShareCardLabels,
  type SessionSummaryHeroLabels,
  type ShareCardTheme,
  type ShareChannel,
} from '@/components/features/session-summary';
import { useLiveSession } from '@/hooks/queries/useLiveSession';
import { useSessionVisionSnapshots } from '@/hooks/queries/useSessionSnapshots';
import { useTranslation } from '@/hooks/useTranslation';
import type { GameSessionDto } from '@/lib/api/schemas/games.schemas';
import type { LiveSessionDto } from '@/lib/api/schemas/live-sessions.schemas';
import type { ParticipantDto, SessionDetailsDto } from '@/lib/api/schemas/session-tracking.schemas';
import type { DiaryEntryDto } from '@/lib/api/session-flow/types';
import { shouldShowConfetti } from '@/lib/sessions-summary/confetti-trigger';
import { deriveSessionSummaryState, type SessionSummaryFSMCell } from '@/lib/sessions-summary/fsm';
import type { AchievementDto } from '@/lib/sessions-summary/schemas';
import {
  computeRankedParticipants,
  type RankedParticipant,
} from '@/lib/sessions-summary/tie-groups';
import {
  parseStateOverride,
  sessionSummaryFixtures,
  STATE_OVERRIDE_ENABLED,
  type SessionSummaryFixture,
} from '@/lib/sessions-summary/visual-test-fixture';

// ─── SessionId resolution (contract §3.2) ──────────────────────────────────

function resolveSessionId(rawId: string | string[] | undefined): string | null {
  if (typeof rawId !== 'string' || rawId.length === 0) return null;
  return rawId;
}

// ─── DiaryFilter parsing ───────────────────────────────────────────────────

const DIARY_FILTER_VALUES = ['all', 'score', 'event', 'chat', 'photo'] as const;

function parseDiaryFilter(raw: string | null): DiaryFilter {
  if (raw && (DIARY_FILTER_VALUES as readonly string[]).includes(raw)) {
    return raw as DiaryFilter;
  }
  return 'all';
}

function parseShareTheme(raw: string | null): ShareCardTheme {
  return raw === 'dark' ? 'dark' : 'light';
}

const NO_ACHIEVEMENTS: readonly AchievementDto[] = [];
const NO_DIARY: readonly DiaryEntryDto[] = [];
const NO_DIARY_QUERY = { isPending: false, isError: false, error: null, data: NO_DIARY };

// ─── LiveSessionDto → SessionDetailsDto-like adapter (#4156) ───────────────

/**
 * L'id della rotta è quello di una `LiveGameSession` (decisione D4 di #4156): il riepilogo la
 * legge, e i suoi componenti parlano la forma `SessionDetailsDto`, che si costruisce qui. Totali
 * e punteggi per round sono quelli veri, da `RoundScores` (ADR-089 §1). Prima il riepilogo
 * leggeva la `GameSession` con l'id di una live, riceveva 404, e anche quando la trovava usava
 * un punteggio segnaposto (1 al vincitore, 0 agli altri).
 */
function adaptLiveSessionToDetails(live: LiveSessionDto): SessionDetailsDto {
  const participants: ParticipantDto[] = playersOf(live).map((p, idx) => ({
    id: p.id,
    userId: p.userId,
    displayName: p.displayName,
    isOwner: p.role === 'Host',
    joinOrder: idx + 1,
    finalRank: null, // computed downstream by computeRankedParticipants
    totalScore: p.totalScore,
  }));

  return {
    id: live.id,
    userId: live.createdByUserId,
    gameId: live.gameId ?? '',
    sessionCode: live.sessionCode,
    sessionType: 'Standard',
    status: live.status,
    sessionDate: live.startedAt ?? live.createdAt,
    location: null,
    finalizedAt: live.completedAt,
    participants,
    scores: live.roundScores.map(s => ({
      id: `${s.playerId}-${s.round}-${s.dimension}`,
      participantId: s.playerId,
      roundNumber: s.round,
      category: s.dimension,
      scoreValue: s.value,
      timestamp: s.recordedAt,
    })),
    notes: [], // le note personali della pagina /notes restano nel browser
  };
}

/** Chi gioca: gli spettatori non segnano punti e non salgono sul podio. */
function playersOf(live: LiveSessionDto): LiveSessionDto['players'] {
  return live.players.filter(p => p.role !== 'Spectator');
}

/**
 * L'aspetto per gioco del riepilogo (#3022, Catan) riceve una `GameSessionDto`. La si costruisce
 * dalla live, con il punteggio «Points» ricavato dai totali di `RoundScores`: il polimorfico di
 * SessionTracking esce dal percorso live (ADR-089), quindi non c'è un altro punteggio da leggere.
 */
function adaptLiveSessionToFlavorSession(live: LiveSessionDto): GameSessionDto {
  const players = playersOf(live);
  const top = players.reduce((max, p) => Math.max(max, p.totalScore), Number.NEGATIVE_INFINITY);
  const leaders = players.filter(p => p.totalScore === top);
  const durationMinutes =
    live.startedAt && live.completedAt
      ? Math.max(0, Math.round((Date.parse(live.completedAt) - Date.parse(live.startedAt)) / 60000))
      : 0;

  return {
    id: live.id,
    gameId: live.gameId ?? '',
    status: live.status,
    startedAt: live.startedAt ?? live.createdAt,
    completedAt: live.completedAt,
    playerCount: Math.max(players.length, 1),
    players: players.map((p, idx) => ({
      id: p.id,
      playerName: p.displayName,
      playerOrder: idx + 1,
      color: p.color,
    })),
    // Un vincitore solo se il primo posto non è condiviso: un pareggio non ha un nome.
    winnerName: leaders.length === 1 ? leaders[0].displayName : null,
    notes: live.notes,
    durationMinutes,
    scoringType: 'Points',
    scoreData: JSON.stringify({
      scores: players.map(p => ({ playerId: p.id, points: p.totalScore })),
    }),
    gameSlug: live.gameSlug,
    gameName: live.gameName,
    scorePlayers: players.map(p => ({ id: p.id, displayName: p.displayName, color: p.color })),
  };
}

// ─── Diary turn grouping (contract §5.7) ───────────────────────────────────

interface DiaryEntryLike {
  readonly id: string;
  readonly eventType: string;
  readonly timestamp: string;
  readonly payload: string | null;
}

function deriveTurnFromDiaryEntry(entry: DiaryEntryLike, fallback: number): number {
  // Try to extract turn from payload JSON shape `{ turn: number, ... }` or
  // `{ turnNumber: number, ... }`. Fallback to chronological position.
  if (!entry.payload) return fallback;
  try {
    const parsed = JSON.parse(entry.payload);
    if (typeof parsed?.turn === 'number') return parsed.turn;
    if (typeof parsed?.turnNumber === 'number') return parsed.turnNumber;
  } catch {
    // not JSON — fall through to fallback
  }
  return fallback;
}

function groupDiaryByTurn(diary: readonly DiaryEntryLike[]): readonly DiaryTurnGroup[] {
  if (diary.length === 0) return [];
  const groups = new Map<number, DiaryEntryLike[]>();
  diary.forEach((entry, idx) => {
    const turn = deriveTurnFromDiaryEntry(entry, Math.floor(idx / 4) + 1);
    const list = groups.get(turn);
    if (list) list.push(entry);
    else groups.set(turn, [entry]);
  });
  return Array.from(groups.entries())
    .sort(([a], [b]) => a - b)
    .map(([turn, events]) => ({ turn, events: events as never }));
}

// ─── Skeleton + error + not-found shells ───────────────────────────────────

function LoadingShell({ ariaLabel }: { ariaLabel: string }): ReactElement {
  return (
    <div
      data-slot="session-summary-loading"
      role="status"
      aria-label={ariaLabel}
      aria-live="polite"
      className="mx-auto flex max-w-[1200px] flex-col gap-4 px-4 py-6 animate-pulse"
    >
      <div className="h-48 rounded-lg bg-muted/40" />
      <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
        <div className="h-20 rounded-md bg-muted/40" />
        <div className="h-20 rounded-md bg-muted/40" />
        <div className="h-20 rounded-md bg-muted/40" />
        <div className="h-20 rounded-md bg-muted/40" />
      </div>
      <div className="h-64 rounded-lg bg-muted/40" />
      <div className="h-48 rounded-lg bg-muted/40" />
    </div>
  );
}

function ErrorShell({
  title,
  description,
  ctaRetry,
  onRetry,
}: {
  title: string;
  description: string;
  ctaRetry: string;
  onRetry: () => void;
}): ReactElement {
  return (
    <div
      data-slot="session-summary-error"
      role="alert"
      className="mx-auto flex max-w-[1200px] flex-col items-center justify-center gap-4 px-4 py-12 text-center"
    >
      <p className="text-lg font-semibold text-foreground">{title}</p>
      <p className="text-sm text-muted-foreground">{description}</p>
      <button
        type="button"
        onClick={onRetry}
        data-slot="session-summary-error-retry"
        className="rounded-lg bg-[hsl(240,60%,38%)] px-6 py-2 text-sm font-semibold text-white hover:bg-[hsl(240,60%,32%)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
      >
        {ctaRetry}
      </button>
    </div>
  );
}

function NotFoundShell({
  title,
  description,
  ctaBack,
  onBack,
  ctaNewSession,
  onNewSession,
}: {
  title: string;
  description: string;
  ctaBack: string;
  onBack: () => void;
  /** Issue #2088: optional primary CTA to start a new session. */
  ctaNewSession?: string;
  onNewSession?: () => void;
}): ReactElement {
  return (
    <div
      data-slot="session-summary-not-found"
      role="status"
      className="mx-auto flex max-w-[1200px] flex-col items-center justify-center gap-4 px-4 py-12 text-center"
    >
      <p className="text-lg font-semibold text-foreground">{title}</p>
      {description ? <p className="text-sm text-muted-foreground">{description}</p> : null}
      <div className="flex flex-wrap items-center justify-center gap-3">
        {ctaNewSession && onNewSession ? (
          <button
            type="button"
            onClick={onNewSession}
            data-slot="session-summary-not-found-new-cta"
            className="rounded-lg bg-[hsl(25,95%,38%)] px-6 py-2 text-sm font-semibold text-white hover:opacity-90 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
          >
            {ctaNewSession}
          </button>
        ) : null}
        <button
          type="button"
          onClick={onBack}
          data-slot="session-summary-not-found-cta"
          className="rounded-lg border border-border bg-background px-6 py-2 text-sm font-semibold text-foreground hover:bg-muted/40 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
        >
          {ctaBack}
        </button>
      </div>
    </div>
  );
}

function NotCompletedShell({
  title,
  description,
  ctaLive,
  onGoLive,
}: {
  title: string;
  description: string;
  ctaLive: string;
  onGoLive: () => void;
}): ReactElement {
  return (
    <div
      data-slot="session-summary-not-completed"
      role="status"
      className="mx-auto flex max-w-[1200px] flex-col items-center justify-center gap-4 px-4 py-12 text-center"
    >
      <p className="text-lg font-semibold text-foreground">{title}</p>
      <p className="text-sm text-muted-foreground">{description}</p>
      <button
        type="button"
        onClick={onGoLive}
        data-slot="session-summary-go-live"
        className="rounded-lg bg-[hsl(240,60%,38%)] px-6 py-2 text-sm font-semibold text-white hover:bg-[hsl(240,60%,32%)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
      >
        {ctaLive}
      </button>
    </div>
  );
}

// ─── Component ─────────────────────────────────────────────────────────────

export interface SessionSummaryViewProps {
  /** Resolved session id from page.tsx server component. */
  readonly sessionId: string;
}

export function SessionSummaryView({
  sessionId: rawSessionId,
}: SessionSummaryViewProps): ReactElement {
  const { t, formatDate } = useTranslation();
  const params = useParams<{ id: string }>();
  const searchParams = useSearchParams();
  const router = useRouter();
  const pathname = usePathname();

  // Prefer explicit prop (server-resolved), fall back to client-side useParams.
  const sessionId = useMemo<string | null>(() => {
    if (rawSessionId.length > 0) return rawSessionId;
    return resolveSessionId(params?.id);
  }, [rawSessionId, params?.id]);

  // ── URL state SSOT ────────────────────────────────────────────────────
  const diaryFilter = parseDiaryFilter(searchParams.get('diary'));
  const shareTheme = parseShareTheme(searchParams.get('theme'));
  const fixtureOverride = STATE_OVERRIDE_ENABLED
    ? parseStateOverride(searchParams.get('fixture'))
    : null;

  // ── Visual fixture path: short-circuit hooks when override active ─────
  const fixture: SessionSummaryFixture | null = useMemo(() => {
    if (fixtureOverride === null) return null;
    return sessionSummaryFixtures[fixtureOverride];
  }, [fixtureOverride]);

  // ── Real data hooks (disabled when fixture active) ────────────────────
  const sessionQuery = useLiveSession(sessionId ?? '', fixture === null && sessionId !== null);
  const snapshotsQuery = useSessionVisionSnapshots(
    sessionId ?? '',
    fixture === null && sessionId !== null && sessionQuery.isSuccess && sessionQuery.data != null
  );

  // ── Achievements ──────────────────────────────────────────────────────
  // Non c'è un backend per gli achievement: sui dati reali il carosello è vuoto. Fino a #4156
  // questo ramo mostrava gli achievement della fixture di default, cioè dati di prova presentati
  // come se la partita li avesse sbloccati.
  const achievements: readonly AchievementDto[] = useMemo(() => {
    if (fixture !== null) return fixture.achievements;
    return NO_ACHIEVEMENTS;
  }, [fixture]);

  // ── Adapt LiveSessionDto → SessionDetailsDto-shape ────────────────────
  const adaptedSession: SessionDetailsDto | null = useMemo(() => {
    if (fixture !== null) return fixture.session;
    if (!sessionQuery.data) return null;
    return adaptLiveSessionToDetails(sessionQuery.data);
  }, [fixture, sessionQuery.data]);

  const flavorSession: GameSessionDto | null = useMemo(
    () => (sessionQuery.data ? adaptLiveSessionToFlavorSession(sessionQuery.data) : null),
    [sessionQuery.data]
  );

  // ── FSM derivation (delegate to foundation) ───────────────────────────
  // Build QueryLike inputs from our hook results / fixture.
  const fsmCell: SessionSummaryFSMCell = useMemo<SessionSummaryFSMCell>(() => {
    if (fixture !== null) {
      // Fixture path always produces a default/partial cell — never loading/error.
      const status = fixture.session.status;
      if (!['Completed', 'Abandoned'].includes(status)) {
        return { kind: 'not-completed', status, sessionId: fixture.session.id };
      }
      const missing: ('diary' | 'snapshots' | 'achievements' | 'chat')[] = [];
      if (fixture.diary.length === 0) missing.push('diary');
      if (fixture.snapshots.length === 0) missing.push('snapshots');
      if (fixture.achievements.length === 0) missing.push('achievements');
      if (missing.length > 0) {
        return {
          kind: 'partial',
          session: fixture.session,
          diary: fixture.diary,
          snapshots: fixture.snapshots,
          achievements: fixture.achievements,
          missing,
        };
      }
      return {
        kind: 'default',
        session: fixture.session,
        diary: fixture.diary,
        snapshots: fixture.snapshots,
        achievements: fixture.achievements,
      };
    }
    if (sessionId === null) return { kind: 'not-found' };
    return deriveSessionSummaryState({
      sessionQuery: {
        isPending: sessionQuery.isLoading,
        isError: sessionQuery.isError,
        error: sessionQuery.error,
        data: adaptedSession,
      },
      // #4156: il diario di questa vista è quello di SessionTracking, che con l'id di una live
      // non esiste. Il diario della live è fatto di note di testo, che la timeline non sa ancora
      // mostrare: finché non lo sa, il diario resta vuoto invece di interrogare l'aggregato
      // sbagliato.
      diaryQuery: NO_DIARY_QUERY,
      snapshotsQuery: {
        isPending: sessionQuery.isSuccess && snapshotsQuery.isLoading,
        isError: snapshotsQuery.isError,
        error: snapshotsQuery.error,
        data: snapshotsQuery.data ?? [],
      },
      achievementsQuery: {
        // Stub — never pending/errored; data always available.
        isPending: false,
        isError: false,
        error: null,
        data: achievements,
      },
      sessionId,
    });
  }, [
    fixture,
    sessionId,
    sessionQuery.isLoading,
    sessionQuery.isError,
    sessionQuery.error,
    sessionQuery.isSuccess,
    adaptedSession,
    snapshotsQuery.isLoading,
    snapshotsQuery.isError,
    snapshotsQuery.error,
    snapshotsQuery.data,
    achievements,
  ]);

  // ── Side effect: redirect to /live when status is in-progress/setup ──
  // Per Q1 resolution (contract §1.0): InProgress/Paused/Setup → live page.
  useEffect(() => {
    if (fsmCell.kind !== 'not-completed') return;
    if (sessionId === null) return;
    const inProgressStatuses = new Set(['Created', 'InProgress', 'Paused', 'Setup']);
    if (inProgressStatuses.has(fsmCell.status)) {
      router.replace(`/sessions/${sessionId}/live`);
    }
  }, [fsmCell, sessionId, router]);

  // ── Navigation handlers ──────────────────────────────────────────────
  const buildQuery = useCallback(
    (overrides: Partial<Record<string, string | null>>): string => {
      const next = new URLSearchParams(searchParams.toString());
      Object.entries(overrides).forEach(([k, v]) => {
        if (v == null || v === '') next.delete(k);
        else next.set(k, v);
      });
      const qs = next.toString();
      return qs ? `?${qs}` : '';
    },
    [searchParams]
  );

  const handleDiaryFilterChange = useCallback(
    (next: DiaryFilter) => {
      const val = next === 'all' ? null : next;
      router.replace(`${pathname}${buildQuery({ diary: val })}`, { scroll: false });
    },
    [router, pathname, buildQuery]
  );

  const handleShareThemeChange = useCallback(
    (next: ShareCardTheme) => {
      const val = next === 'light' ? null : next;
      router.replace(`${pathname}${buildQuery({ theme: val })}`, { scroll: false });
    },
    [router, pathname, buildQuery]
  );

  const handleRetry = useCallback(() => {
    void sessionQuery.refetch?.();
  }, [sessionQuery]);

  const handleBack = useCallback(() => {
    router.push('/sessions');
  }, [router]);

  const handleGoLive = useCallback(() => {
    if (sessionId === null) return;
    router.replace(`/sessions/${sessionId}/live`);
  }, [router, sessionId]);

  // ── Diary turn expand/collapse (lightweight local UI state) ──────────
  // Mockup: by default first turn expanded. Subsequent UI state is
  // ephemeral — not URL-promoted (would clutter the address bar).
  // Computed initial set per session via memo for stable identity.
  const diaryGroups: readonly DiaryTurnGroup[] = useMemo(() => {
    if (fsmCell.kind !== 'default' && fsmCell.kind !== 'partial') return [];
    return groupDiaryByTurn(fsmCell.diary);
  }, [fsmCell]);

  const [expandedTurns, toggleTurn] = useExpandedTurns(diaryGroups);

  // ── Confetti (first-load only) ────────────────────────────────────────
  const showConfetti = useMemo<boolean>(() => {
    if (fsmCell.kind !== 'default' && fsmCell.kind !== 'partial') return false;
    if (fsmCell.session.status !== 'Completed') return false;
    return shouldShowConfetti(fsmCell.session.id);
  }, [fsmCell]);

  // ── Share callbacks ───────────────────────────────────────────────────
  const handleShare = useCallback(
    (channel: ShareChannel) => {
      // Out-of-scope D.3 implementation depth. Wire to clipboard / window.open
      // when sharing copy is finalised (contract §10).
      if (typeof window === 'undefined') return;
      const summary =
        fsmCell.kind === 'default' || fsmCell.kind === 'partial' ? fsmCell.session : null;
      if (summary === null) return;
      const url = `${window.location.origin}/sessions/${summary.id}`;
      if (channel === 'copy') {
        void navigator.clipboard?.writeText(url);
      } else if (channel === 'twitter') {
        const text = encodeURIComponent(`Risultati partita: ${url}`);
        window.open(`https://twitter.com/intent/tweet?text=${text}`, '_blank', 'noopener');
      } else if (channel === 'whatsapp') {
        const text = encodeURIComponent(`Risultati partita: ${url}`);
        window.open(`https://wa.me/?text=${text}`, '_blank', 'noopener');
      } else if (channel === 'instagram') {
        // Web Share API fallback — Instagram has no direct intent URL.
        void navigator.clipboard?.writeText(url);
      }
    },
    [fsmCell]
  );

  // ── i18n labels (Gate A: ICU plural resolved here) ────────────────────
  // Achievements label resolvers (resolves i18n keys → strings for the
  // pure component). Schema defines `titleKey`/`descriptionKey` as i18n keys.
  const achievementTitleFor = useCallback(
    (achievementId: string): string => {
      const a = achievements.find(x => x.id === achievementId);
      if (!a) return '';
      return t(a.titleKey, a.titleKey);
    },
    [achievements, t]
  );

  const achievementDescriptionFor = useCallback(
    (achievementId: string): string => {
      const a = achievements.find(x => x.id === achievementId);
      if (!a) return '';
      return t(a.descriptionKey, a.descriptionKey);
    },
    [achievements, t]
  );

  const achievementUnlockedAtText = useCallback(
    (achievementId: string): string | null => {
      const a = achievements.find(x => x.id === achievementId);
      if (!a || a.unlockedAt === null) return null;
      return t('pages.sessionSummary.achievements.unlockedAt', {
        date: formatDate(a.unlockedAt, { dateStyle: 'medium' }),
      });
    },
    [achievements, t, formatDate]
  );

  const achievementsCarouselLabels: AchievementsCarouselLabels = useMemo(() => {
    const total = achievements.length;
    const unlocked = achievements.filter(a => a.unlockedAt !== null).length;
    return {
      title: t('pages.sessionSummary.achievements.title'),
      unlockedCount: `${unlocked} / ${total}`,
      emptyTitle: t('pages.sessionSummary.achievements.empty'),
      emptyDescription: t('pages.sessionSummary.achievements.locked'),
      lockedAriaPrefix: `${t('pages.sessionSummary.achievements.locked')}: `,
      unlockedAtText: achievementUnlockedAtText,
      titleFor: achievementTitleFor,
      descriptionFor: achievementDescriptionFor,
    };
  }, [achievements, t, achievementUnlockedAtText, achievementTitleFor, achievementDescriptionFor]);

  const heroLabels: SessionSummaryHeroLabels = useMemo(() => {
    const tiedNames =
      fsmCell.kind === 'default' || fsmCell.kind === 'partial'
        ? (() => {
            const top = computeRankedParticipants(fsmCell.session.participants).filter(
              p => p.rank === 1
            );
            return top.length >= 2 ? top.map(p => p.displayName).join(', ') : '';
          })()
        : '';
    const tiedCount = tiedNames.split(', ').filter(Boolean).length;
    return {
      title: t('pages.sessionSummary.hero.title'),
      tiedBanner:
        tiedCount >= 2
          ? t('pages.sessionSummary.hero.tiedBanner', { count: tiedCount, names: tiedNames })
          : undefined,
      confettiAriaLabel: t('pages.sessionSummary.hero.confettiAriaLabel'),
      confettiSkippedLabel: t('pages.sessionSummary.hero.confettiSkippedLabel'),
      podiumPlaceAriaLabel: (place, name, score) =>
        t('pages.sessionSummary.hero.podiumLabel') + `: ${place}° ${name} (${score})`,
    };
  }, [fsmCell, t]);

  const connectionBarLabels: ConnectionBarLabels = useMemo(
    () => ({
      title: t('pages.sessionSummary.connectionBar.title'),
      emptyEvent: t('pages.sessionSummary.connectionBar.emptyEvent'),
    }),
    [t]
  );

  const scoringLabels: ScoringBreakdownTableLabels = useMemo(
    () => ({
      title: t('pages.sessionSummary.scoring.title'),
      headerName: t('pages.sessionSummary.scoring.headerName'),
      headerScore: t('pages.sessionSummary.scoring.headerScore'),
      headerRank: t('pages.sessionSummary.scoring.headerRank'),
      tied: t('pages.sessionSummary.scoring.tied'),
    }),
    [t]
  );

  const diaryLabels: SessionDiaryTimelineLabels = useMemo(
    () => ({
      title: t('pages.sessionSummary.diary.title'),
      filterAll: t('pages.sessionSummary.diary.filterAll'),
      filterScore: t('pages.sessionSummary.diary.filterScore'),
      filterEvent: t('pages.sessionSummary.diary.filterEvent'),
      filterChat: t('pages.sessionSummary.diary.filterChat'),
      filterPhoto: t('pages.sessionSummary.diary.filterPhoto'),
      empty: t('pages.sessionSummary.diary.empty'),
      toggleAriaLabel: (turn, isExpanded) =>
        isExpanded
          ? t('pages.sessionSummary.diary.collapse', { turn })
          : t('pages.sessionSummary.diary.expand', { turn }),
      turnLabel: turn => `Turno ${turn}`,
      turnEventsCount: count => `${count} eventi`,
    }),
    [t]
  );

  const photosLabels: PhotosGalleryLabels = useMemo(
    () => ({
      title: t('pages.sessionSummary.photos.title'),
      emptyTitle: t('pages.sessionSummary.photos.empty'),
      emptyDescription: t('pages.sessionSummary.photos.empty'),
      photoAltFallback: t('pages.sessionSummary.photos.viewLarger'),
    }),
    [t]
  );

  const chatLabels: ChatHighlightsLabels = useMemo(
    () => ({
      title: t('pages.sessionSummary.chatHighlights.title'),
      emptyTitle: t('pages.sessionSummary.chatHighlights.empty'),
      emptyDescription: t('pages.sessionSummary.chatHighlights.empty'),
    }),
    [t]
  );

  const shareLabels: SessionShareCardLabels = useMemo(() => {
    const headline =
      fsmCell.kind === 'default' || fsmCell.kind === 'partial'
        ? (() => {
            const ranked = computeRankedParticipants(fsmCell.session.participants);
            const winner = ranked.find(p => p.rank === 1);
            return winner ? winner.displayName : '';
          })()
        : '';
    return {
      title: t('pages.sessionSummary.share.title'),
      previewLight: t('pages.sessionSummary.share.previewLight'),
      previewDark: t('pages.sessionSummary.share.previewDark'),
      themeToggleAriaLabel: t('pages.sessionSummary.share.title'),
      shareTwitter: t('pages.sessionSummary.share.shareTwitter'),
      shareInstagram: t('pages.sessionSummary.share.shareInstagram'),
      shareWhatsApp: t('pages.sessionSummary.share.shareWhatsApp'),
      copyLink: t('pages.sessionSummary.share.copyLink'),
      downloadPng: t('pages.sessionSummary.share.downloadPng'),
      downloadPngDisabled: t('pages.sessionSummary.share.downloadPngDisabled'),
      previewHeadline: headline,
      previewMeta:
        fsmCell.kind === 'default' || fsmCell.kind === 'partial'
          ? `${fsmCell.session.participants.length} giocatori`
          : '',
    };
  }, [fsmCell, t]);

  const playAgainLabels: PlayAgainCtaLabels = useMemo(
    () => ({
      title: t('pages.sessionSummary.playAgain.title'),
      description: t('pages.sessionSummary.playAgain.description'),
      cta: t('pages.sessionSummary.playAgain.cta'),
    }),
    [t]
  );

  // ── KPI tile builders ─────────────────────────────────────────────────
  const kpis: readonly KpiEntry[] = useMemo(() => {
    if (fsmCell.kind !== 'default' && fsmCell.kind !== 'partial') return [];
    const session = fsmCell.session;
    const playerCount = session.participants.length;
    const totalScore = session.participants.reduce((sum, p) => sum + p.totalScore, 0);
    return [
      {
        key: 'players',
        label: t('pages.sessionSummary.kpi.players'),
        value: String(playerCount),
        emoji: '👥',
        entity: 'session',
      },
      {
        key: 'turns',
        label: t('pages.sessionSummary.kpi.turns'),
        value: String(diaryGroups.length || '—'),
        emoji: '🔄',
        entity: 'toolkit',
      },
      {
        key: 'totalScore',
        label: t('pages.sessionSummary.kpi.totalScore'),
        value: String(totalScore),
        emoji: '🏆',
        entity: 'game',
      },
      {
        key: 'duration',
        label: t('pages.sessionSummary.kpi.duration'),
        value: '—', // v1 carryover: no durationMinutes on adapted session
        emoji: '⏱️',
        entity: 'agent',
      },
    ];
  }, [fsmCell, diaryGroups.length, t]);

  // ── Connection bar pips ───────────────────────────────────────────────
  const connectionPips: readonly ConnectionBarPip[] = useMemo(() => {
    if (fsmCell.kind !== 'default' && fsmCell.kind !== 'partial') return [];
    return [
      { entity: 'game', emoji: '🎲', label: 'Gioco', count: 1 },
      {
        entity: 'player',
        emoji: '👥',
        label: 'Giocatori',
        count: fsmCell.session.participants.length,
      },
      { entity: 'event', emoji: '📜', label: 'Eventi', count: fsmCell.diary.length },
      { entity: 'agent', emoji: '🤖', label: 'Agente', count: achievements.length },
      { entity: 'chat', emoji: '💬', label: 'Chat', count: 0 },
      { entity: 'session', emoji: '🎯', label: 'Snap', count: fsmCell.snapshots.length },
    ];
  }, [fsmCell, achievements.length]);

  // ── Ranked participants (memoised for hero + scoring + share) ────────
  const rankedParticipants: readonly RankedParticipant[] = useMemo(() => {
    if (fsmCell.kind !== 'default' && fsmCell.kind !== 'partial') return [];
    return computeRankedParticipants(fsmCell.session.participants);
  }, [fsmCell]);

  // ── Render ────────────────────────────────────────────────────────────

  if (fsmCell.kind === 'loading') {
    return (
      <div data-slot="session-summary-view" data-ui-state="loading">
        <LoadingShell ariaLabel={t('pages.sessionSummary.hero.title')} />
      </div>
    );
  }

  if (fsmCell.kind === 'error') {
    return (
      <div data-slot="session-summary-view" data-ui-state="error">
        <ErrorShell
          title={t('common.errorTitle', 'Errore')}
          description={fsmCell.error.message}
          ctaRetry={t('common.retry', 'Riprova')}
          onRetry={handleRetry}
        />
      </div>
    );
  }

  if (fsmCell.kind === 'not-found') {
    return (
      <div data-slot="session-summary-view" data-ui-state="not-found">
        <NotFoundShell
          title={t('pages.sessionSummary.states.notFound.title')}
          description={t('pages.sessionSummary.states.notFound.description')}
          ctaBack={t('pages.sessionSummary.states.notFound.backToSessions')}
          onBack={handleBack}
          ctaNewSession={t('pages.sessionSummary.states.notFound.newSession')}
          onNewSession={() => router.push('/sessions/new')}
        />
      </div>
    );
  }

  if (fsmCell.kind === 'not-completed') {
    return (
      <div data-slot="session-summary-view" data-ui-state="not-completed">
        <NotCompletedShell
          title={t('pages.sessionSummary.states.notCompleted.title')}
          description={t('pages.sessionSummary.states.notCompleted.description')}
          ctaLive={t('pages.sessionSummary.states.notCompleted.continueLive')}
          onGoLive={handleGoLive}
        />
      </div>
    );
  }

  // default + partial: full render
  const session = fsmCell.session;

  return (
    <div
      data-slot="session-summary-view"
      data-ui-state={fsmCell.kind}
      className="mx-auto max-w-[1200px] pb-12"
    >
      {/* #3022: per-game summary flavor (Catan…), completed sessions only. */}
      {sessionQuery.data != null &&
        flavorSession != null &&
        sessionQuery.data.status === 'Completed' &&
        hasSummaryFlavor(sessionQuery.data.gameSlug) && (
          <div className="px-4 pt-4 sm:px-6">
            <SummaryFlavorRenderer gameSlug={sessionQuery.data.gameSlug} session={flavorSession} />
          </div>
        )}

      <SessionSummaryHero
        rankedParticipants={rankedParticipants}
        showConfetti={showConfetti}
        labels={heroLabels}
      />

      <div className="px-4 sm:px-6">
        <SessionKpiGrid
          kpis={kpis}
          gridAriaLabel={t('pages.sessionSummary.kpi.totalScore')}
          className="mt-4"
        />

        <ConnectionBar
          pips={connectionPips}
          labels={connectionBarLabels}
          className="mt-4 rounded-md"
        />

        <div className="mt-6">
          <ScoringBreakdownTable rankedParticipants={rankedParticipants} labels={scoringLabels} />
        </div>

        <div className="mt-6">
          <AchievementsCarousel
            achievements={fsmCell.achievements}
            labels={achievementsCarouselLabels}
          />
        </div>

        <div className="mt-6">
          <SessionDiaryTimeline
            turns={diaryGroups}
            activeFilter={diaryFilter}
            onFilterChange={handleDiaryFilterChange}
            expandedTurns={expandedTurns}
            onToggleTurn={toggleTurn}
            labels={diaryLabels}
          />
        </div>

        <div className="mt-6">
          <PhotosGallery snapshots={fsmCell.snapshots} labels={photosLabels} />
        </div>

        <div className="mt-6">
          <ChatHighlights highlights={[]} labels={chatLabels} />
        </div>

        <div className="mt-6">
          <SessionShareCard
            podium={rankedParticipants.slice(0, 3)}
            theme={shareTheme}
            onThemeChange={handleShareThemeChange}
            onShare={handleShare}
            labels={shareLabels}
          />
        </div>

        <div className="mt-6">
          <PlayAgainCta sessionId={session.id} labels={playAgainLabels} />
        </div>
      </div>
    </div>
  );
}

// ─── useExpandedTurns hook ────────────────────────────────────────────────
// Lightweight local state for the diary turn collapsibles. Initialises with
// the first turn expanded (mockup default). Lives in a tiny custom hook so
// the orchestrator stays under the rule of one cohesive function.

function useExpandedTurns(
  diaryGroups: readonly DiaryTurnGroup[]
): readonly [ReadonlySet<number>, (turn: number) => void] {
  const initial = useMemo(() => {
    if (diaryGroups.length === 0) return new Set<number>();
    return new Set<number>([diaryGroups[0].turn]);
  }, [diaryGroups]);

  const [expanded, setExpanded] = useState<ReadonlySet<number>>(initial);

  const toggle = useCallback((turn: number) => {
    setExpanded(prev => {
      const next = new Set(prev);
      if (next.has(turn)) next.delete(turn);
      else next.add(turn);
      return next;
    });
  }, []);

  return [expanded, toggle];
}

/**
 * Wrapper for Suspense + page server component. Exported separately so
 * `page.tsx` can render it without leaking client/server boundary concerns.
 */
export function SessionSummaryViewSuspense({ sessionId }: { sessionId: string }): ReactElement {
  return (
    <Suspense fallback={<LoadingShell ariaLabel="Caricamento riepilogo" />}>
      <SessionSummaryView sessionId={sessionId} />
    </Suspense>
  );
}
