/**
 * PlayRecordDetailBody — pure, prop-driven body for a single play record.
 *
 * Extracted from PlayRecordDetailView as part of #2437-2 to enable reuse
 * by a public (share-token) view. Receives an already-loaded `record` and
 * `currentUserId` (null for anonymous/spectator contexts); all derivations
 * and the full composition are performed here.
 *
 * @see PlayRecordDetailView — thin wrapper that handles hooks + loading/error guards
 */
/* eslint-disable local/no-hardcoded-color-utility -- text-white / gradient covers intentionally use colored bg following .e-bg mockup pattern */
'use client';

import { useState, type ReactElement } from 'react';

import Link from 'next/link';
import { useRouter } from 'next/navigation';

import { entityHsl } from '@/components/ui/data-display/meeple-card';
import { useTranslation } from '@/hooks/useTranslation';
import type { PlayRecordDto } from '@/lib/api/schemas/play-records.schemas';
import type { SessionPlayer } from '@/lib/api/schemas/play-records.schemas';
import { derivePerspective } from '@/lib/play-records/derivePerspective';
import { formatRelativeDate } from '@/lib/play-records/formatRelativeDate';

import { Classifica, type ClassificaRow } from './detail/Classifica';
import { ConnectionBar } from './detail/ConnectionBar';
import { KpiGrid } from './detail/KpiGrid';
import { ScoreBreakdown, type ScoreBreakdownRow } from './detail/ScoreBreakdown';
import { PlayRecordPhotoGallery } from './photos/PlayRecordPhotoGallery';
import { PlayRecordPhotoUploadDialog } from './photos/PlayRecordPhotoUploadDialog';
import {
  PlayRecordHeroPodium,
  type RankedScore,
  type PlayRecordHeroPodiumLabels,
} from './primitives/PlayRecordHeroPodium';
import { SharePlayRecordDialog } from './SharePlayRecordDialog';

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

// ── Component ─────────────────────────────────────────────────────────────────

export interface PlayRecordDetailBodyProps {
  record: PlayRecordDto;
  currentUserId: string | null;
}

export function PlayRecordDetailBody({
  record,
  currentUserId,
}: PlayRecordDetailBodyProps): ReactElement {
  const { t } = useTranslation();
  const router = useRouter();
  const [photoDialogOpen, setPhotoDialogOpen] = useState(false);
  const [shareOpen, setShareOpen] = useState(false);

  // ── Derive perspective ──────────────────────────────────────────────────────
  const isCreator = currentUserId !== null && currentUserId === record.createdByUserId;
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

  // #2437-1 MVP chip: derive from winnerPlayerIds — only when exactly 1 winner.
  const winnerIds = record.winnerPlayerIds ?? [];
  const mvpName =
    winnerIds.length === 1
      ? (record.players.find(p => p.id === winnerIds[0])?.displayName ?? null)
      : null;

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
        onStart={() => router.push(`/play-records/${record.id}/edit`)}
      />

      {/* Connection Bar — AC-2.3, #2437-1 MVP chip */}
      <ConnectionBar
        gameId={record.gameId}
        gameName={record.gameName}
        playerCount={record.players.length}
        dateLabel={formattedDate}
        chatCount={0}
        mvpName={mvpName}
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

        {/* Photos — #2436 PR-C. The gallery renders its own <h2>; no section aria-label
            to avoid a double screen-reader announcement of the same title. */}
        <section className="flex flex-col gap-2">
          {isCreator && (
            <div className="flex gap-2">
              <button
                type="button"
                onClick={() => setShareOpen(true)}
                className="rounded-md border border-border px-3 py-1.5 text-sm font-bold text-foreground hover:bg-muted"
              >
                🔗 {t('playRecords.share.button')}
              </button>
              <button
                type="button"
                onClick={() => setPhotoDialogOpen(true)}
                className="ml-auto rounded-md border border-border px-3 py-1.5 text-sm font-bold text-foreground hover:bg-muted"
              >
                📷 {t('playRecords.photos.addButton')}
              </button>
            </div>
          )}
          <PlayRecordPhotoGallery
            photos={record.photos ?? []}
            labels={{
              title: t('playRecords.photos.sectionTitle'),
              emptyTitle: t('playRecords.photos.emptyTitle'),
              emptyDescription: t('playRecords.photos.emptyDescription'),
              photoAltFallback: t('playRecords.photos.photoAltFallback'),
              ocrResultTitle: t('playRecords.photos.ocrResultTitle'),
              prev: t('playRecords.photos.lightboxPrev'),
              next: t('playRecords.photos.lightboxNext'),
            }}
          />
        </section>

        {isCreator && (
          <PlayRecordPhotoUploadDialog
            recordId={record.id}
            open={photoDialogOpen}
            onClose={() => setPhotoDialogOpen(false)}
          />
        )}

        {isCreator && (
          <SharePlayRecordDialog
            recordId={record.id}
            currentShareToken={record.shareToken ?? null}
            open={shareOpen}
            onClose={() => setShareOpen(false)}
          />
        )}

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
