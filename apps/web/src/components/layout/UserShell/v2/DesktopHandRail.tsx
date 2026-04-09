'use client';

import { usePathname } from 'next/navigation';

import { cn } from '@/lib/utils';
import { useCardHand } from '@/stores/use-card-hand';

import { HandRailItem } from './HandRailItem';
import { HandRailToolbar } from './HandRailToolbar';

/**
 * DesktopHandRail — persistent left rail reading from useCardHand store.
 * Replaces the hardcoded NAV_ITEMS behavior of the legacy CardRack.
 *
 * Active state is computed from the current pathname: the card whose
 * href matches the pathname (exact or prefix for game/session ids) is active.
 */
export function DesktopHandRail() {
  const cards = useCardHand(s => s.cards);
  const pinnedIds = useCardHand(s => s.pinnedIds);
  const pinCard = useCardHand(s => s.pinCard);
  const unpinCard = useCardHand(s => s.unpinCard);
  const isExpanded = useCardHand(s => s.expandedStack);
  const toggleExpand = useCardHand(s => s.toggleExpandStack);
  const pathname = usePathname() ?? '';

  const isCardActive = (href: string) => {
    if (href === '/') return pathname === '/';
    return pathname === href || pathname.startsWith(href + '/');
  };

  const activeCard = cards.find(c => isCardActive(c.href));
  const isActivePinned = activeCard ? pinnedIds.has(activeCard.id) : false;

  const handleTogglePin = () => {
    if (!activeCard) return;
    if (isActivePinned) unpinCard(activeCard.id);
    else pinCard(activeCard.id);
  };

  return (
    <aside
      data-testid="desktop-hand-rail"
      aria-label="Cards in hand"
      data-expanded={isExpanded}
      className={cn(
        'hidden md:flex flex-col shrink-0 border-r border-[var(--nh-border-default)] py-3.5 pb-3 gap-2 transition-[width] duration-200 ease-out',
        isExpanded ? 'w-[var(--card-rack-hover-width,240px)]' : 'w-[var(--card-rack-width,76px)]'
      )}
      style={{
        background: 'linear-gradient(180deg, rgba(255,252,248,0.6), rgba(255,252,248,0.2))',
      }}
    >
      <span
        className="text-[9px] font-extrabold font-quicksand uppercase tracking-[0.12em] text-[var(--nh-text-muted)] pb-2 self-center"
        style={{ writingMode: 'vertical-rl', transform: 'rotate(180deg)' }}
      >
        La tua mano
      </span>
      <div className="flex-1 flex flex-col items-center gap-2.5 w-full overflow-y-auto pt-1 px-1">
        {cards.map(card => (
          <HandRailItem key={card.id} card={card} isActive={isCardActive(card.href)} />
        ))}
      </div>
      <HandRailToolbar
        onTogglePin={handleTogglePin}
        onToggleExpand={toggleExpand}
        isPinned={isActivePinned}
        isExpanded={isExpanded}
      />
    </aside>
  );
}
