/**
 * MeepleEventCard — Event (game night) entity adapter for MeepleCard.
 *
 * Renders an upcoming or past game night with nav-footer wired to participants,
 * location map, games list, and calendar.
 */
/* eslint-disable @typescript-eslint/no-non-null-assertion -- pre-existing pattern: array/object access guarded by length/key check or by upstream validator; assertion is correct by construction. Cleanup tracked for follow-up audit. */

'use client';

import { useMemo } from 'react';

import { format } from 'date-fns';
import { it } from 'date-fns/locale';
import { useRouter } from 'next/navigation';

import { MeepleCard, type MeepleCardVariant } from '@/components/ui/data-display/meeple-card';
import { buildEventConnections } from '@/components/ui/data-display/meeple-card/nav-items';

// ============================================================================
// Types
// ============================================================================

export interface GameNightSummary {
  id: string;
  title: string;
  /** ISO date string */
  scheduledAt: string;
  location?: string | null;
  participantCount: number;
  gameCount: number;
}

export interface MeepleEventCardProps {
  event: GameNightSummary;
  variant?: MeepleCardVariant;
  onClick?: (eventId: string) => void;
  className?: string;
}

// ============================================================================
// Component
// ============================================================================

export function MeepleEventCard({
  event,
  variant = 'grid',
  onClick,
  className,
}: MeepleEventCardProps) {
  const router = useRouter();

  const subtitle = useMemo(() => {
    try {
      return format(new Date(event.scheduledAt), "EEEE d MMM 'alle' HH:mm", { locale: it });
    } catch {
      return event.scheduledAt ?? 'Data non disponibile';
    }
  }, [event.scheduledAt]);

  const connections = useMemo(
    () =>
      buildEventConnections(
        {
          participantCount: event.participantCount,
          gameCount: event.gameCount,
        },
        {
          onParticipantsClick: () => router.push(`/game-nights/${event.id}/participants`),
          onLocationClick: event.location
            ? () =>
                window.open(
                  `https://maps.google.com/?q=${encodeURIComponent(event.location!)}`,
                  '_blank',
                  'noopener,noreferrer'
                )
            : undefined,
          onGamesClick: () => router.push(`/game-nights/${event.id}/games`),
          onDateClick: () => router.push(`/calendar?date=${event.scheduledAt}`),
        }
      ),
    [event.id, event.participantCount, event.gameCount, event.location, event.scheduledAt, router]
  );

  return (
    <MeepleCard
      id={event.id}
      entity="event"
      variant={variant}
      title={event.title}
      subtitle={subtitle}
      connections={connections}
      onClick={onClick ? () => onClick(event.id) : undefined}
      className={className}
      data-testid={`event-card-${event.id}`}
    />
  );
}

export default MeepleEventCard;
