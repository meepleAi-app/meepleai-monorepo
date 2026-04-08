'use client';

/**
 * MeepleResumeSessionCard
 *
 * Migration of ResumeSessionCard to MeepleCard system.
 * Displays a paused session using the session entity variant.
 */

import { useMemo } from 'react';

import { formatDistanceToNow } from 'date-fns';
import { it } from 'date-fns/locale';
import Link from 'next/link';

import { MeepleCard, type MeepleCardMetadata } from '@/components/ui/data-display/meeple-card';
import { buildSessionNavItems } from '@/components/ui/data-display/meeple-card/nav-items';

export interface MeepleResumeSessionCardProps {
  /** Session ID used to build the resume link. */
  sessionId: string;
  /** Display name of the game. */
  gameName: string;
  /**
   * ISO datetime of the last recorded activity on the session
   * (maps to `LiveSessionSummaryDto.updatedAt`).
   */
  lastActivityAt: string;
  /** Number of players in the session. */
  playerCount: number;
  /** Short invite/join code for the session. */
  sessionCode: string;
  /** Optional number of photos saved for this session. */
  photoCount?: number;
}

export function MeepleResumeSessionCard({
  sessionId,
  gameName,
  lastActivityAt,
  playerCount,
  sessionCode,
  photoCount,
}: MeepleResumeSessionCardProps) {
  const timeAgo = formatDistanceToNow(new Date(lastActivityAt), {
    addSuffix: true,
    locale: it,
  });

  const hasPhotos = typeof photoCount === 'number' && photoCount > 0;

  const metadata: MeepleCardMetadata[] = [
    { label: `${playerCount} giocatori` },
    { label: sessionCode },
    ...(hasPhotos ? [{ label: `${photoCount} foto salvate` } as MeepleCardMetadata] : []),
  ];

  const navItems = useMemo(
    () =>
      buildSessionNavItems(
        {
          playerCount,
          hasNotes: false,
          toolCount: 0,
          photoCount: photoCount ?? 0,
        },
        {
          // All nav slots are no-ops since the Link wraps the card.
          onPlayersClick: () => {},
          onPhotosClick: hasPhotos ? () => {} : undefined,
        }
      ),
    [playerCount, photoCount, hasPhotos]
  );

  return (
    <Link href={`/sessions/${sessionId}/scoreboard`}>
      <MeepleCard
        entity="session"
        variant="list"
        title={gameName}
        subtitle={`In pausa \u2022 ${timeAgo}`}
        metadata={metadata}
        badge="In pausa"
        status="paused"
        navItems={navItems}
      />
    </Link>
  );
}
