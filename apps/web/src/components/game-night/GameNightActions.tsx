/**
 * GameNightActions — Action buttons for a live game night
 *
 * Renders:
 * - "Aggiungi partita" — navigates to library with gameNightId context
 * - "Concludi serata" — confirmation dialog → completeGameNight API call
 *
 * Plan 2 Task 5 — Session Flow v2.1
 */

'use client';

import { useState } from 'react';

import { CheckCircle, PlusCircle } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { ConfirmationDialog } from '@/components/ui/overlays/confirmation-dialog';
import { Button } from '@/components/ui/primitives/button';
import { useCompleteGameNight } from '@/hooks/queries/useSessionFlow';
import { useToast } from '@/hooks/useToast';

// ─── Component ──────────────────────────────────────────────────────────────

export interface GameNightActionsProps {
  gameNightId: string;
  /** Whether any session is currently in progress. */
  hasActiveSession: boolean;
  /** Total number of sessions in the night. */
  sessionCount: number;
  /** Whether the night is already completed. */
  isCompleted: boolean;
}

export function GameNightActions({
  gameNightId,
  hasActiveSession,
  sessionCount,
  isCompleted,
}: GameNightActionsProps) {
  const router = useRouter();
  const { toast } = useToast();
  const completeMutation = useCompleteGameNight();
  const [showConfirm, setShowConfirm] = useState(false);

  function handleAddGame() {
    // Navigate to the library with the gameNightId as a query param.
    // The game picker (P2-T6) will read this and create a session in the night.
    router.push(`/library?gameNightId=${encodeURIComponent(gameNightId)}`);
  }

  function handleComplete() {
    completeMutation.mutate(gameNightId, {
      onSuccess: (data) => {
        toast({
          title: 'Serata completata!',
          description: `${data.finalizedSessionCount} ${data.finalizedSessionCount === 1 ? 'partita finalizzata' : 'partite finalizzate'}.`,
        });
      },
      onError: () => {
        toast({
          title: 'Errore',
          description: 'Impossibile completare la serata. Riprova.',
          variant: 'destructive',
        });
      },
    });
  }

  if (isCompleted) {
    return null;
  }

  return (
    <>
      <div className="flex gap-2 flex-wrap">
        <Button
          variant="outline"
          onClick={handleAddGame}
          disabled={hasActiveSession}
        >
          <PlusCircle className="h-4 w-4 mr-1" />
          Aggiungi partita
        </Button>

        <Button
          variant="default"
          onClick={() => setShowConfirm(true)}
          disabled={sessionCount === 0 || hasActiveSession || completeMutation.isPending}
        >
          <CheckCircle className="h-4 w-4 mr-1" />
          Concludi serata
        </Button>
      </div>

      <ConfirmationDialog
        isOpen={showConfirm}
        onClose={() => setShowConfirm(false)}
        onConfirm={handleComplete}
        title="Concludi serata"
        message={
          hasActiveSession
            ? 'C\'e ancora una partita in corso. Completala prima di concludere la serata.'
            : `Verranno finalizzate tutte le ${sessionCount} partite della serata. Questa azione non puo essere annullata.`
        }
        confirmText="Concludi"
        cancelText="Annulla"
        variant="warning"
        isLoading={completeMutation.isPending}
      />
    </>
  );
}
