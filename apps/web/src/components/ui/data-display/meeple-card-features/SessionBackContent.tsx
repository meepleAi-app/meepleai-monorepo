/**
 * SessionBackContent - Session Card Back Side
 * Issue #4752 - MeepleCard Session Back
 *
 * Renders session-specific back content for FlipCard:
 * - Header with session name + status
 * - Statistics grid (duration, turns, avg score, leader)
 * - Player ranking with medal icons and progress bars
 * - Timeline of turn events
 * - Media counts, agent link, detail link
 * - Status-specific sections (setup config, completed highlights)
 */

'use client';

import React from 'react';

import {
  Bot,
  Camera,
  Clock,
  ExternalLink,
  Mic,
  RotateCw,
  ScrollText,
  Settings,
  Trophy,
  Video,
} from 'lucide-react';
import Link from 'next/link';

import { cn } from '@/lib/utils';

import {
  PLAYER_COLOR_BG,
  PLAYER_COLOR_MAP,
  type SessionBackData,
  type SessionPlayerInfo,
  type SessionStatus,
} from './session-types';

// ============================================================================
// Types
// ============================================================================

export interface SessionBackContentProps {
  status: SessionStatus;
  players: SessionPlayerInfo[];
  backData: SessionBackData;
  /** Session entity color HSL (default indigo) */
  entityColor?: string;
  /** Session title */
  title?: string;
  /** Link to session detail page */
  detailHref?: string;
  className?: string;
}

// ============================================================================
// Medal icons for top 3
// ============================================================================

const MEDALS = ['🥇', '🥈', '🥉'] as const;

/** Validate that href is a safe relative path (not external/javascript) */
const isSafeHref = (href: string) => href.startsWith('/') && !href.startsWith('//');

// ============================================================================
// Sub-components
// ============================================================================

function StatItem({ icon: Icon, label, value }: {
  icon: typeof Clock;
  label: string;
  value: string | number;
}) {
  return (
    <div className="flex flex-col items-center gap-0.5" data-testid={`stat-${label.toLowerCase().replace(/\s/g, '-')}`}>
      <Icon className="w-3.5 h-3.5 text-muted-foreground" />
      <span className="text-xs font-bold tabular-nums text-card-foreground max-w-[4rem] truncate">{value}</span>
      <span className="text-[9px] text-muted-foreground">{label}</span>
    </div>
  );
}

function PlayerRankRow({ player, maxScore }: {
  player: SessionPlayerInfo;
  maxScore: number;
}) {
  const medal = player.currentRank <= 3
    // eslint-disable-next-line security/detect-object-injection
    ? MEDALS[player.currentRank - 1]
    : undefined;
  const barWidth = maxScore > 0 ? Math.max(0, Math.min(100, (player.totalScore / maxScore) * 100)) : 0;
  // eslint-disable-next-line security/detect-object-injection
  const colorHsl = PLAYER_COLOR_MAP[player.color];

  return (
    <div
      className="flex items-center gap-2"
      data-testid={`rank-row-${player.id}`}
    >
      <span className="w-5 text-center text-xs" aria-label={medal ? `Rank ${player.currentRank}` : undefined}>
        {medal ?? `#${player.currentRank}`}
      </span>
      <span
        // eslint-disable-next-line security/detect-object-injection
        className={cn('w-2 h-2 rounded-full flex-shrink-0', PLAYER_COLOR_BG[player.color])}
      />
      <span className="flex-1 min-w-0 text-xs font-medium truncate text-card-foreground">
        {player.displayName}
      </span>
      <div className="w-16 h-1.5 rounded-full bg-muted overflow-hidden">
        <div
          className="h-full rounded-full transition-all duration-300"
          style={{
            width: `${barWidth}%`,
            backgroundColor: `hsl(${colorHsl})`,
          }}
        />
      </div>
      <span className="text-xs font-bold tabular-nums text-card-foreground w-8 text-right">
        {player.totalScore}
      </span>
    </div>
  );
}

// ============================================================================
// Main Component
// ============================================================================

export const SessionBackContent = React.memo(function SessionBackContent({
  status,
  players,
  backData,
  entityColor = '240 60% 55%',
  title,
  detailHref,
  className,
}: SessionBackContentProps) {
  const sortedPlayers = [...players].sort((a, b) => a.currentRank - b.currentRank);
  const maxScore = players.reduce((max, p) => Math.max(max, p.totalScore), 1);
  const winner = sortedPlayers[0];
  const hasMedia = !!backData.mediaCounts &&
    (backData.mediaCounts.photos > 0 || backData.mediaCounts.videos > 0 || backData.mediaCounts.audio > 0);

  const statusLabels: Record<SessionStatus, string> = {
    setup: 'Configurazione',
    inProgress: 'In Corso',
    paused: 'In Pausa',
    completed: 'Completata',
  };

  return (
    <div
      className={cn('flex h-full flex-col overflow-hidden', className)}
      data-testid="session-back-content"
    >
      {/* Entity-colored header */}
      <div
        className="relative overflow-hidden px-5 pb-3 pt-5"
        style={{ backgroundColor: `hsl(${entityColor})` }}
      >
        <div
          className="pointer-events-none absolute inset-0 opacity-[0.12]"
          style={{
            backgroundImage: 'repeating-linear-gradient(45deg, transparent, transparent 10px, currentColor 10px, currentColor 11px)',
          }}
          aria-hidden="true"
        />
        <h2 className="relative z-[1] font-quicksand text-lg font-bold text-white">
          {title || 'Sessione'}
        </h2>
        <p className="relative z-[1] text-xs text-white/80 mt-0.5">
          {/* eslint-disable-next-line security/detect-object-injection */}
          {statusLabels[status]}
        </p>
      </div>

      {/* Content area */}
      <div className="flex flex-1 flex-col overflow-y-auto px-5 py-3 gap-3">
        {/* Setup-specific: config details */}
        {status === 'setup' && (
          <div className="space-y-1.5" data-testid="setup-config">
            <h3 className="flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wider text-muted-foreground">
              <Settings className="w-3 h-3" />
              Configurazione
            </h3>
            {backData.sessionCode && (
              <p className="text-sm text-card-foreground">
                Codice: <span className="font-mono font-bold">{backData.sessionCode}</span>
              </p>
            )}
            {backData.scoringType && (
              <p className="text-xs text-muted-foreground">Punteggio: {backData.scoringType}</p>
            )}
            {backData.turnType && (
              <p className="text-xs text-muted-foreground">Turni: {backData.turnType}</p>
            )}
            <p className="text-xs text-muted-foreground">
              Giocatori: {players.filter((p) => p.role !== 'spectator').length}
            </p>
          </div>
        )}

        {/* Statistics grid (not shown in setup) */}
        {status !== 'setup' && (
          <div className="grid grid-cols-4 gap-2" data-testid="stats-grid">
            <StatItem
              icon={Clock}
              label="Durata"
              value={backData.durationMinutes ? `${backData.durationMinutes}m` : '—'}
            />
            <StatItem
              icon={RotateCw}
              label="Turni"
              value={backData.totalTurns ?? '—'}
            />
            <StatItem
              icon={Trophy}
              label="Media"
              value={backData.averageScore != null ? Math.round(backData.averageScore) : '—'}
            />
            <StatItem
              icon={Trophy}
              label={status === 'completed' ? 'Vincitore' : 'Leader'}
              value={winner?.displayName ?? '—'}
            />
          </div>
        )}

        {/* Ranking (not shown in setup) */}
        {status !== 'setup' && sortedPlayers.length > 0 && (
          <div data-testid="ranking-section">
            <h3 className="mb-1.5 text-xs font-semibold uppercase tracking-wider text-muted-foreground">
              {status === 'completed' ? 'Classifica Finale' : 'Classifica'}
            </h3>
            <div className="space-y-1.5">
              {sortedPlayers.map((player) => (
                <PlayerRankRow key={player.id} player={player} maxScore={maxScore} />
              ))}
            </div>
          </div>
        )}

        {/* Timeline (limited to last 5 events) */}
        {backData.timeline && backData.timeline.length > 0 && status !== 'setup' && (
          <div data-testid="timeline-section">
            <h3 className="mb-1.5 text-xs font-semibold uppercase tracking-wider text-muted-foreground">
              Timeline
            </h3>
            <div className="space-y-1 border-l-2 border-border/40 pl-3">
              {backData.timeline.slice(-5).map((event) => (
                <div key={event.id} className="flex items-start gap-2 text-xs">
                  <span className="w-1.5 h-1.5 rounded-full bg-muted-foreground mt-1 flex-shrink-0" />
                  <span className="text-card-foreground">{event.label}</span>
                </div>
              ))}
            </div>
          </div>
        )}

        {/* Media counts */}
        {hasMedia && backData.mediaCounts && (
          <div className="flex items-center gap-3" data-testid="media-counts">
            {backData.mediaCounts.photos > 0 && (
              <span className="flex items-center gap-1 text-xs text-muted-foreground" aria-label={`${backData.mediaCounts.photos} foto`}>
                <Camera className="w-3 h-3" aria-hidden="true" /> {backData.mediaCounts.photos}
              </span>
            )}
            {backData.mediaCounts.videos > 0 && (
              <span className="flex items-center gap-1 text-xs text-muted-foreground" aria-label={`${backData.mediaCounts.videos} video`}>
                <Video className="w-3 h-3" aria-hidden="true" /> {backData.mediaCounts.videos}
              </span>
            )}
            {backData.mediaCounts.audio > 0 && (
              <span className="flex items-center gap-1 text-xs text-muted-foreground" aria-label={`${backData.mediaCounts.audio} audio`}>
                <Mic className="w-3 h-3" aria-hidden="true" /> {backData.mediaCounts.audio}
              </span>
            )}
          </div>
        )}

        {/* Agent link */}
        {backData.agentChatHref && isSafeHref(backData.agentChatHref) && (
          <Link
            href={backData.agentChatHref}
            className="flex items-center gap-1.5 text-xs font-medium text-indigo-500 hover:text-indigo-600 transition-colors"
            onClick={(e) => e.stopPropagation()}
            data-testid="agent-chat-link"
          >
            <Bot className="w-3.5 h-3.5" />
            Chat con Agente AI
          </Link>
        )}

        {/* Completed: PlayRecord link */}
        {status === 'completed' && backData.playRecordHref && isSafeHref(backData.playRecordHref) && (
          <Link
            href={backData.playRecordHref}
            className="flex items-center gap-1.5 text-xs font-medium text-indigo-500 hover:text-indigo-600 transition-colors"
            onClick={(e) => e.stopPropagation()}
            data-testid="playrecord-link"
          >
            <ScrollText className="w-3.5 h-3.5" />
            Vedi PlayRecord
          </Link>
        )}

        {/* Detail link */}
        {detailHref && (
          <div className="mt-auto pt-2">
            <Link
              href={detailHref}
              className="inline-flex items-center gap-1.5 text-sm font-medium transition-colors"
              style={{ color: `hsl(${entityColor})` }}
              onClick={(e) => e.stopPropagation()}
              data-testid="session-detail-link"
            >
              <ExternalLink className="h-3.5 w-3.5" />
              Vai alla pagina sessione
            </Link>
          </div>
        )}
      </div>
    </div>
  );
});
