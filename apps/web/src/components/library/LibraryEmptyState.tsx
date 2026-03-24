/**
 * LibraryEmptyState — Gaming immersive empty-state for private library.
 * Animated floating icons, quick-start cards, and trending games row.
 * Layout Redesign: replaces original plain empty state (in-place update).
 */

'use client';

import { cn } from '@/lib/utils';

import { TrendingGamesRow } from './TrendingGamesRow';

interface LibraryEmptyStateProps {
  onExploreCatalog?: () => void;
  onImportBgg?: () => void;
  onCreateCustom?: () => void;
  className?: string;
}

const FLOATING_ICONS = [
  { emoji: '🎲', className: 'left-[20%] top-[10%] text-[40px]', delay: '0s' },
  { emoji: '♟️', className: 'left-[55%] top-[5%] text-[48px]', delay: '0.8s' },
  { emoji: '🃏', className: 'left-[75%] top-[25%] text-[40px]', delay: '1.5s' },
  { emoji: '🧩', className: 'left-[10%] top-[50%] text-[32px]', delay: '2.2s' },
  { emoji: '🎯', className: 'left-[45%] top-[55%] text-[40px]', delay: '0.5s' },
  { emoji: '🏰', className: 'left-[80%] top-[55%] text-[36px]', delay: '1.8s' },
];

const QUICK_START_CARDS = [
  {
    icon: '🔍',
    title: 'Esplora il Catalogo',
    description: 'Cerca tra migliaia di giochi e aggiungili alla tua libreria',
    borderColor: 'border-t-primary',
    action: 'catalog' as const,
  },
  {
    icon: '📤',
    title: 'Importa da BGG',
    description: 'Importa la tua collezione da BoardGameGeek',
    borderColor: 'border-t-blue-500',
    action: 'bgg' as const,
  },
  {
    icon: '✏️',
    title: 'Crea Gioco Custom',
    description: 'Aggiungi un gioco non presente nel catalogo',
    borderColor: 'border-t-green-500',
    action: 'custom' as const,
  },
];

export function LibraryEmptyState({
  onExploreCatalog,
  onImportBgg,
  onCreateCustom,
  className,
}: LibraryEmptyStateProps) {
  const handleAction = (action: 'catalog' | 'bgg' | 'custom') => {
    if (action === 'catalog') onExploreCatalog?.();
    else if (action === 'bgg') onImportBgg?.();
    else onCreateCustom?.();
  };

  return (
    <div
      className={cn('flex flex-col items-center text-center', className)}
      data-testid="empty-state"
    >
      {/* Animated illustration */}
      <div className="relative mb-8 h-[180px] w-[280px]" aria-hidden="true">
        {FLOATING_ICONS.map((icon, i) => (
          <span
            key={i}
            className={cn('absolute animate-float drop-shadow-lg', icon.className)}
            style={{ animationDelay: icon.delay }}
          >
            {icon.emoji}
          </span>
        ))}
        {/* Central glow ring */}
        <div className="absolute left-1/2 top-1/2 flex h-[100px] w-[100px] -translate-x-1/2 -translate-y-1/2 items-center justify-center rounded-full border-2 border-primary/20 bg-[radial-gradient(circle,hsla(25,95%,60%,0.15)_0%,transparent_70%)] animate-glow-pulse">
          <span className="text-[40px]">📚</span>
        </div>
      </div>

      {/* Title + subtitle */}
      <h2 className="mb-2 bg-gradient-to-r from-foreground to-primary bg-clip-text font-quicksand text-2xl font-extrabold text-transparent sm:text-[26px]">
        La tua collezione ti aspetta
      </h2>
      <p className="mb-7 max-w-[420px] text-sm leading-relaxed text-muted-foreground sm:text-[15px]">
        Aggiungi i tuoi giochi da tavolo preferiti, carica i manuali PDF e lascia che l&apos;AI ti
        aiuti a padroneggiare ogni regola.
      </p>

      {/* Quick-start cards */}
      <div className="mb-7 grid w-full max-w-[700px] grid-cols-1 gap-3.5 sm:grid-cols-3">
        {QUICK_START_CARDS.map(card => (
          <button
            key={card.action}
            onClick={() => handleAction(card.action)}
            className={cn(
              'flex flex-col items-center rounded-xl border border-border border-t-[3px] bg-card px-4 py-5 text-center',
              'transition-all duration-300 hover:-translate-y-1 hover:border-primary hover:shadow-warm-lg',
              card.borderColor
            )}
          >
            <span className="mb-2.5 text-[32px]">{card.icon}</span>
            <span className="mb-1 font-quicksand text-sm font-bold">{card.title}</span>
            <span className="text-[11px] leading-snug text-muted-foreground">
              {card.description}
            </span>
          </button>
        ))}
      </div>

      {/* Trending games */}
      <TrendingGamesRow className="max-w-[700px]" />
    </div>
  );
}
