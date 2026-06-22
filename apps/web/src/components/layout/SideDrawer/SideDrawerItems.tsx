'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';

import { useNavigationItems } from '@/hooks/useNavigationItems';

// ---------------------------------------------------------------------------
// SideDrawerItems
//
// The mobile ☰ drawer renders the "tutto il resto" overflow: every role-filtered
// destination that is NOT already a fixed bottom-tab (Home/Libreria/Hub/Chat/
// Profilo) and not the bell (Notifiche). Source of truth: useNavigationItems →
// UNIFIED_NAV_ITEMS, so it stays in sync with the rest of the navbar.
// ---------------------------------------------------------------------------

interface SideDrawerItemsProps {
  onNavigate: () => void;
}

export function SideDrawerItems({ onNavigate }: SideDrawerItemsProps) {
  const pathname = usePathname();
  const { mobileDrawerItems, isItemActive } = useNavigationItems();

  return (
    <nav className="flex flex-1 flex-col gap-0.5 overflow-y-auto">
      {mobileDrawerItems.map(item => {
        const Icon = item.icon;
        const isActive = isItemActive(item, pathname);

        return (
          <Link
            key={item.id}
            href={item.href}
            onClick={onNavigate}
            data-testid={item.testId}
            aria-current={isActive ? 'page' : undefined}
            className={[
              'flex items-center gap-3 rounded-lg px-3 py-2.5 text-sm font-medium transition-colors',
              isActive
                ? 'bg-entity-game/10 text-entity-game-text'
                : 'text-foreground/70 hover:bg-muted hover:text-foreground',
            ].join(' ')}
          >
            <Icon className="h-4 w-4 shrink-0" />
            <span>{item.label}</span>
          </Link>
        );
      })}
    </nav>
  );
}
