/**
 * PlayRecordDetailView — Task 2 (Issue #1488 / Epic #1475 Phase D).
 *
 * Full detail view for a single play record. Composes:
 *   - PlayRecordHeroPodium (variant driven by derivePerspective)
 *   - ConnectionBar (entity-tinted chips: game / players / chat / date)
 *   - KpiGrid (duration, top score, average, spread)
 *   - Classifica (ranked player list with progress bars)
 *   - ScoreBreakdown (accordion for multi-dim scoring, EC-10)
 *   - Notes section (if present)
 *   - Rematch CTA (for completed records)
 *
 * Perspective: derived from PlayRecordDto × currentUserId via derivePerspective.
 *
 * AC-2.1: consumes usePlayRecord(id) + derivePerspective
 * AC-2.2: HeroPodium variant matrix (won/tied/cooperative/inprogress/planned/spectator)
 * AC-2.3: ConnectionBar entity-tinted chips
 * AC-2.4: KpiGrid 4 KPI
 * AC-2.5: Classifica ranked + progress bar
 * AC-2.6: ScoreBreakdown accordion for multi-dim (EC-10)
 * AC-2.7: EC-1 cooperative — no OutcomeBadge, classifica neutrale
 * AC-2.8: EC-2 freeform (gameId=null) — emoji fallback, no game link
 * AC-2.9: EC-4/5 spectator — no current-user highlight
 * AC-2.10: EC-6/7 InProgress/Planned — omit duration, relative future date
 * AC-2.12: 404 → redirect to /play-records
 *
 * @see plan `docs/superpowers/plans/2026-05-29-play-records-reskin.md` Task 2
 * @see mockup `admin-mockups/design_files/sp4-play-records-detail.jsx`
 */
/* eslint-disable local/no-hardcoded-color-utility -- text-white / gradient covers intentionally use colored bg following .e-bg mockup pattern */
'use client';

import type { ReactElement } from 'react';

import Link from 'next/link';
import { useRouter } from 'next/navigation';

import { entityHsl } from '@/components/ui/data-display/meeple-card';
import { useCurrentUser } from '@/hooks/queries/useCurrentUser';
import { useTranslation } from '@/hooks/useTranslation';
import type { SessionPlayer } from '@/lib/api/schemas/play-records.schemas';
import { usePlayRecord } from '@/lib/domain-hooks/usePlayRecords';
import { derivePerspective } from '@/lib/play-records/derivePerspective';
import { formatRelativeDate } from '@/lib/play-records/formatRelativeDate';

import { Classifica, type ClassificaRow } from './detail/Classifica';
import { ConnectionBar } from './detail/ConnectionBar';
import { KpiGrid } from './detail/KpiGrid';
import { ScoreBreakdown, type ScoreBreakdownRow } from './detail/ScoreBreakdown';
import {
  PlayRecordHeroPodium,
  type RankedScore,
  type PlayRecordHeroPodiumLabels,
} from './primitives/PlayRecordHeroPodium';

// ── helpers ───────────────────────────────────────────────────────────────────

/**
 * Map perspective.kind to the hero variant accepted by PlayRecordHeroPodium.
 * spectator → use "won" or "tied" visual variant, current-user just isn't highlighted.
 */
function perspectiveToHeroVariant(
  kind: string,
  winnerCount: number
): 'won' | 'tied' | 'cooperative' | 'inprogress' | 'planned' {
  switch (kind) {
    case 'won':
      return 'won';
    case 'tie':
    case 'tied':
      return 'tied';
    case 'cooperative':
      return 'cooperative';
    case 'pending':
      return 'inprogress'; // will be overridden to 'planned' based on status below
    case 'spectator':
    case 'lost':
      // For spectator/lost, still show the competitive podium (won/tied based on winner count)
      return winnerCount > 1 ? 'tied' : 'won';
    default:
      return 'won';
  }
}

/** Format .NET TimeSpan or ISO 8601 duration string to human-readable. */
function formatDuration(raw: string | null): string | null {
  if (!raw) return null;
  // .NET TimeSpan: "02:15:00" or "1.02:15:00"
  // eslint-disable-next-line security/detect-unsafe-regex
  const match = raw.match(/^(?:(\d+)\.)?(\d+):(\d+):(\d+)$/);
  if (match) {
    const days = match[1] ? parseInt(match[1]) : 0;
    const hours = parseInt(match[2]) + days * 24;
    const minutes = parseInt(match[3]);
    const h = hours > 0 ? `${hours}h` : '';
    const m = minutes > 0 ? `${minutes}min` : '';
    const result = [h, m].filter(Boolean).join(' ');
    return result || null;
  }
  return raw;
}

/** Build RankedScore[] sorted descending by totalScore from PlayRecordDto players. */
function buildRankedScores(players: SessionPlayer[], winnerPlayerIds: string[]): RankedScore[] {
  return [...players]
    .sort((a, b) => {
      const sa = a.totalScore ?? null;
      const sb = b.totalScore ?? null;
      if (sa === null && sb === null) return 0;
      if (sa === null) return 1;
      if (sb === null) return -1;
      return sb - sa;
    })
    .map(p => ({
      playerId: p.id,
      name: p.displayName,
      score: p.totalScore ?? null,
      isWinner: winnerPlayerIds.includes(p.id),
    }));
}

/** Build ClassificaRow[] from players. */
function buildClassificaRows(players: SessionPlayer[], winnerPlayerIds: string[]): ClassificaRow[] {
  return players.map(p => ({
    playerId: p.id,
    userId: p.userId,
    name: p.displayName,
    totalScore: p.totalScore ?? null,
    isWinner: winnerPlayerIds.includes(p.id),
  }));
}

/** Build ScoreBreakdownRow[] from players. */
function buildBreakdownRows(players: SessionPlayer[]): ScoreBreakdownRow[] {
  return players.map(p => ({
    playerId: p.id,
    name: p.displayName,
    scores: p.scores.map(s => ({ dimension: s.dimension, value: s.value })),
    totalScore: p.totalScore ?? null,
  }));
}

/** Compute KPI values from players. */
function computeKpis(players: SessionPlayer[]): {
  topScore: number | null;
  avgScore: number | null;
  spread: number | null;
} {
  const scores = players
    .map(p => p.totalScore)
    .filter((s): s is number => s !== null && s !== undefined);

  if (scores.length === 0) {
    return { topScore: null, avgScore: null, spread: null };
  }

  const max = Math.max(...scores);
  const min = Math.min(...scores);
  const avg = Math.round(scores.reduce((a, b) => a + b, 0) / scores.length);

  return {
    topScore: max,
    avgScore: avg,
    spread: max - min,
  };
}

// ── Loading skeleton ──────────────────────────────────────────────────────────

function LoadingSkeleton(): ReactElement {
  return (
    <div data-testid="play-record-detail-loading" className="flex flex-col gap-4 p-4 sm:p-8">
      <div className="h-52 animate-pulse rounded-2xl bg-muted sm:h-64" />
      <div className="h-12 animate-pulse rounded-xl bg-muted" />
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
        {[0, 1, 2, 3].map(i => (
          <div key={i} className="h-20 animate-pulse rounded-lg bg-muted" />
        ))}
      </div>
      <div className="h-48 animate-pulse rounded-xl bg-muted" />
    </div>
  );
}

// ── Error / Not found ─────────────────────────────────────────────────────────

function ErrorState({ message }: { message: string }): ReactElement {
  return (
    <div
      data-testid="play-record-detail-error"
      className="mx-4 mt-4 rounded-xl bg-danger/6 border border-danger/25 px-6 py-8 text-center sm:mx-8"
      style={{
        background: 'hsl(var(--c-danger) / .06)',
        borderColor: 'hsl(var(--c-danger) / .25)',
      }}
    >
      <div className="mb-2 text-3xl" aria-hidden="true">
        ⚠
      </div>
      <h3 className="mb-1 font-display text-base font-extrabold text-foreground">
        Partita non trovata
      </h3>
      <p className="mb-4 text-sm text-muted-foreground">{message}</p>
      <Link
        href="/play-records"
        className="inline-flex items-center gap-1.5 rounded-md px-3 py-1.5 text-sm font-bold text-white"
        style={{ background: 'hsl(var(--c-danger))' }}
      >
        ← Torna alle partite
      </Link>
    </div>
  );
}

// ── Main component ────────────────────────────────────────────────────────────

export interface PlayRecordDetailViewProps {
  readonly recordId: string;
}

export function PlayRecordDetailView({ recordId }: PlayRecordDetailViewProps): ReactElement {
  const { t } = useTranslation();
  const router = useRouter();
  const { data: currentUser } = useCurrentUser();
  const { data: record, isLoading, error } = usePlayRecord(recordId);

  if (isLoading) {
    return <LoadingSkeleton />;
  }

  if (error || !record) {
    // AC-2.12: 404 redirect
    if (typeof window !== 'undefined' && error) {
      // Only redirect on actual 404, not on loading
      const is404 = error instanceof Error && error.message?.includes('404');
      if (is404) {
        router.push('/play-records');
      }
    }
    return (
      <ErrorState
        message={
          error instanceof Error
            ? error.message
            : 'Impossibile caricare il dettaglio della partita.'
        }
      />
    );
  }

  // ── Derive perspective ──────────────────────────────────────────────────────
  const currentUserId = currentUser?.id ?? null;
  const perspective = derivePerspective({
    currentUserId,
    players: record.players,
    winnerPlayerIds: record.winnerPlayerIds ?? [],
    outcomeType: record.outcomeType,
    status: record.status,
  });

  // ── Build variant for hero ──────────────────────────────────────────────────
  let heroVariant = perspectiveToHeroVariant(perspective.kind, record.winnerPlayerIds?.length ?? 0);
  // Override pending → planned/inprogress based on actual status
  if (perspective.kind === 'pending') {
    heroVariant = record.status === 'Planned' ? 'planned' : 'inprogress';
  }

  const isCooperative = perspective.kind === 'cooperative';
  const isCompleted = record.status === 'Completed';

  // ── Hero labels ─────────────────────────────────────────────────────────────
  const heroLabels: PlayRecordHeroPodiumLabels = {
    variantWon: t('playRecords.detail.hero.won'),
    variantTied: t('playRecords.detail.hero.tied'),
    variantCooperative: t('playRecords.detail.hero.cooperative'),
    variantInProgress: t('playRecords.detail.hero.inprogress'),
    variantPlanned: t('playRecords.detail.hero.planned'),
    bannerWon: (winnerName, gameName) =>
      t('playRecords.detail.hero.bannerWon', { winnerName, gameName }),
    bannerTied: score => t('playRecords.detail.hero.bannerTied', { score }),
    bannerCooperative: gameName => t('playRecords.detail.hero.bannerCooperative', { gameName }),
    bannerInProgress: (gameName, turn) =>
      turn !== undefined
        ? `${gameName} in corso · turno ${turn}`
        : t('playRecords.detail.hero.bannerInProgress', { gameName }),
    bannerPlanned: gameName => t('playRecords.detail.hero.bannerPlanned', { gameName }),
    metaPlayers: n => t('playRecords.detail.hero.metaPlayers', { count: n }),
    ctaStart: t('playRecords.detail.hero.ctaStart'),
  };

  // ── Data derivatives ────────────────────────────────────────────────────────
  const rankedScores = buildRankedScores(record.players, record.winnerPlayerIds ?? []);
  const clasificaRows = buildClassificaRows(record.players, record.winnerPlayerIds ?? []);
  const breakdownRows = buildBreakdownRows(record.players);
  const { topScore, avgScore, spread } = computeKpis(record.players);

  const formattedDuration = formatDuration(record.duration);
  const formattedDate = formatRelativeDate(record.sessionDate);

  const dimensions = record.scoringConfig.enabledDimensions;

  // Game info for hero (EC-2: freeform emoji fallback)
  const gameForHero = {
    id: record.gameId,
    name: record.gameName,
    coverEmoji: '🎲', // fallback; real cover from useSharedGames in future
  };

  return (
    <div data-testid="play-record-detail" className="flex flex-col">
      {/* Hero Podium — AC-2.2 */}
      <PlayRecordHeroPodium
        variant={heroVariant}
        game={gameForHero}
        rankedScores={rankedScores}
        metadata={{
          date: formattedDate,
          duration: formattedDuration,
          playerCount: record.players.length,
        }}
        perspective={perspective}
        labels={heroLabels}
        onStart={() => router.push(`/play-records/${recordId}/edit`)}
      />

      {/* Connection Bar — AC-2.3 */}
      <ConnectionBar
        gameId={record.gameId}
        gameName={record.gameName}
        playerCount={record.players.length}
        dateLabel={formattedDate}
        chatCount={0}
      />

      {/* Body sections */}
      <div className="mx-auto flex w-full max-w-4xl flex-col gap-5 px-4 py-6 pb-16 sm:px-8 sm:py-8">
        {/* KPI Grid — AC-2.4 */}
        <KpiGrid
          duration={formattedDuration}
          topScore={topScore}
          avgScore={avgScore}
          spread={spread}
        />

        {/* Classifica — AC-2.5 */}
        {clasificaRows.length > 0 && (
          <Classifica
            rows={clasificaRows}
            isCooperative={isCooperative}
            currentUserPlayerId={perspective.currentUserPlayerId}
          />
        )}

        {/* ScoreBreakdown accordion — AC-2.6, EC-10 */}
        {dimensions.length > 1 && <ScoreBreakdown rows={breakdownRows} dimensions={dimensions} />}

        {/* Notes section */}
        {record.notes && (
          <section role="region" aria-label="Note">
            <h2 className="mb-2 flex items-center gap-1.5 font-display text-sm font-extrabold text-foreground">
              <span aria-hidden="true">📝</span>
              Note
            </h2>
            <div
              className="rounded-xl border border-border bg-card px-4 py-3"
              style={{ borderLeft: `3px solid ${entityHsl('session')}` }}
            >
              <p className="text-sm leading-relaxed text-muted-foreground whitespace-pre-wrap">
                {record.notes}
              </p>
            </div>
          </section>
        )}

        {/* Rematch CTA — only for completed non-pending records */}
        {isCompleted && !isCooperative && (
          <section
            className="flex items-center justify-between gap-4 rounded-xl border px-5 py-4"
            style={{
              background: `linear-gradient(135deg, ${entityHsl('game', 0.1)}, ${entityHsl('session', 0.1)})`,
              borderColor: entityHsl('game', 0.3),
            }}
            aria-label="Registra rivincita"
          >
            <div className="min-w-0 flex-1">
              <h3 className="font-display text-base font-extrabold text-foreground">
                🎲 Pronti per la rivincita?
              </h3>
              <p className="text-xs text-muted-foreground">Stessi giocatori e gioco.</p>
            </div>
            <Link
              href="/play-records/new"
              className="shrink-0 rounded-md px-4 py-2 text-sm font-extrabold text-white shadow-md"
              style={{
                background: entityHsl('session'),
                boxShadow: `0 4px 14px ${entityHsl('session', 0.4)}`,
              }}
            >
              ▶ Registra rivincita
            </Link>
          </section>
        )}
      </div>
    </div>
  );
}
