/**
 * GameOverModal — shown when a session is completed
 *
 * Game Session Flow v2.0 — Task 11
 *
 * Displays final rankings and provides navigation to play records.
 */

'use client';

import { Trophy, Medal, Crown } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { GradientButton } from '@/components/ui/buttons/GradientButton';
import { cn } from '@/lib/utils';

export interface GameOverPlayer {
  id: string;
  displayName: string;
  totalScore: number;
  rank: number;
  avatarColor: string;
}

interface GameOverModalProps {
  gameName: string;
  players: GameOverPlayer[];
  sessionId: string;
  onClose?: () => void;
}

const RANK_ICONS = [
  <Trophy key="1" className="h-5 w-5 text-amber-400" />,
  <Medal key="2" className="h-5 w-5 text-slate-300" />,
  <Medal key="3" className="h-5 w-5 text-amber-700" />,
];

export function GameOverModal({ gameName, players, sessionId, onClose }: GameOverModalProps) {
  const router = useRouter();

  const sorted = [...players].sort((a, b) => a.rank - b.rank);
  const winner = sorted[0];

  const handleGoToRecords = () => {
    router.push(`/sessions/${sessionId}`);
    onClose?.();
  };

  const handleNewSession = () => {
    router.push('/sessions/new');
    onClose?.();
  };

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-label="Partita terminata"
      className="fixed inset-0 z-50 flex items-end sm:items-center justify-center bg-black/70 backdrop-blur-sm"
    >
      <div className="w-full max-w-md bg-[var(--gaming-card-bg,#1a1a2e)] border border-white/10 rounded-t-2xl sm:rounded-2xl p-6 space-y-6">
        {/* Header */}
        <div className="text-center space-y-2">
          <div className="flex justify-center">
            <Crown className="h-10 w-10 text-amber-400" />
          </div>
          <h2 className="text-2xl font-bold font-quicksand">Partita terminata!</h2>
          <p className="text-sm text-muted-foreground">{gameName}</p>
          {winner && (
            <p className="text-base font-semibold text-amber-300">
              Vincitore: {winner.displayName} ({winner.totalScore} pts)
            </p>
          )}
        </div>

        {/* Rankings */}
        <div className="space-y-2">
          {sorted.map(player => (
            <div
              key={player.id}
              className={cn(
                'flex items-center gap-3 rounded-xl px-4 py-3 border',
                player.rank === 1
                  ? 'border-amber-500/50 bg-amber-500/10'
                  : 'border-white/10 bg-white/5'
              )}
            >
              {/* Rank icon */}
              <div className="w-6 flex justify-center shrink-0">
                {player.rank <= 3 ? (
                  RANK_ICONS[player.rank - 1]
                ) : (
                  <span className="text-sm font-bold text-muted-foreground">#{player.rank}</span>
                )}
              </div>

              {/* Avatar */}
              <div
                className="h-9 w-9 rounded-lg flex items-center justify-center text-xs font-bold text-white shrink-0"
                style={{ backgroundColor: player.avatarColor }}
              >
                {player.displayName.slice(0, 2).toUpperCase()}
              </div>

              {/* Name */}
              <span className="flex-1 font-semibold text-sm truncate">{player.displayName}</span>

              {/* Score */}
              <span className="font-mono text-lg font-bold tabular-nums">
                {player.totalScore}
                <span className="text-xs font-normal text-muted-foreground ml-1">pts</span>
              </span>
            </div>
          ))}
        </div>

        {/* Actions */}
        <div className="flex gap-3">
          <button
            onClick={handleNewSession}
            className="flex-1 rounded-xl bg-white/10 py-3 text-sm font-medium hover:bg-white/20 transition-colors"
          >
            Nuova partita
          </button>
          <GradientButton className="flex-1" onClick={handleGoToRecords}>
            Vedi dettagli
          </GradientButton>
        </div>
      </div>
    </div>
  );
}
