/**
 * CardStackPanel - "Carte in Mano" navigation panel
 *
 * Right-side panel (280px) with a floating toggle button that shows
 * the user's entity navigation trail as mini-cards with preview.
 *
 * Features:
 * - FAB toggle in bottom-right corner with badge count
 * - Entity-coloured mini-cards with icon and title
 * - Click a card to navigate back (truncates trail)
 * - Responsive: Sheet on mobile, fixed panel on desktop
 * - Uses existing useNavigationTrail hook + sessionStorage
 *
 * @see Epic #4688 - MeepleCard Navigation System
 */

'use client';

import { useCallback, useState } from 'react';

import { Layers, X, ChevronRight, Trash2 } from 'lucide-react';
import Link from 'next/link';

import type { MeepleEntityType } from '@/components/ui/data-display/meeple-card';
import { ENTITY_NAV_ICONS } from '@/components/ui/data-display/meeple-card-features/navigation-icons';
import type { BreadcrumbStep } from '@/hooks/use-navigation-trail';
import { useNavigationTrail } from '@/hooks/use-navigation-trail';
import { cn } from '@/lib/utils';

// ---------------------------------------------------------------------------
// Entity colours (matching breadcrumb-trail.tsx)
// ---------------------------------------------------------------------------

const ENTITY_HSL: Record<MeepleEntityType, string> = {
  game: '25 95% 45%',
  player: '262 83% 58%',
  session: '240 60% 55%',
  agent: '38 92% 50%',
  kb: '174 60% 40%',
  chatSession: '220 80% 55%',
  event: '350 89% 60%',
  toolkit: '142 70% 45%',
  tool: '190 65% 45%',
  custom: '220 70% 50%',
};

const ENTITY_LABELS: Record<MeepleEntityType, string> = {
  game: 'Game',
  player: 'Player',
  session: 'Session',
  agent: 'Agent',
  kb: 'KB',
  chatSession: 'Chat',
  event: 'Event',
  toolkit: 'Toolkit',
  tool: 'Tool',
  custom: 'Item',
};

// ---------------------------------------------------------------------------
// Card item
// ---------------------------------------------------------------------------

function CardStackItem({
  step,
  index,
  isLast,
  onNavigate,
}: {
  step: BreadcrumbStep;
  index: number;
  isLast: boolean;
  onNavigate: (index: number) => void;
}) {
  const Icon = ENTITY_NAV_ICONS[step.entity] ?? ENTITY_NAV_ICONS.game;
  const hsl = ENTITY_HSL[step.entity] ?? ENTITY_HSL.custom;
  const typeLabel = ENTITY_LABELS[step.entity] ?? 'Item';

  const handleClick = useCallback(
    (e: React.MouseEvent) => {
      if (!isLast) {
        e.preventDefault();
        onNavigate(index);
      }
    },
    [index, isLast, onNavigate]
  );

  return (
    <Link
      href={step.href}
      onClick={handleClick}
      className={cn(
        'group flex items-center gap-3 px-3 py-2.5 rounded-lg',
        'transition-all duration-200 no-underline',
        'border',
        isLast
          ? 'bg-[hsl(var(--card-hsl)/0.1)] border-[hsl(var(--card-hsl)/0.25)] shadow-sm'
          : 'bg-card/50 border-border/30 hover:bg-[hsl(var(--card-hsl)/0.06)] hover:border-[hsl(var(--card-hsl)/0.2)]'
      )}
      style={{ '--card-hsl': hsl } as React.CSSProperties}
    >
      {/* Entity icon */}
      <div
        className={cn(
          'flex items-center justify-center w-8 h-8 rounded-md shrink-0',
          'bg-[hsl(var(--card-hsl)/0.12)]'
        )}
      >
        <Icon className="w-4 h-4 text-[hsl(var(--card-hsl))]" />
      </div>

      {/* Label + type */}
      <div className="flex-1 min-w-0">
        <p
          className={cn(
            'text-sm font-medium truncate',
            isLast ? 'text-[hsl(var(--card-hsl))]' : 'text-foreground'
          )}
        >
          {step.label}
        </p>
        <p className="text-[10px] uppercase tracking-wider text-muted-foreground font-nunito">
          {typeLabel}
        </p>
      </div>

      {/* Arrow indicator */}
      {isLast && <ChevronRight className="w-3.5 h-3.5 text-[hsl(var(--card-hsl)/0.5)] shrink-0" />}

      {/* Step number */}
      <span className="text-[10px] text-muted-foreground/50 tabular-nums shrink-0 font-mono">
        {index + 1}
      </span>
    </Link>
  );
}

// ---------------------------------------------------------------------------
// Main panel
// ---------------------------------------------------------------------------

export function CardStackPanel() {
  const { trail, navigateTo, clear } = useNavigationTrail();
  const [isOpen, setIsOpen] = useState(false);

  const count = trail.length;

  if (count === 0) return null;

  return (
    <>
      {/* FAB Toggle Button */}
      <button
        onClick={() => setIsOpen(prev => !prev)}
        className={cn(
          'fixed z-40',
          'bottom-20 md:bottom-6 right-4',
          'flex items-center justify-center',
          'w-12 h-12 rounded-full',
          'bg-primary text-primary-foreground',
          'shadow-lg hover:shadow-xl',
          'transition-all duration-200',
          'hover:scale-105 active:scale-95',
          isOpen && 'rotate-45'
        )}
        title={isOpen ? 'Chiudi carte' : `${count} carte in mano`}
        aria-label={isOpen ? 'Close card stack panel' : `Open card stack panel (${count} cards)`}
      >
        {isOpen ? (
          <X className="w-5 h-5" />
        ) : (
          <div className="relative">
            <Layers className="w-5 h-5" />
            {/* Badge count */}
            <span
              className={cn(
                'absolute -top-2 -right-2',
                'flex items-center justify-center',
                'min-w-[18px] h-[18px] px-1',
                'text-[10px] font-bold',
                'bg-destructive text-destructive-foreground',
                'rounded-full'
              )}
            >
              {count}
            </span>
          </div>
        )}
      </button>

      {/* Backdrop (mobile) */}
      {isOpen && (
        <div
          className="fixed inset-0 z-40 bg-black/20 backdrop-blur-sm md:hidden"
          onClick={() => setIsOpen(false)}
          aria-hidden="true"
        />
      )}

      {/* Panel */}
      <div
        className={cn(
          'fixed z-40',
          // Desktop: right side panel
          'md:top-0 md:right-0 md:h-full md:w-[280px]',
          'md:border-l md:border-border/40',
          // Mobile: sheet from bottom
          'max-md:bottom-0 max-md:left-0 max-md:right-0 max-md:max-h-[70vh]',
          'max-md:rounded-t-2xl max-md:border-t max-md:border-border/40',
          // Shared
          'bg-background/95 backdrop-blur-xl',
          'shadow-2xl',
          'flex flex-col',
          'transition-transform duration-300 ease-out',
          isOpen
            ? 'translate-x-0 max-md:translate-y-0'
            : 'md:translate-x-full max-md:translate-y-full'
        )}
      >
        {/* Header */}
        <div className="flex items-center justify-between px-4 py-3 border-b border-border/30">
          <div className="flex items-center gap-2">
            <Layers className="w-4 h-4 text-primary" />
            <h3 className="text-sm font-semibold font-quicksand text-foreground">Carte in Mano</h3>
            <span className="text-xs text-muted-foreground bg-muted/50 px-1.5 py-0.5 rounded-full">
              {count}
            </span>
          </div>
          <div className="flex items-center gap-1">
            <button
              onClick={clear}
              className="flex items-center justify-center w-7 h-7 rounded-md text-muted-foreground hover:text-destructive hover:bg-destructive/10 transition-colors"
              title="Svuota tutte le carte"
              aria-label="Clear all cards"
            >
              <Trash2 className="w-3.5 h-3.5" />
            </button>
            <button
              onClick={() => setIsOpen(false)}
              className="flex items-center justify-center w-7 h-7 rounded-md text-muted-foreground hover:text-foreground hover:bg-muted/50 transition-colors md:hidden"
              aria-label="Close panel"
            >
              <X className="w-4 h-4" />
            </button>
          </div>
        </div>

        {/* Card list */}
        <div className="flex-1 overflow-y-auto px-3 py-3 space-y-2">
          {trail.map((step, i) => (
            <CardStackItem
              key={`${step.href}-${i}`}
              step={step}
              index={i}
              isLast={i === trail.length - 1}
              onNavigate={navigateTo}
            />
          ))}
        </div>

        {/* Footer hint */}
        <div className="px-4 py-2 border-t border-border/20">
          <p className="text-[10px] text-muted-foreground/60 text-center font-nunito">
            Naviga tra le entit&agrave; cliccando i link nelle card
          </p>
        </div>
      </div>
    </>
  );
}
