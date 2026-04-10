/**
 * SessionQuickActions — Inline action bar for dice, scores, turn advance
 *
 * Provides one-tap shortcuts for common session gameplay actions:
 * - "Tira dado" — roll dice for a selected participant
 * - "Aggiorna punteggio" — upsert score for a selected participant
 * - "Avanza turno" — advance to the next player
 *
 * Uses useContextualHandStore for actions when available, otherwise
 * falls back to useSessionStore.
 *
 * Plan 2 Task 4 — Session Flow v2.1
 */

'use client';

import { useState, useCallback } from 'react';

import { Dice5, Trophy, ArrowRightCircle, Loader2 } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';
import {
  useContextualHandStore,
  selectCurrentSession,
  selectHasActiveSession,
} from '@/stores/contextual-hand';

// ─── Types ────────────────────────────────────────────────────────────────

interface PlayerOption {
  id: string;
  displayName: string;
}

interface SessionQuickActionsProps {
  /** Player list for the selector dropdowns. */
  participants: PlayerOption[];
  /** Current turn player ID to show in "Avanza turno". */
  currentTurnPlayerId: string | null;
  /** Session ID — used to verify contextual hand store is for this session. */
  sessionId: string;
}

// ─── Component ────────────────────────────────────────────────────────────

export function SessionQuickActions({
  participants,
  currentTurnPlayerId,
  sessionId,
}: SessionQuickActionsProps) {
  const currentSession = useContextualHandStore(selectCurrentSession);
  const hasActiveSession = useContextualHandStore(selectHasActiveSession);
  const rollDice = useContextualHandStore(s => s.rollDice);
  const upsertScore = useContextualHandStore(s => s.upsertScore);
  const advanceTurn = useContextualHandStore(s => s.advanceTurn);

  // Only show quick actions if the contextual hand store is tracking this session
  const isContextualHandActive =
    hasActiveSession && currentSession?.sessionId === sessionId;

  // ── Dice state ──
  const [diceParticipant, setDiceParticipant] = useState('');
  const [diceFormula, setDiceFormula] = useState('2d6');
  const [diceResult, setDiceResult] = useState<{ total: number; rolls: number[] } | null>(null);
  const [isDiceLoading, setIsDiceLoading] = useState(false);

  // ── Score state ──
  const [scoreParticipant, setScoreParticipant] = useState('');
  const [scoreValue, setScoreValue] = useState('');
  const [isScoreLoading, setIsScoreLoading] = useState(false);
  const [scoreSuccess, setScoreSuccess] = useState(false);

  // ── Turn state ──
  const [isTurnLoading, setIsTurnLoading] = useState(false);

  const handleRollDice = useCallback(async () => {
    if (!diceParticipant || !diceFormula) return;
    setIsDiceLoading(true);
    setDiceResult(null);
    try {
      const result = await rollDice(diceParticipant, diceFormula);
      if (result) {
        setDiceResult({ total: result.total, rolls: result.rolls });
      }
    } finally {
      setIsDiceLoading(false);
    }
  }, [diceParticipant, diceFormula, rollDice]);

  const handleUpsertScore = useCallback(async () => {
    if (!scoreParticipant || !scoreValue) return;
    const numValue = parseFloat(scoreValue);
    if (isNaN(numValue)) return;

    setIsScoreLoading(true);
    setScoreSuccess(false);
    try {
      const result = await upsertScore(scoreParticipant, numValue);
      if (result) {
        setScoreSuccess(true);
        setScoreValue('');
        setTimeout(() => setScoreSuccess(false), 2000);
      }
    } finally {
      setIsScoreLoading(false);
    }
  }, [scoreParticipant, scoreValue, upsertScore]);

  const handleAdvanceTurn = useCallback(async () => {
    setIsTurnLoading(true);
    try {
      await advanceTurn();
    } finally {
      setIsTurnLoading(false);
    }
  }, [advanceTurn]);

  // Don't render if no contextual hand session or no participants
  if (!isContextualHandActive || participants.length === 0) {
    return null;
  }

  const currentPlayerName =
    participants.find(p => p.id === currentTurnPlayerId)?.displayName ?? null;

  return (
    <section className="space-y-3" data-testid="session-quick-actions">
      <h2 className="text-sm font-semibold text-muted-foreground uppercase tracking-wider">
        Azioni rapide
      </h2>

      <div className="grid gap-3 sm:grid-cols-3">
        {/* ── Dice Roll ────────────────────────────────────── */}
        <div className="rounded-lg border border-border bg-card p-3 space-y-2">
          <div className="flex items-center gap-2 text-orange-600 dark:text-orange-400">
            <Dice5 className="h-4 w-4" />
            <span className="text-xs font-semibold uppercase tracking-wider">Tira dado</span>
          </div>

          <select
            value={diceParticipant}
            onChange={e => setDiceParticipant(e.target.value)}
            aria-label="Giocatore per lancio dadi"
            className="w-full rounded-md border border-border bg-background px-2 py-1.5 text-sm"
          >
            <option value="">Seleziona giocatore</option>
            {participants.map(p => (
              <option key={p.id} value={p.id}>
                {p.displayName}
              </option>
            ))}
          </select>

          <div className="flex gap-2">
            <input
              type="text"
              value={diceFormula}
              onChange={e => setDiceFormula(e.target.value)}
              placeholder="2d6"
              aria-label="Formula dadi"
              className="flex-1 rounded-md border border-border bg-background px-2 py-1.5 text-sm font-mono"
            />
            <Button
              size="sm"
              onClick={handleRollDice}
              disabled={isDiceLoading || !diceParticipant || !diceFormula}
              className="shrink-0"
            >
              {isDiceLoading ? <Loader2 className="h-3 w-3 animate-spin" /> : 'Tira'}
            </Button>
          </div>

          {diceResult && (
            <div className="rounded-md bg-orange-50 dark:bg-orange-900/20 px-2 py-1.5 text-center">
              <span className="text-lg font-bold text-orange-700 dark:text-orange-300 tabular-nums">
                {diceResult.total}
              </span>
              <span className="ml-2 text-xs text-orange-600 dark:text-orange-400">
                [{diceResult.rolls.join(', ')}]
              </span>
            </div>
          )}
        </div>

        {/* ── Score Update ─────────────────────────────────── */}
        <div className="rounded-lg border border-border bg-card p-3 space-y-2">
          <div className="flex items-center gap-2 text-green-600 dark:text-green-400">
            <Trophy className="h-4 w-4" />
            <span className="text-xs font-semibold uppercase tracking-wider">
              Aggiorna punteggio
            </span>
          </div>

          <select
            value={scoreParticipant}
            onChange={e => setScoreParticipant(e.target.value)}
            aria-label="Giocatore per punteggio"
            className="w-full rounded-md border border-border bg-background px-2 py-1.5 text-sm"
          >
            <option value="">Seleziona giocatore</option>
            {participants.map(p => (
              <option key={p.id} value={p.id}>
                {p.displayName}
              </option>
            ))}
          </select>

          <div className="flex gap-2">
            <input
              type="number"
              value={scoreValue}
              onChange={e => setScoreValue(e.target.value)}
              placeholder="Punti"
              aria-label="Valore punteggio"
              className="flex-1 rounded-md border border-border bg-background px-2 py-1.5 text-sm tabular-nums"
            />
            <Button
              size="sm"
              onClick={handleUpsertScore}
              disabled={isScoreLoading || !scoreParticipant || !scoreValue}
              className={cn('shrink-0', scoreSuccess && 'bg-green-600 hover:bg-green-700')}
            >
              {isScoreLoading ? (
                <Loader2 className="h-3 w-3 animate-spin" />
              ) : scoreSuccess ? (
                '✓'
              ) : (
                'Aggiorna'
              )}
            </Button>
          </div>
        </div>

        {/* ── Advance Turn ─────────────────────────────────── */}
        <div className="rounded-lg border border-border bg-card p-3 space-y-2">
          <div className="flex items-center gap-2 text-blue-600 dark:text-blue-400">
            <ArrowRightCircle className="h-4 w-4" />
            <span className="text-xs font-semibold uppercase tracking-wider">Avanza turno</span>
          </div>

          {currentPlayerName && (
            <p className="text-sm text-muted-foreground">
              Turno corrente: <span className="font-medium text-foreground">{currentPlayerName}</span>
            </p>
          )}

          <Button
            size="sm"
            variant="outline"
            className="w-full"
            onClick={handleAdvanceTurn}
            disabled={isTurnLoading}
          >
            {isTurnLoading ? (
              <Loader2 className="h-3 w-3 animate-spin mr-2" />
            ) : (
              <ArrowRightCircle className="h-3 w-3 mr-2" />
            )}
            Avanza turno
          </Button>
        </div>
      </div>
    </section>
  );
}

export type { SessionQuickActionsProps };
