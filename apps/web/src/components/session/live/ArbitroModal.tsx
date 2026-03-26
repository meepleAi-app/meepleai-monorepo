/**
 * ArbitroModal
 *
 * Game Night Improvvisata — Task 17 (v1) + Task 15 (v2)
 *
 * Dialog that lets a player submit a rule dispute and shows the AI verdict.
 * v1: POST /api/v1/game-night/sessions/{sessionId}/disputes (legacy)
 * v2: Structured multi-party dispute flow with voting
 *
 * When v2 is enabled (useV2 prop), the flow is:
 * 1. Initiator enters claim, optionally tags a respondent
 * 2. If respondent tagged: 2-minute timer for response
 * 3. After response or timeout: structured verdict (DisputeVerdictStructured)
 * 4. After verdict: democratic voting (DisputeVoting)
 * 5. After voting: final result
 */

'use client';

import { useCallback, useEffect, useRef, useState } from 'react';

import { Loader2, Timer } from 'lucide-react';

import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';
import { api } from '@/lib/api';
import type { RuleDisputeResponse } from '@/lib/api/schemas/improvvisata.schemas';
import { useLiveSessionStore } from '@/lib/stores/live-session-store';

import { ArbitroVerdictCard } from './ArbitroVerdictCard';
import { DisputeVerdictStructured, type StructuredVerdict } from './DisputeVerdictStructured';
import { DisputeVoting } from './DisputeVoting';

// ─── Types ────────────────────────────────────────────────────────────────────

interface ArbitroModalProps {
  sessionId: string;
  players: Array<{ id?: string; name: string }>;
  onVerdictReceived?: (verdict: RuleDisputeResponse) => void;
  /** Enable v2 structured dispute flow */
  useV2?: boolean;
}

type V2Step = 'form' | 'waiting-response' | 'verdict' | 'voting' | 'result';

const RESPONDENT_TIMEOUT_SECONDS = 120;

// ─── Component ────────────────────────────────────────────────────────────────

export function ArbitroModal({
  sessionId,
  players,
  onVerdictReceived,
  useV2 = false,
}: ArbitroModalProps) {
  const [open, setOpen] = useState(false);
  const [description, setDescription] = useState('');
  const [raisedBy, setRaisedBy] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [verdict, setVerdict] = useState<RuleDisputeResponse | null>(null);
  const [error, setError] = useState<string | null>(null);

  // v2 state
  const [v2Step, setV2Step] = useState<V2Step>('form');
  const [respondent, setRespondent] = useState('');
  const [respondentClaim, setRespondentClaim] = useState('');
  const [disputeId, setDisputeId] = useState<string | null>(null);
  const [structuredVerdict, setStructuredVerdict] = useState<StructuredVerdict | null>(null);
  const [respondentTimer, setRespondentTimer] = useState(RESPONDENT_TIMEOUT_SECONDS);
  const [votingOutcome, setVotingOutcome] = useState<
    'VerdictAccepted' | 'VerdictOverridden' | null
  >(null);
  const timerRef = useRef<ReturnType<typeof setInterval> | null>(null);

  const addDispute = useLiveSessionStore(s => s.addDispute);

  // ─── Reset ──────────────────────────────────────────────────────────

  function resetAll() {
    setDescription('');
    setRaisedBy('');
    setVerdict(null);
    setError(null);
    setV2Step('form');
    setRespondent('');
    setRespondentClaim('');
    setDisputeId(null);
    setStructuredVerdict(null);
    setRespondentTimer(RESPONDENT_TIMEOUT_SECONDS);
    setVotingOutcome(null);
    if (timerRef.current) {
      clearInterval(timerRef.current);
      timerRef.current = null;
    }
  }

  function handleOpenChange(next: boolean) {
    setOpen(next);
    if (!next) resetAll();
  }

  // ─── v2 respondent timeout ─────────────────────────────────────────

  const handleRespondentTimeout = useCallback(async () => {
    if (timerRef.current) {
      clearInterval(timerRef.current);
      timerRef.current = null;
    }
    if (!disputeId) return;

    setIsLoading(true);
    try {
      await api.liveSessions.respondentTimeout(sessionId, disputeId);
      // After timeout, show a placeholder verdict (in production, the backend
      // would set the verdict via SignalR; here we move to a waiting state)
      setV2Step('verdict');
      setStructuredVerdict({
        rulingFor: 'Initiator',
        reasoning: 'Il rispondente non ha fornito una contro-argomentazione entro il tempo limite.',
        citation: null,
        confidence: 'Medium',
      });
    } catch {
      setError('Errore durante il timeout.');
    } finally {
      setIsLoading(false);
    }
  }, [disputeId, sessionId]);

  useEffect(() => {
    if (v2Step === 'waiting-response' && respondentTimer <= 0) {
      handleRespondentTimeout();
    }
  }, [respondentTimer, v2Step, handleRespondentTimeout]);

  // ─── v1 submit ────────────────────────────────────────────────────

  async function handleV1Submit(e: React.FormEvent) {
    e.preventDefault();
    if (!description.trim() || !raisedBy) return;

    setIsLoading(true);
    setError(null);

    try {
      const result = await api.liveSessions.submitDispute(sessionId, description.trim(), raisedBy);
      setVerdict(result);
      onVerdictReceived?.(result);

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

  // ─── v2 submit ────────────────────────────────────────────────────

  async function handleV2Submit(e: React.FormEvent) {
    e.preventDefault();
    if (!description.trim() || !raisedBy) return;

    setIsLoading(true);
    setError(null);

    const initiatorPlayer = players.find(p => p.name === raisedBy);
    const respondentPlayer = respondent ? players.find(p => p.name === respondent) : undefined;

    try {
      const id = await api.liveSessions.openStructuredDispute(
        sessionId,
        initiatorPlayer?.id ?? raisedBy,
        description.trim(),
        respondentPlayer?.id
      );
      setDisputeId(id);

      if (respondentPlayer) {
        // Start waiting for respondent
        setV2Step('waiting-response');
        timerRef.current = setInterval(() => {
          setRespondentTimer(prev => {
            if (prev <= 1) return 0;
            return prev - 1;
          });
        }, 1000);
      } else {
        // No respondent — go straight to verdict (backend resolves immediately)
        setV2Step('verdict');
        setStructuredVerdict({
          rulingFor: 'Initiator',
          reasoning: "Disputa aperta senza rispondente. L'arbitro ha analizzato la richiesta.",
          citation: null,
          confidence: 'Medium',
        });
      }
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'Errore durante la richiesta.';
      setError(message);
    } finally {
      setIsLoading(false);
    }
  }

  // ─── v2 respond ───────────────────────────────────────────────────

  async function handleV2Respond(e: React.FormEvent) {
    e.preventDefault();
    if (!disputeId || !respondentClaim.trim()) return;

    if (timerRef.current) {
      clearInterval(timerRef.current);
      timerRef.current = null;
    }

    setIsLoading(true);
    setError(null);

    const respondentPlayer = players.find(p => p.name === respondent);

    try {
      await api.liveSessions.respondToDispute(
        sessionId,
        disputeId,
        respondentPlayer?.id ?? respondent,
        respondentClaim.trim()
      );
      setV2Step('verdict');
      // Placeholder verdict — in production, set via SignalR or poll
      setStructuredVerdict({
        rulingFor: 'Ambiguous',
        reasoning: "L'arbitro sta valutando entrambe le argomentazioni.",
        citation: null,
        confidence: 'Medium',
      });
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'Errore durante la risposta.';
      setError(message);
    } finally {
      setIsLoading(false);
    }
  }

  // ─── v2 voting complete ───────────────────────────────────────────

  function handleVotingComplete(outcome: 'VerdictAccepted' | 'VerdictOverridden') {
    setVotingOutcome(outcome);
    setV2Step('result');

    // Persist to store
    addDispute({
      id: disputeId ?? crypto.randomUUID(),
      description: description.trim(),
      verdict: structuredVerdict?.reasoning ?? '',
      ruleReferences: structuredVerdict?.citation ? [structuredVerdict.citation] : [],
      raisedByPlayerName: raisedBy,
      timestamp: new Date().toISOString(),
    });
  }

  // ─── v2 advance to voting ─────────────────────────────────────────

  function handleAdvanceToVoting() {
    setV2Step('voting');
  }

  // ─── Render ───────────────────────────────────────────────────────

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogTrigger asChild>
        <Button variant="outline" size="sm">
          ⚖️ Arbitro
        </Button>
      </DialogTrigger>

      <DialogContent className="max-w-md max-h-[85vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle className="font-quicksand flex items-center gap-2">
            <span>⚖️</span>
            <span>Chiedi all&apos;Arbitro</span>
            {useV2 && (
              <span className="text-xs bg-amber-100 text-amber-700 px-1.5 py-0.5 rounded-full font-nunito">
                v2
              </span>
            )}
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

        {/* ─── V1 Flow ─────────────────────────────────────────────── */}
        {!useV2 && (
          <>
            {/* V1 Verdict */}
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

            {/* V1 Form */}
            {!isLoading && !verdict && (
              <form onSubmit={handleV1Submit} className="space-y-4">
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
          </>
        )}

        {/* ─── V2 Flow ─────────────────────────────────────────────── */}
        {useV2 && !isLoading && (
          <>
            {/* V2 Step 1: Form */}
            {v2Step === 'form' && (
              <form onSubmit={handleV2Submit} className="space-y-4">
                <div className="space-y-1.5">
                  <label
                    htmlFor="arbitro-v2-description"
                    className="text-sm font-nunito font-medium text-gray-700"
                  >
                    La tua argomentazione
                  </label>
                  <textarea
                    id="arbitro-v2-description"
                    className="w-full min-h-[100px] rounded-lg border border-gray-300 px-3 py-2 text-sm font-nunito text-gray-900 placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-amber-400 focus:border-transparent resize-none"
                    placeholder="Es: Secondo me questa carta non può essere giocata ora perché..."
                    value={description}
                    onChange={e => setDescription(e.target.value)}
                    required
                  />
                </div>

                <div className="space-y-1.5">
                  <label
                    htmlFor="arbitro-v2-raised-by"
                    className="text-sm font-nunito font-medium text-gray-700"
                  >
                    Chi solleva la disputa
                  </label>
                  <select
                    id="arbitro-v2-raised-by"
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

                <div className="space-y-1.5">
                  <label
                    htmlFor="arbitro-v2-respondent"
                    className="text-sm font-nunito font-medium text-gray-700"
                  >
                    Rispondente (opzionale)
                  </label>
                  <select
                    id="arbitro-v2-respondent"
                    className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm font-nunito text-gray-900 focus:outline-none focus:ring-2 focus:ring-amber-400 focus:border-transparent bg-white"
                    value={respondent}
                    onChange={e => setRespondent(e.target.value)}
                  >
                    <option value="">Nessun rispondente</option>
                    {players
                      .filter(p => p.name !== raisedBy)
                      .map(p => (
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
                  Apri disputa
                </Button>
              </form>
            )}

            {/* V2 Step 2: Waiting for respondent */}
            {v2Step === 'waiting-response' && (
              <div className="space-y-4">
                <div className="flex items-center justify-between bg-amber-50 rounded-lg px-3 py-2">
                  <span className="text-sm font-nunito text-amber-800">
                    In attesa di risposta da <strong>{respondent}</strong>
                  </span>
                  <div
                    className={`flex items-center gap-1 px-2 py-1 rounded-full text-xs font-nunito font-medium ${
                      respondentTimer <= 30
                        ? 'bg-red-100 text-red-700'
                        : 'bg-gray-100 text-gray-600'
                    }`}
                    data-testid="respondent-timer"
                  >
                    <Timer className="h-3.5 w-3.5" />
                    <span>{respondentTimer}s</span>
                  </div>
                </div>

                {/* Respondent reply form */}
                <form onSubmit={handleV2Respond} className="space-y-3">
                  <div className="space-y-1.5">
                    <label
                      htmlFor="arbitro-v2-respondent-claim"
                      className="text-sm font-nunito font-medium text-gray-700"
                    >
                      Risposta di {respondent}
                    </label>
                    <textarea
                      id="arbitro-v2-respondent-claim"
                      className="w-full min-h-[80px] rounded-lg border border-gray-300 px-3 py-2 text-sm font-nunito text-gray-900 placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-amber-400 focus:border-transparent resize-none"
                      placeholder="La mia contro-argomentazione è..."
                      value={respondentClaim}
                      onChange={e => setRespondentClaim(e.target.value)}
                      required
                    />
                  </div>

                  {error && (
                    <p className="text-sm text-red-600 font-nunito" role="alert">
                      {error}
                    </p>
                  )}

                  <Button
                    type="submit"
                    className="w-full bg-amber-500 hover:bg-amber-600 text-white font-nunito"
                    disabled={!respondentClaim.trim()}
                  >
                    Invia risposta
                  </Button>
                </form>
              </div>
            )}

            {/* V2 Step 3: Verdict */}
            {v2Step === 'verdict' && structuredVerdict && (
              <div className="space-y-4">
                <DisputeVerdictStructured
                  verdict={structuredVerdict}
                  initiatorName={raisedBy}
                  respondentName={respondent || null}
                />
                <Button
                  className="w-full bg-amber-500 hover:bg-amber-600 text-white font-nunito"
                  onClick={handleAdvanceToVoting}
                >
                  Procedi alla votazione
                </Button>
              </div>
            )}

            {/* V2 Step 4: Voting */}
            {v2Step === 'voting' && disputeId && (
              <DisputeVoting
                disputeId={disputeId}
                sessionId={sessionId}
                players={players.filter(p => p.id).map(p => ({ id: p.id!, name: p.name }))}
                onVotingComplete={handleVotingComplete}
              />
            )}

            {/* V2 Step 5: Result */}
            {v2Step === 'result' && (
              <div className="space-y-4">
                <div
                  className={`rounded-xl p-4 text-center ${
                    votingOutcome === 'VerdictAccepted'
                      ? 'bg-green-50 border-2 border-green-300'
                      : 'bg-red-50 border-2 border-red-300'
                  }`}
                >
                  <p className="text-lg font-quicksand font-bold">
                    {votingOutcome === 'VerdictAccepted'
                      ? 'Verdetto accettato'
                      : 'Verdetto respinto'}
                  </p>
                  <p className="text-sm font-nunito text-gray-600 mt-1">
                    {votingOutcome === 'VerdictAccepted'
                      ? "I giocatori hanno accettato il verdetto dell'arbitro."
                      : "I giocatori hanno respinto il verdetto dell'arbitro."}
                  </p>
                </div>

                <Button variant="outline" className="w-full font-nunito" onClick={resetAll}>
                  Nuova disputa
                </Button>
              </div>
            )}
          </>
        )}
      </DialogContent>
    </Dialog>
  );
}
