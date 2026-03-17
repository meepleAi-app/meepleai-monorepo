'use client';

import { Plus } from 'lucide-react';
import Link from 'next/link';

import type { GameNightSummary } from '@/store/game-night';

import { GameNightCard } from './GameNightCard';
import { GameNightListSkeleton } from './GameNightListSkeleton';

interface GameNightListProps {
  nights: GameNightSummary[];
  isLoading: boolean;
}

export function GameNightList({ nights, isLoading }: GameNightListProps) {
  if (isLoading) return <GameNightListSkeleton />;

  if (nights.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center py-16 text-center">
        <p className="text-muted-foreground mb-4">Nessuna serata pianificata</p>
        <Link
          href="/game-nights/new"
          className="inline-flex items-center gap-2 px-4 py-2 rounded-lg bg-primary text-primary-foreground font-medium hover:bg-primary/90 transition-colors"
        >
          <Plus className="h-4 w-4" />
          Crea serata
        </Link>
      </div>
    );
  }

  return (
    <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
      {nights.map(night => (
        <GameNightCard key={night.id} night={night} />
      ))}
    </div>
  );
}
