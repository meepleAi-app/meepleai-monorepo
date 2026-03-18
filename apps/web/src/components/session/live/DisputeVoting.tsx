/**
 * DisputeVoting
 *
 * Arbitro v2 — Task 15
 *
 * Democratic override voting interface. Players vote to accept or reject
 * the AI verdict. Includes a 60-second countdown timer.
 */

'use client';

import { useCallback, useEffect, useRef, useState } from 'react';

import { Check, Timer, X } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { api } from '@/lib/api';

// ─── Types ────────────────────────────────────────────────────────────────────

export interface DisputeVotingProps {
  disputeId: string;
  sessionId: string;
  players: { id: string; name: string }[];
  onVotingComplete: (outcome: 'VerdictAccepted' | 'VerdictOverridden') => void;
}

interface VoteRecord {
  playerId: string;
  acceptsVerdict: boolean;
}

// ─── Constants ────────────────────────────────────────────────────────────────

const VOTING_DURATION_SECONDS = 60;

// ─── Component ────────────────────────────────────────────────────────────────

export function DisputeVoting({
  disputeId,
  sessionId,
  players,
  onVotingComplete,
}: DisputeVotingProps) {
  const [votes, setVotes] = useState<VoteRecord[]>([]);
  const [secondsLeft, setSecondsLeft] = useState(VOTING_DURATION_SECONDS);
  const [isFinalized, setIsFinalized] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const timerRef = useRef<ReturnType<typeof setInterval> | null>(null);

  const acceptCount = votes.filter(v => v.acceptsVerdict).length;
  const rejectCount = votes.filter(v => !v.acceptsVerdict).length;

  // ─── Finalize ─────────────────────────────────────────────────────────

  const finalize = useCallback(async () => {
    if (isFinalized) return;
    setIsFinalized(true);

    if (timerRef.current) {
      clearInterval(timerRef.current);
      timerRef.current = null;
    }

    try {
      await api.liveSessions.tallyVotes(sessionId, disputeId);

      const outcome: 'VerdictAccepted' | 'VerdictOverridden' =
        acceptCount > rejectCount ? 'VerdictAccepted' : 'VerdictOverridden';
      onVotingComplete(outcome);
    } catch {
      setError('Errore durante il conteggio dei voti.');
      setIsFinalized(false);
    }
  }, [isFinalized, acceptCount, rejectCount, sessionId, disputeId, onVotingComplete]);

  // ─── Timer ────────────────────────────────────────────────────────────

  useEffect(() => {
    timerRef.current = setInterval(() => {
      setSecondsLeft(prev => {
        if (prev <= 1) return 0;
        return prev - 1;
      });
    }, 1000);

    return () => {
      if (timerRef.current) clearInterval(timerRef.current);
    };
  }, []);

  // Auto-finalize when timer expires
  useEffect(() => {
    if (secondsLeft === 0 && !isFinalized) {
      finalize();
    }
  }, [secondsLeft, isFinalized, finalize]);

  // Auto-finalize when all players have voted
  useEffect(() => {
    if (votes.length === players.length && votes.length > 0 && !isFinalized) {
      finalize();
    }
  }, [votes, players.length, isFinalized, finalize]);

  // ─── Vote handler ─────────────────────────────────────────────────────

  function hasVoted(playerId: string) {
    return votes.some(v => v.playerId === playerId);
  }

  async function handleVote(playerId: string, acceptsVerdict: boolean) {
    if (hasVoted(playerId) || isFinalized) return;

    try {
      setError(null);
      await api.liveSessions.castVote(sessionId, disputeId, playerId, acceptsVerdict);
      setVotes(prev => [...prev, { playerId, acceptsVerdict }]);
    } catch {
      setError('Errore durante il voto.');
    }
  }

  // ─── Render ───────────────────────────────────────────────────────────

  return (
    <div className="border-2 border-amber-400 bg-white/70 backdrop-blur-md rounded-xl p-4 space-y-4">
      {/* Header with timer */}
      <div className="flex items-center justify-between">
        <h3 className="font-quicksand font-bold text-amber-900 text-base flex items-center gap-2">
          <span>Votazione</span>
        </h3>
        <div
          className={`flex items-center gap-1 px-2 py-1 rounded-full text-xs font-nunito font-medium ${
            secondsLeft <= 10 ? 'bg-red-100 text-red-700' : 'bg-gray-100 text-gray-600'
          }`}
          data-testid="vote-timer"
        >
          <Timer className="h-3.5 w-3.5" />
          <span>{secondsLeft}s</span>
        </div>
      </div>

      {/* Vote tally */}
      <div className="flex items-center gap-4 text-sm font-nunito">
        <span className="text-green-700" data-testid="accept-count">
          {acceptCount} accetta{acceptCount !== 1 ? 'no' : ''}
        </span>
        <span className="text-gray-300">|</span>
        <span className="text-red-700" data-testid="reject-count">
          {rejectCount} rifiut{rejectCount !== 1 ? 'ano' : 'a'}
        </span>
      </div>

      {/* Player voting rows */}
      <div className="space-y-2">
        {players.map(player => {
          const playerVote = votes.find(v => v.playerId === player.id);
          const voted = !!playerVote;

          return (
            <div
              key={player.id}
              className="flex items-center justify-between gap-2 bg-gray-50 rounded-lg px-3 py-2"
            >
              <span className="text-sm font-nunito font-medium text-gray-800 truncate">
                {player.name}
              </span>

              {voted ? (
                <span
                  className={`text-xs font-nunito font-medium px-2 py-0.5 rounded-full ${
                    playerVote.acceptsVerdict
                      ? 'bg-green-100 text-green-700'
                      : 'bg-red-100 text-red-700'
                  }`}
                >
                  {playerVote.acceptsVerdict ? 'Accettato' : 'Rifiutato'}
                </span>
              ) : (
                <div className="flex items-center gap-1.5">
                  <Button
                    size="sm"
                    variant="outline"
                    className="h-7 px-2 text-green-700 border-green-300 hover:bg-green-50 font-nunito"
                    onClick={() => handleVote(player.id, true)}
                    disabled={isFinalized}
                    aria-label={`${player.name} accetta`}
                  >
                    <Check className="h-3.5 w-3.5 mr-1" />
                    Accetta
                  </Button>
                  <Button
                    size="sm"
                    variant="outline"
                    className="h-7 px-2 text-red-700 border-red-300 hover:bg-red-50 font-nunito"
                    onClick={() => handleVote(player.id, false)}
                    disabled={isFinalized}
                    aria-label={`${player.name} rifiuta`}
                  >
                    <X className="h-3.5 w-3.5 mr-1" />
                    Rifiuta
                  </Button>
                </div>
              )}
            </div>
          );
        })}
      </div>

      {/* Error */}
      {error && (
        <p className="text-sm text-red-600 font-nunito" role="alert">
          {error}
        </p>
      )}
    </div>
  );
}
