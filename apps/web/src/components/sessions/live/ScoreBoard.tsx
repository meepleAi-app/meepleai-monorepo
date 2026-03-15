/**
 * ScoreBoard
 *
 * Game Night Improvvisata — Task 16
 *
 * Displays per-player scores from the live-session-store.
 * Host sees +/− buttons to adjust scores and approve/reject pending proposals.
 */

'use client';

import { CheckCircle, Crown, Minus, Plus, XCircle } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { useSessionScores } from '@/lib/hooks/use-session-scores';
import { useSignalRSession } from '@/lib/hooks/use-signalr-session';
import {
  useLiveSessionStore,
  type PlayerInfo,
  type ScoreProposal,
} from '@/lib/stores/live-session-store';

// ─── PlayerScoreCard ──────────────────────────────────────────────────────────

interface PlayerScoreCardProps {
  player: PlayerInfo;
  score: number;
  isLeader: boolean;
  isHost: boolean;
  onIncrement: () => void;
  onDecrement: () => void;
}

function PlayerScoreCard({
  player,
  score,
  isLeader,
  isHost,
  onIncrement,
  onDecrement,
}: PlayerScoreCardProps) {
  return (
    <div
      className={[
        'rounded-xl border p-3 space-y-2 transition-shadow',
        isLeader
          ? 'bg-amber-50 border-amber-300 shadow-md shadow-amber-100'
          : 'bg-white/70 backdrop-blur-md border-white/40 shadow-sm',
        player.isOnline ? '' : 'opacity-60',
      ]
        .filter(Boolean)
        .join(' ')}
    >
      {/* Player name + leader badge */}
      <div className="flex items-center justify-between gap-1">
        <span className="font-quicksand font-semibold text-sm truncate text-gray-900">
          {player.name}
        </span>
        {isLeader && <Crown className="h-4 w-4 shrink-0 text-amber-500" aria-label="Leader" />}
      </div>

      {/* Score */}
      <p className="text-2xl font-quicksand font-bold text-gray-900">{score}</p>

      {/* Online indicator */}
      <div className="flex items-center gap-1">
        <span
          className={[
            'h-2 w-2 rounded-full',
            player.isOnline ? 'bg-green-400' : 'bg-gray-300',
          ].join(' ')}
        />
        <span className="text-xs text-gray-500 font-nunito">
          {player.isOnline ? 'Online' : 'Offline'}
        </span>
      </div>

      {/* Host controls */}
      {isHost && (
        <div className="flex gap-1 pt-1">
          <Button
            variant="outline"
            size="sm"
            className="flex-1 h-8 px-2"
            onClick={onDecrement}
            aria-label={`Decrementa punteggio di ${player.name}`}
          >
            <Minus className="h-3 w-3" />
          </Button>
          <Button
            variant="outline"
            size="sm"
            className="flex-1 h-8 px-2"
            onClick={onIncrement}
            aria-label={`Incrementa punteggio di ${player.name}`}
          >
            <Plus className="h-3 w-3" />
          </Button>
        </div>
      )}
    </div>
  );
}

// ─── ProposalCard ─────────────────────────────────────────────────────────────

interface ProposalCardProps {
  proposal: ScoreProposal;
  onApprove: () => void;
  onReject: () => void;
}

function ProposalCard({ proposal, onApprove, onReject }: ProposalCardProps) {
  const deltaLabel = proposal.delta >= 0 ? `+${proposal.delta}` : String(proposal.delta);

  return (
    <div className="flex items-center justify-between gap-3 rounded-lg bg-white border border-gray-200 px-3 py-2 shadow-sm">
      <div className="flex items-center gap-2 min-w-0">
        <Badge variant="outline" className="shrink-0 font-mono text-xs">
          {deltaLabel}
        </Badge>
        <span className="text-sm font-nunito text-gray-700 truncate">{proposal.playerName}</span>
      </div>

      <div className="flex gap-1 shrink-0">
        <Button
          variant="ghost"
          size="sm"
          className="h-7 w-7 p-0 text-green-600 hover:text-green-700 hover:bg-green-50"
          onClick={onApprove}
          aria-label="Approva proposta"
        >
          <CheckCircle className="h-4 w-4" />
        </Button>
        <Button
          variant="ghost"
          size="sm"
          className="h-7 w-7 p-0 text-red-500 hover:text-red-600 hover:bg-red-50"
          onClick={onReject}
          aria-label="Rifiuta proposta"
        >
          <XCircle className="h-4 w-4" />
        </Button>
      </div>
    </div>
  );
}

// ─── ScoreBoard ────────────────────────────────────────────────────────────────

interface ScoreBoardProps {
  sessionId: string;
  isHost?: boolean;
}

export function ScoreBoard({ sessionId, isHost = false }: ScoreBoardProps) {
  const { scores, players, pendingProposals, leader } = useSessionScores(sessionId);
  const resolveProposal = useLiveSessionStore(s => s.resolveProposal);
  const updateScore = useLiveSessionStore(s => s.updateScore);

  const { sendScore } = useSignalRSession(sessionId);

  function handleScoreChange(playerName: string, delta: number) {
    const current = scores[playerName] ?? 0;
    const next = Math.max(0, current + delta);
    updateScore(playerName, next);
    sendScore(playerName, next).catch((err: unknown) => {
      console.error('[ScoreBoard] sendScore failed:', err);
    });
  }

  function handleApprove(proposal: ScoreProposal) {
    resolveProposal(proposal.id, true);
    const next = (scores[proposal.playerName] ?? 0) + proposal.delta;
    sendScore(proposal.playerName, next).catch((err: unknown) => {
      console.error('[ScoreBoard] sendScore (approve) failed:', err);
    });
  }

  function handleReject(proposalId: string) {
    resolveProposal(proposalId, false);
  }

  return (
    <div className="space-y-6 p-4">
      <h2 className="text-xl font-quicksand font-bold text-gray-900">Punteggi</h2>

      {/* Empty state */}
      {players.length === 0 && (
        <p className="text-sm text-gray-500 font-nunito text-center py-8">
          Nessun giocatore ancora registrato.
        </p>
      )}

      {/* Player score cards */}
      {players.length > 0 && (
        <div className="grid grid-cols-2 gap-3">
          {players.map(player => (
            <PlayerScoreCard
              key={player.id}
              player={player}
              score={scores[player.name] ?? 0}
              isLeader={player.name === leader}
              isHost={isHost}
              onIncrement={() => handleScoreChange(player.name, 1)}
              onDecrement={() => handleScoreChange(player.name, -1)}
            />
          ))}
        </div>
      )}

      {/* Pending proposals — host only */}
      {isHost && pendingProposals.length > 0 && (
        <div className="space-y-2">
          <h3 className="text-sm font-semibold text-gray-500 font-nunito uppercase tracking-wide">
            Proposte in attesa
          </h3>
          <div className="space-y-2">
            {pendingProposals.map(p => (
              <ProposalCard
                key={p.id}
                proposal={p}
                onApprove={() => handleApprove(p)}
                onReject={() => handleReject(p.id)}
              />
            ))}
          </div>
        </div>
      )}

      {/* New round button */}
      <Button variant="outline" className="w-full font-nunito">
        Nuovo Round
      </Button>
    </div>
  );
}
