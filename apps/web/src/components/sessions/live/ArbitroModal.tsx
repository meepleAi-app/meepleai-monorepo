/**
 * ArbitroModal
 *
 * Game Night Improvvisata — Task 17
 *
 * Dialog that lets a player submit a rule dispute and shows the AI verdict.
 * Triggers POST /api/v1/game-night/sessions/{sessionId}/disputes
 */

'use client';

import { useState } from 'react';

import { Loader2 } from 'lucide-react';

import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/overlays/dialog';
import { api } from '@/lib/api';
import type { RuleDisputeResponse } from '@/lib/api/schemas/improvvisata.schemas';
import { useLiveSessionStore } from '@/lib/stores/live-session-store';

import { ArbitroVerdictCard } from './ArbitroVerdictCard';

// ─── Types ────────────────────────────────────────────────────────────────────

interface ArbitroModalProps {
  sessionId: string;
  players: Array<{ name: string }>;
  onVerdictReceived?: (verdict: RuleDisputeResponse) => void;
}

// ─── Component ────────────────────────────────────────────────────────────────

export function ArbitroModal({ sessionId, players, onVerdictReceived }: ArbitroModalProps) {
  const [open, setOpen] = useState(false);
  const [description, setDescription] = useState('');
  const [raisedBy, setRaisedBy] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [verdict, setVerdict] = useState<RuleDisputeResponse | null>(null);
  const [error, setError] = useState<string | null>(null);

  const addDispute = useLiveSessionStore(s => s.addDispute);

  function handleOpenChange(next: boolean) {
    setOpen(next);
    if (!next) {
      // Reset form on close
      setDescription('');
      setRaisedBy('');
      setVerdict(null);
      setError(null);
    }
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!description.trim() || !raisedBy) return;

    setIsLoading(true);
    setError(null);

    try {
      const result = await api.liveSessions.submitDispute(sessionId, description.trim(), raisedBy);
      setVerdict(result);
      onVerdictReceived?.(result);

      // Persist to store so DisputeHistory picks it up
      addDispute({
        id: result.id,
        description: description.trim(),
        verdict: result.verdict,
        ruleReferences: result.ruleReferences,
        raisedByPlayerName: raisedBy,
        timestamp: new Date().toISOString(),
      });
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'Errore durante la richiesta.';
      setError(message);
    } finally {
      setIsLoading(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogTrigger asChild>
        <Button variant="outline" size="sm">
          ⚖️ Arbitro
        </Button>
      </DialogTrigger>

      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle className="font-quicksand flex items-center gap-2">
            <span>⚖️</span>
            <span>Chiedi all&apos;Arbitro</span>
          </DialogTitle>
        </DialogHeader>

        {/* Loading */}
        {isLoading && (
          <div className="flex flex-col items-center justify-center py-10 gap-3">
            <Loader2 className="h-8 w-8 animate-spin text-amber-500" />
            <p className="text-sm text-gray-500 font-nunito">
              L&apos;arbitro sta analizzando il regolamento...
            </p>
          </div>
        )}

        {/* Verdict */}
        {!isLoading && verdict && (
          <div className="space-y-4">
            <ArbitroVerdictCard verdict={verdict} />
            <Button
              variant="outline"
              className="w-full font-nunito"
              onClick={() => {
                setVerdict(null);
                setDescription('');
                setRaisedBy('');
              }}
            >
              Nuova disputa
            </Button>
          </div>
        )}

        {/* Form */}
        {!isLoading && !verdict && (
          <form onSubmit={handleSubmit} className="space-y-4">
            <div className="space-y-1.5">
              <label
                htmlFor="arbitro-description"
                className="text-sm font-nunito font-medium text-gray-700"
              >
                Descrivi la disputa
              </label>
              <textarea
                id="arbitro-description"
                className="w-full min-h-[100px] rounded-lg border border-gray-300 px-3 py-2 text-sm font-nunito text-gray-900 placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-amber-400 focus:border-transparent resize-none"
                placeholder="Es: È possibile giocare questa carta durante il turno dell'avversario?"
                value={description}
                onChange={e => setDescription(e.target.value)}
                required
              />
            </div>

            <div className="space-y-1.5">
              <label
                htmlFor="arbitro-raised-by"
                className="text-sm font-nunito font-medium text-gray-700"
              >
                Sollevata da
              </label>
              <select
                id="arbitro-raised-by"
                className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm font-nunito text-gray-900 focus:outline-none focus:ring-2 focus:ring-amber-400 focus:border-transparent bg-white"
                value={raisedBy}
                onChange={e => setRaisedBy(e.target.value)}
                required
              >
                <option value="" disabled>
                  Seleziona il giocatore…
                </option>
                {players.map(p => (
                  <option key={p.name} value={p.name}>
                    {p.name}
                  </option>
                ))}
              </select>
            </div>

            {error && (
              <p className="text-sm text-red-600 font-nunito" role="alert">
                {error}
              </p>
            )}

            <Button
              type="submit"
              className="w-full bg-amber-500 hover:bg-amber-600 text-white font-nunito"
              disabled={!description.trim() || !raisedBy}
            >
              Chiedi all&apos;Arbitro
            </Button>
          </form>
        )}
      </DialogContent>
    </Dialog>
  );
}
