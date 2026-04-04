'use client';

/**
 * MeeplePausedSessionCard
 *
 * Migration of PausedSessionCard to MeepleCard system.
 * Displays a paused session with resume/abandon actions.
 * Preserves confirmation dialog for old sessions (>30 days).
 */

import { useState } from 'react';

import { formatDistanceToNow } from 'date-fns';
import { it } from 'date-fns/locale';
import { Play, Trash2, Users } from 'lucide-react';

import { MeepleCard, type MeepleCardMetadata } from '@/components/ui/data-display/meeple-card';
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
    { icon: Users, label: `${session.participants.length} giocatori` },
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

  return (
    <>
      <MeepleCard
        entity="session"
        variant="list"
        title="Partita in pausa"
        subtitle={subtitle}
        metadata={metadata}
        badge={isOld ? 'Vecchia' : undefined}
        sessionStatus="paused"
        quickActions={[
          {
            icon: Play,
            label: 'Riprendi',
            onClick: handleResumeClick,
          },
          {
            icon: Trash2,
            label: 'Abbandona',
            onClick: () => onAbandon(session.id),
            destructive: true,
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
