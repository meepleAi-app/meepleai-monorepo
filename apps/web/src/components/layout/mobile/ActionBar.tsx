'use client';

import { BarChart3, ChevronLeft, MessageCircle, MoreHorizontal, Wrench } from 'lucide-react';
import { usePathname, useRouter } from 'next/navigation';

import { useDashboardMode } from '@/components/dashboard';
import { entityHsl, entityIcon } from '@/components/ui/data-display/meeple-card/tokens';
import { useCardHand } from '@/lib/stores/card-hand-store';
import { useCascadeNavigationStore } from '@/lib/stores/cascade-navigation-store';
import { cn } from '@/lib/utils';
import { resolveCTA } from '@/lib/utils/nav-cta';

export function ActionBar() {
  const { isGameMode, activeSessionId } = useDashboardMode();
  const inSession = isGameMode && !!activeSessionId;

  if (inSession) {
    return <SessionActionBar sessionId={activeSessionId!} />;
  }

  return <NormalActionBar />;
}

function NormalActionBar() {
  const pathname = usePathname();
  const cards = useCardHand(s => s.cards);
  // Pinned first, then non-pinned in store order (no extra sort — preserves insertion order)
  const pinned = cards.filter(c => c.pinned);
  const recent = cards.filter(c => !c.pinned);
  const allCards = [...pinned, ...recent];
  const visibleChips = allCards.slice(0, 3);
  const overflowCount = Math.max(0, allCards.length - 3);

  const openDrawer = useCascadeNavigationStore(s => s.openDrawer);
  const cta = resolveCTA(pathname);

  const handleOverflow = () => {
    const target = allCards[3];
    if (target) openDrawer(target.entityType, target.entityId);
  };

  return (
    <nav
      role="toolbar"
      data-testid="action-bar"
      className={cn(
        'fixed bottom-0 left-0 right-0 z-30 md:hidden',
        'flex items-center justify-between gap-3',
        'px-3 pt-2 pb-[calc(env(safe-area-inset-bottom)+8px)]',
        'bg-[rgba(8,12,24,0.92)] backdrop-blur-[16px]',
        'border-t border-white/8'
      )}
    >
      {/* Mini hand */}
      <div className="flex items-center">
        {visibleChips.map((card, i) => (
          <button
            key={card.id}
            type="button"
            aria-label={card.label}
            onClick={() => openDrawer(card.entityType, card.entityId)}
            data-testid={`action-bar-chip-${card.id}`}
            className="flex items-center justify-center rounded transition-transform active:scale-95"
            style={{
              width: 28,
              height: 20,
              background: entityHsl(card.entityType, 0.22),
              border: `1px solid ${entityHsl(card.entityType, 0.55)}`,
              marginLeft: i > 0 ? -6 : 0,
              zIndex: 3 - i,
              position: 'relative',
            }}
          >
            <span className="text-[10px] leading-none">{entityIcon[card.entityType]}</span>
          </button>
        ))}
        {overflowCount > 0 && (
          <button
            type="button"
            onClick={handleOverflow}
            data-testid="action-bar-overflow"
            className="ml-2 text-[10px] font-bold text-white/65 hover:text-white transition-colors"
          >
            +{overflowCount} ›
          </button>
        )}
      </div>

      {/* CTA */}
      {cta ? (
        <a
          href={cta.href}
          data-testid="action-bar-cta"
          className={cn(
            'px-3 py-1.5 rounded-full',
            'text-[11px] font-bold text-white',
            'bg-[hsl(25,95%,45%)] hover:bg-[hsl(25,95%,40%)]',
            'shadow-[0_2px_8px_hsla(25,95%,45%,0.35)] transition-colors'
          )}
        >
          {cta.label}
        </a>
      ) : (
        <div className="w-10" />
      )}
    </nav>
  );
}

function SessionActionBar({ sessionId }: { sessionId: string }) {
  const router = useRouter();
  const openDrawer = useCascadeNavigationStore(s => s.openDrawer);

  return (
    <nav
      data-testid="action-bar-session"
      className={cn(
        'fixed bottom-0 left-0 right-0 z-30 md:hidden',
        'flex items-center justify-around',
        'px-2 pt-1.5 pb-[calc(env(safe-area-inset-bottom)+4px)]',
        'bg-[rgba(8,12,24,0.92)] backdrop-blur-[16px]',
        'border-t-2 border-indigo-400/60'
      )}
    >
      <button
        type="button"
        onClick={() => router.back()}
        className="flex min-w-[48px] flex-col items-center gap-0.5 py-1 text-[10px] font-bold text-muted-foreground"
      >
        <ChevronLeft className="h-5 w-5" />
        <span>Back</span>
      </button>
      <button
        type="button"
        onClick={() => openDrawer('session', sessionId, 'live')}
        className="flex min-w-[48px] flex-col items-center gap-0.5 py-1 text-[10px] font-bold text-indigo-400"
      >
        <BarChart3 className="h-5 w-5" />
        <span>Classifica</span>
      </button>
      <button
        type="button"
        onClick={() => openDrawer('session', sessionId, 'toolkit')}
        className="flex min-w-[48px] flex-col items-center gap-0.5 py-1 text-[10px] font-bold text-muted-foreground"
      >
        <Wrench className="h-5 w-5" />
        <span>Toolkit</span>
      </button>
      <button
        type="button"
        disabled
        aria-label="Chat AI (prossimamente)"
        className="flex min-w-[48px] flex-col items-center gap-0.5 py-1 text-[10px] font-bold opacity-40 cursor-not-allowed"
      >
        <MessageCircle className="h-5 w-5" />
        <span>AI</span>
      </button>
      <button
        type="button"
        className="flex min-w-[48px] flex-col items-center gap-0.5 py-1 text-[10px] font-bold text-muted-foreground"
      >
        <MoreHorizontal className="h-5 w-5" />
        <span>Altro</span>
      </button>
    </nav>
  );
}
