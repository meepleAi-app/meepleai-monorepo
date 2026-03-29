'use client';

import React from 'react';

import { Trophy } from 'lucide-react';
import Link from 'next/link';

import { GradientButton } from '@/components/ui/buttons/GradientButton';
import { GlassCard } from '@/components/ui/surfaces/GlassCard';
import { cn } from '@/lib/utils';

export interface PlayerResult {
  id: string;
  name: string;
  score: number;
}

export interface SessionSummaryCardProps {
  gameName: string;
  players: PlayerResult[];
  durationMinutes: number;
  onPlayAgain?: () => void;
  className?: string;
}

const MEDALS = ['🥇', '🥈', '🥉'];

export function SessionSummaryCard({
  gameName,
  players,
  durationMinutes,
  onPlayAgain,
  className,
}: SessionSummaryCardProps) {
  const sorted = [...players].sort((a, b) => b.score - a.score);

  const hours = Math.floor(durationMinutes / 60);
  const minutes = durationMinutes % 60;
  const durationLabel = hours > 0 ? `${hours}h ${minutes}m` : `${minutes} min`;

  return (
    <GlassCard entity="session" className={cn('flex flex-col items-center gap-6 p-6', className)}>
      {/* Header */}
      <div className="flex flex-col items-center gap-2">
        <Trophy className="h-10 w-10 text-amber-400" aria-hidden="true" />
        <h2 className="text-xl font-bold text-white">Partita Terminata!</h2>
        <p className="text-sm text-[var(--gaming-text-secondary,#aaa)]">{gameName}</p>
      </div>

      {/* Ranked player list */}
      <ol className="w-full space-y-2" aria-label="Classifica giocatori">
        {sorted.map((player, index) => (
          <li
            key={player.id}
            className="flex items-center justify-between rounded-xl bg-white/5 px-4 py-2"
          >
            <div className="flex items-center gap-3">
              <span className="text-xl" aria-label={`Posizione ${index + 1}`}>
                {MEDALS[index] ?? String(index + 1)}
              </span>
              <span className="font-medium text-white">{player.name}</span>
            </div>
            <span className="font-bold tabular-nums text-amber-400">{player.score}</span>
          </li>
        ))}
      </ol>

      {/* Duration */}
      <p className="text-sm text-[var(--gaming-text-secondary,#aaa)]">Durata: {durationLabel}</p>

      {/* Actions */}
      <div className="flex w-full flex-col gap-3">
        {onPlayAgain && (
          <GradientButton fullWidth size="lg" onClick={onPlayAgain}>
            Gioca Ancora
          </GradientButton>
        )}
        <Link
          href="/"
          className="text-center text-sm text-[var(--gaming-text-secondary,#aaa)] underline-offset-4 hover:underline"
        >
          Torna alla Home
        </Link>
      </div>
    </GlassCard>
  );
}
