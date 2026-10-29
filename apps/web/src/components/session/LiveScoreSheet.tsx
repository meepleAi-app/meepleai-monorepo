/**
 * LiveScoreSheet — Bottom sheet wrapper for ScoreInput
 *
 * Adapts LiveSessionPlayerDto → Participant for the existing ScoreInput component,
 * then bridges onSubmit → store.recordScore with RecordScoreRequest shape.
 *
 * Issue #5041 — Sessions Redesign Phase 2
 */

'use client';

import { useCallback } from 'react';

import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
} from '@/components/ui/navigation/sheet';
import type { LiveSessionPlayerDto } from '@/lib/api/schemas/live-sessions.schemas';
import { useSessionStore } from '@/lib/stores/session-store';

import { toParticipant } from './adapters';
import { ScoreInput } from './ScoreInput';

interface LiveScoreSheetProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  players: LiveSessionPlayerDto[];
  currentUserId: string | null;
  currentRound: number;
  dimensions: string[];
}

export function LiveScoreSheet({
  open,
  onOpenChange,
  players,
  currentUserId,
  currentRound,
  dimensions,
}: LiveScoreSheetProps) {
  const recordScore = useSessionStore(s => s.recordScore);

  const participants = players.map(p => toParticipant(p, currentUserId));
  const rounds = Array.from({ length: currentRound }, (_, i) => i + 1);
  const categories = dimensions.filter(d => d !== 'default');
  // What "No Category" records into: the session's own 'default' dimension, or else its first configured one.
  // Hard-coding 'default' made every score added by hand fail (400 "Dimension 'default' is not enabled in this
  // session's scoring config") on a session created with only "Punti", which is what the creation wizard configures.
  const noCategoryDimension = dimensions.includes('default')
    ? 'default'
    : (dimensions[0] ?? 'default');
  // A lone configured dimension and no 'default' leaves nothing to choose: no select.
  const offerCategories =
    categories.length > 1 || (categories.length === 1 && dimensions.includes('default'));

  const handleSubmit = useCallback(
    async (data: {
      participantId: string;
      roundNumber: number | null;
      category: string | null;
      scoreValue: number;
    }) => {
      await recordScore({
        playerId: data.participantId,
        round: data.roundNumber ?? currentRound,
        dimension: data.category || noCategoryDimension,
        value: data.scoreValue,
      });
      onOpenChange(false);
    },
    [recordScore, currentRound, onOpenChange, noCategoryDimension]
  );

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent side="bottom" className="max-h-[85vh] overflow-y-auto px-4 pb-8 pt-2">
        <SheetHeader className="pb-2">
          {/* Drag handle */}
          <div className="mx-auto mb-2 h-1.5 w-12 rounded-full bg-muted-foreground/30" />
          <SheetTitle>Aggiungi Punteggio</SheetTitle>
          <SheetDescription>Seleziona giocatore, round e inserisci il punteggio</SheetDescription>
        </SheetHeader>

        <ScoreInput
          participants={participants}
          rounds={rounds}
          categories={offerCategories ? categories : undefined}
          currentRound={currentRound}
          onSubmit={handleSubmit}
        />
      </SheetContent>
    </Sheet>
  );
}
