/**
 * GuestScoreProposal
 *
 * Game Night Improvvisata — Task 18
 *
 * Form for a guest player to propose a score delta for themselves.
 * Business rules:
 *   - Self-only: guest can only propose for themselves
 *   - Delta range: -100 to +100
 *   - Max 3 pending proposals per guest
 */

'use client';

import { useState } from 'react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { useLiveSessionStore, type ScoreProposal } from '@/lib/stores/live-session-store';

const MAX_PENDING = 3;
const MIN_DELTA = -100;
const MAX_DELTA = 100;

interface GuestScoreProposalProps {
  guestName: string;
  onPropose: (delta: number) => Promise<void>;
}

export function GuestScoreProposal({ guestName, onPropose }: GuestScoreProposalProps) {
  const [delta, setDelta] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [lastSubmitted, setLastSubmitted] = useState<number | null>(null);

  const pendingProposals = useLiveSessionStore(s => s.pendingProposals);
  const myPendingCount = pendingProposals.filter(
    (p: ScoreProposal) => p.playerName === guestName
  ).length;

  const atLimit = myPendingCount >= MAX_PENDING;

  function validate(value: string): string | null {
    const num = parseInt(value, 10);
    if (isNaN(num)) return 'Inserisci un numero valido';
    if (num < MIN_DELTA || num > MAX_DELTA) return `Valore tra ${MIN_DELTA} e +${MAX_DELTA}`;
    return null;
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    const validationError = validate(delta);
    if (validationError) {
      setError(validationError);
      return;
    }
    if (atLimit) {
      setError(`Hai già ${MAX_PENDING} proposte in attesa`);
      return;
    }

    setError(null);
    setIsSubmitting(true);
    try {
      const num = parseInt(delta, 10);
      await onPropose(num);
      setLastSubmitted(num);
      setDelta('');
    } catch {
      setError("Errore durante l'invio. Riprova.");
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <div className="rounded-xl bg-white/70 backdrop-blur-md border border-white/40 shadow-sm p-4 space-y-3">
      <div className="flex items-center justify-between">
        <h3 className="font-quicksand font-semibold text-gray-900 text-sm">Proponi punteggio</h3>
        {myPendingCount > 0 && (
          <Badge variant="outline" className="text-xs">
            {myPendingCount}/{MAX_PENDING} in attesa
          </Badge>
        )}
      </div>

      {atLimit ? (
        <p className="text-sm text-amber-700 font-nunito bg-amber-50 rounded-lg p-3 border border-amber-200">
          Hai raggiunto il limite di {MAX_PENDING} proposte in attesa. Attendi che l&apos;host le
          approvi.
        </p>
      ) : (
        <form onSubmit={handleSubmit} className="space-y-2">
          <div className="flex gap-2">
            <Input
              type="number"
              min={MIN_DELTA}
              max={MAX_DELTA}
              value={delta}
              onChange={e => {
                setDelta(e.target.value);
                setError(null);
              }}
              placeholder="es. +5 o -3"
              className="flex-1 font-mono text-center"
              aria-label="Delta punteggio"
              disabled={isSubmitting}
            />
            <Button
              type="submit"
              size="sm"
              disabled={isSubmitting || !delta}
              className="bg-amber-500 hover:bg-amber-600 text-white"
            >
              {isSubmitting ? 'Invio...' : 'Proponi'}
            </Button>
          </div>
          {error && (
            <p className="text-xs text-red-600 font-nunito" role="alert">
              {error}
            </p>
          )}
          <p className="text-xs text-gray-500 font-nunito">
            Proposta per: <span className="font-semibold">{guestName}</span>
          </p>
        </form>
      )}

      {lastSubmitted !== null && !atLimit && (
        <p className="text-xs text-green-700 font-nunito" role="status">
          Proposta di {lastSubmitted >= 0 ? `+${lastSubmitted}` : lastSubmitted} inviata. Attendi
          approvazione.
        </p>
      )}
    </div>
  );
}
