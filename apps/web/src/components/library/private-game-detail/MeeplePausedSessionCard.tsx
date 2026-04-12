'use client';

/**
 * MeeplePausedSessionCard
 *
 * Migration of PausedSessionCard to MeepleCard system.
 * Displays a paused session with resume/abandon actions.
 * Preserves confirmation dialog for old sessions (>30 days).
 */

import { useMemo, useState } from 'react';

import { formatDistanceToNow } from 'date-fns';
import { it } from 'date-fns/locale';
import { Play, Trash2, Users } from 'lucide-react';

const PlayIcon = <Play className="h-4 w-4" />;
const Trash2Icon = <Trash2 className="h-4 w-4" />;

import { MeepleCard, type MeepleCardMetadata } from '@/components/ui/data-display/meeple-card';
import { buildSessionNavItems } from '@/components/ui/data-display/meeple-card/nav-items';
import { ConfirmationDialog } from '@/components/ui/overlays/confirmation-dialog';

export interface PausedSession {
  id: string;
  sessionDate: string;
  currentTurn?: number;
  totalTurns?: number;
  participants: { displayName: string; score: number }[];
  hasPhotos: boolean;
  hasNotes: boolean;
  hasAgentSummary: boolean;
}

interface MeeplePausedSessionCardProps {
  session: PausedSession;
  onResume: (sessionId: string) => void;
  onAbandon: (sessionId: string) => void;
}

const THIRTY_DAYS_MS = 30 * 24 * 60 * 60 * 1000;

export function MeeplePausedSessionCard({
  session,
  onResume,
  onAbandon,
}: MeeplePausedSessionCardProps) {
  const [showOldSessionDialog, setShowOldSessionDialog] = useState(false);

  const isOld = Date.now() - new Date(session.sessionDate).getTime() > THIRTY_DAYS_MS;

  const timeAgo = formatDistanceToNow(new Date(session.sessionDate), {
    addSuffix: true,
    locale: it,
  });

  const turnInfo =
    session.currentTurn != null && session.totalTurns != null
      ? `Turno ${session.currentTurn} di ${session.totalTurns}`
      : undefined;

  const participantSummary = session.participants
    .map(p => `${p.displayName}: ${p.score}`)
    .join(' | ');

  const metadata: MeepleCardMetadata[] = [
    { icon: <Users className="h-4 w-4" />, label: `${session.participants.length} giocatori` },
    ...(turnInfo ? [{ label: turnInfo } as MeepleCardMetadata] : []),
  ];

  const subtitle = [timeAgo, participantSummary].filter(Boolean).join(' \u2022 ');

  const handleResumeClick = () => {
    if (isOld) {
      setShowOldSessionDialog(true);
    } else {
      onResume(session.id);
    }
  };

  const navItems = useMemo(
    () =>
      buildSessionNavItems(
        {
          playerCount: session.participants.length,
          hasNotes: session.hasNotes,
          toolCount: 0,
          photoCount: session.hasPhotos ? 1 : 0,
        },
        {
          // Resume is the primary action; slot handlers no-op in this view
          onPlayersClick: () => {},
          onNotesClick: session.hasNotes ? () => {} : undefined,
          onPhotosClick: session.hasPhotos ? () => {} : undefined,
        }
      ),
    [session.participants.length, session.hasNotes, session.hasPhotos]
  );

  return (
    <>
      <MeepleCard
        entity="session"
        variant="list"
        title="Partita in pausa"
        subtitle={subtitle}
        metadata={metadata}
        badge={isOld ? 'Vecchia' : undefined}
        navItems={navItems}
        actions={[
          {
            icon: PlayIcon,
            label: 'Riprendi',
            onClick: handleResumeClick,
          },
          {
            icon: Trash2Icon,
            label: 'Abbandona',
            onClick: () => onAbandon(session.id),
            variant: 'danger' as const,
          },
        ]}
      />

      <ConfirmationDialog
        isOpen={showOldSessionDialog}
        onClose={() => setShowOldSessionDialog(false)}
        onConfirm={() => {
          setShowOldSessionDialog(false);
          onResume(session.id);
        }}
        title="Riprendere partita vecchia?"
        message="Questa partita è stata messa in pausa più di 30 giorni fa. Vuoi ancora riprenderla?"
        confirmText="Riprendi"
        cancelText="Annulla"
        variant="warning"
      />
    </>
  );
}
