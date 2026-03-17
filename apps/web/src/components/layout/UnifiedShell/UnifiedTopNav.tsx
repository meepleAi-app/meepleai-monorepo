'use client';

import { Search, Bell, Menu } from 'lucide-react';
import Link from 'next/link';

import { ENTITY_NAV_ICONS } from '@/components/ui/data-display/meeple-card-features/navigation-icons';
import { entityColors } from '@/components/ui/data-display/meeple-card-styles';
import { cn } from '@/lib/utils';
import { useCardHand } from '@/stores/use-card-hand';

import { AdminToggle } from './AdminToggle';

interface UnifiedTopNavProps {
  isAdmin: boolean;
  /** Whether the nav is visible (scroll-hide behaviour) */
  isNavVisible?: boolean;
  /** Slot for user menu (passed from shell) */
  userMenu?: React.ReactNode;
  /** Slot for notification bell (passed from shell) */
  notificationBell?: React.ReactNode;
  /** Slot for command palette trigger */
  searchTrigger?: React.ReactNode;
  /** Slot for mini card icons when hand is collapsed (mobile) */
  miniCards?: React.ReactNode;
  /** Callback to open admin mobile drawer */
  onMenuToggle?: () => void;
  /** Whether the admin drawer is open (for aria-expanded) */
  isMenuOpen?: boolean;
}

export function UnifiedTopNav({
  isAdmin,
  isNavVisible = true,
  userMenu,
  notificationBell,
  searchTrigger,
  miniCards,
  onMenuToggle,
  isMenuOpen,
}: UnifiedTopNavProps) {
  const { cards, focusedIdx } = useCardHand();
  const focusedCard = focusedIdx >= 0 && focusedIdx < cards.length ? cards[focusedIdx] : null;

  const Icon = focusedCard ? (ENTITY_NAV_ICONS[focusedCard.entity] ?? ENTITY_NAV_ICONS.game) : null;
  const hsl = focusedCard ? (entityColors[focusedCard.entity]?.hsl ?? '220 70% 50%') : null;

  return (
    <header
      className={cn(
        'sticky top-0 z-40 h-14',
        'flex items-center justify-between px-4',
        'bg-background/95 backdrop-blur-xl',
        'border-b border-border/40',
        'transition-transform duration-300',
        !isNavVisible && '-translate-y-full -mt-14 md:translate-y-0 md:mt-0'
      )}
      data-testid="unified-top-nav"
    >
      {/* Hamburger (mobile admin only) */}
      {isAdmin && onMenuToggle && (
        <button
          type="button"
          className="md:hidden flex items-center justify-center w-9 h-9 rounded-lg text-muted-foreground hover:text-foreground hover:bg-muted/50 transition-colors"
          onClick={onMenuToggle}
          aria-label="Open admin menu"
          aria-expanded={isMenuOpen}
          aria-controls="admin-mobile-drawer"
        >
          <Menu className="w-5 h-5" />
        </button>
      )}

      {/* Left: Logo */}
      <Link href="/" className="flex items-center gap-2 shrink-0">
        <span className="text-lg font-bold font-quicksand">MeepleAI</span>
      </Link>

      {/* Center: Focused card title */}
      <div className="flex items-center gap-2 min-w-0 mx-4">
        {focusedCard && Icon ? (
          <>
            <div
              className="flex items-center justify-center w-6 h-6 rounded shrink-0 bg-[hsl(var(--card-hsl)/0.12)]"
              style={{ '--card-hsl': hsl } as React.CSSProperties}
            >
              <Icon className="w-3.5 h-3.5 text-[hsl(var(--card-hsl))]" />
            </div>
            <span className="text-sm font-medium truncate font-quicksand">{focusedCard.title}</span>
          </>
        ) : (
          <span className="text-sm font-medium text-muted-foreground font-quicksand">MeepleAI</span>
        )}
      </div>

      {/* Mini cards (collapsed hand, mobile) */}
      {miniCards && (
        <div className="flex min-w-0 flex-1 items-center gap-1 overflow-x-auto">{miniCards}</div>
      )}

      {/* Right: Admin toggle + utilities */}
      <div className="flex items-center gap-2 shrink-0">
        {isAdmin && <AdminToggle />}
        {searchTrigger ?? (
          <button
            type="button"
            className="flex items-center justify-center w-9 h-9 rounded-lg text-muted-foreground hover:text-foreground hover:bg-muted/50 transition-colors"
            aria-label="Search"
          >
            <Search className="w-4 h-4" />
          </button>
        )}
        {notificationBell ?? (
          <button
            type="button"
            className="flex items-center justify-center w-9 h-9 rounded-lg text-muted-foreground hover:text-foreground hover:bg-muted/50 transition-colors"
            aria-label="Notifications"
          >
            <Bell className="w-4 h-4" />
          </button>
        )}
        {userMenu}
      </div>
    </header>
  );
}
