'use client';

/**
 * MeepleResumeSessionCard
 *
 * Migration of ResumeSessionCard to MeepleCard system.
 * Displays a paused session using the session entity variant.
 */

import { formatDistanceToNow } from 'date-fns';
import { it } from 'date-fns/locale';
import { Camera, Hash, Users } from 'lucide-react';
import Link from 'next/link';

import { MeepleCard, type MeepleCardMetadata } from '@/components/ui/data-display/meeple-card';

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
    { icon: Users, label: `${playerCount} giocatori` },
    { icon: Hash, label: sessionCode },
    ...(hasPhotos
      ? [{ icon: Camera, label: `${photoCount} foto salvate` } as MeepleCardMetadata]
      : []),
  ];

  return (
    <Link href={`/sessions/${sessionId}/scoreboard`}>
      <MeepleCard
        entity="session"
        variant="list"
        title={gameName}
        subtitle={`In pausa \u2022 ${timeAgo}`}
        metadata={metadata}
        badge="In pausa"
        sessionStatus="paused"
        primaryActions={[
          {
            icon: '\u25B6',
            label: 'Riprendi partita',
            onClick: () => {
              /* navigation handled by Link wrapper */
            },
          },
        ]}
      />
    </Link>
  );
}
