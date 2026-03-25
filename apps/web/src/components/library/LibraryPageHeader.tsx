'use client';

import { Plus } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';

interface LibraryPageHeaderProps {
  gameCount: number;
  onAddGame: () => void;
  className?: string;
}

export function LibraryPageHeader({ gameCount, onAddGame, className }: LibraryPageHeaderProps) {
  return (
    <div className={cn('flex items-center justify-between', className)}>
      <div className="flex items-baseline gap-3">
        <h1 className="font-quicksand text-2xl font-bold text-foreground">I Miei Giochi</h1>
        <span className="rounded-full bg-muted px-2.5 py-0.5 text-sm font-medium text-muted-foreground">
          {gameCount} giochi
        </span>
      </div>

      {/* Desktop: full CTA button */}
      <Button
        onClick={onAddGame}
        className="hidden sm:inline-flex gap-1.5"
        aria-label="Aggiungi un gioco alla libreria"
      >
        <Plus className="h-4 w-4" />
        Aggiungi Gioco
      </Button>

      {/* Mobile: FAB circular */}
      <Button
        onClick={onAddGame}
        size="icon"
        className="sm:hidden h-9 w-9 rounded-full shadow-warm-md"
        aria-label="Aggiungi un gioco alla libreria"
      >
        <Plus className="h-5 w-5" />
      </Button>
    </div>
  );
}
