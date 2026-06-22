/**
 * PlayerConnectionBar — 6-pip ConnectionBar for /players/[id] Stage 3 cluster (Issue #1113).
 *
 * Translates PlayerProfileFixture + gameCount into 6 canonical ConnectionPip
 * entries. Pips 3-6 (event/agent/toolkit/chat) render with isEmpty=true
 * pending backend schema extension (follow-up issue).
 */

'use client';

import type { JSX } from 'react';
import { useMemo } from 'react';

import {
  Bot,
  Calendar,
  Dices,
  MessageCircle,
  Target,
  Wrench,
} from 'lucide-react';

import { ConnectionBar } from '@/components/ui/data-display/connection-bar/ConnectionBar';
import type { ConnectionPip } from '@/components/ui/data-display/connection-bar/types';
import type { PlayerProfileFixture } from '@/lib/player-detail/player-detail-visual-test-fixture';

export interface PlayerConnectionBarLabels {
  readonly topGames: string;
  readonly sessions: string;
  readonly gameNights: string;
  readonly agents: string;
  readonly toolkits: string;
  readonly chats: string;
}

export interface PlayerConnectionBarProps {
  readonly stats: PlayerProfileFixture;
  /** Distinct game count derived from gamePlayCounts.length by the caller. */
  readonly gameCount: number;
  readonly labels: PlayerConnectionBarLabels;
  readonly className?: string;
}

export function PlayerConnectionBar({
  stats,
  gameCount,
  labels,
  className,
}: PlayerConnectionBarProps): JSX.Element {
  const connections = useMemo<ConnectionPip[]>(
    () => [
      {
        entityType: 'game',
        count: gameCount,
        label: labels.topGames,
        icon: Dices,
        isEmpty: gameCount === 0,
      },
      {
        entityType: 'session',
        count: stats.totalSessions,
        label: labels.sessions,
        icon: Target,
        isEmpty: stats.totalSessions === 0,
      },
      // Pips 3-6: backend doesn't yet expose these counts on PlayerStatistics.
      // Follow-up issue tracks the schema extension; until then we render
      // isEmpty=true so the visual layout matches the mockup exactly.
      {
        entityType: 'event',
        count: 0,
        label: labels.gameNights,
        icon: Calendar,
        isEmpty: true,
      },
      {
        entityType: 'agent',
        count: 0,
        label: labels.agents,
        icon: Bot,
        isEmpty: true,
      },
      {
        entityType: 'toolkit',
        count: 0,
        label: labels.toolkits,
        icon: Wrench,
        isEmpty: true,
      },
      {
        entityType: 'chat',
        count: 0,
        label: labels.chats,
        icon: MessageCircle,
        isEmpty: true,
      },
    ],
    [gameCount, stats.totalSessions, labels],
  );

  return (
    <ConnectionBar
      connections={connections}
      onPipClick={() => {
        /* no-op until backend exposes the data and follow-up wires navigation */
      }}
      className={className}
      data-testid="player-connection-bar"
    />
  );
}
