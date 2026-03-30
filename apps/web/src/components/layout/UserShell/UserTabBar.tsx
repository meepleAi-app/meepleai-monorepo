'use client';

/**
 * UserTabBar — Mobile bottom tab navigation (< lg breakpoint).
 *
 * 5 positions: Home · Libreria · [FAB +] · Chat · Profilo
 * The FAB uses the same contextual action as the desktop TopNav CTA.
 * Falls back to /sessions/new when no section CTA is defined.
 */

import Link from 'next/link';
import { usePathname } from 'next/navigation';

import { getCtaForPathname } from '@/config/contextual-cta';
import { cn } from '@/lib/utils';

interface TabConfig {
  id: string;
  label: string;
  href: string;
  icon: string;
  isActive: (pathname: string) => boolean;
}

const TABS: TabConfig[] = [
  {
    id: 'home',
    label: 'Home',
    href: '/dashboard',
    icon: '🏠',
    isActive: p => p === '/dashboard',
  },
  {
    id: 'library',
    label: 'Libreria',
    href: '/library?tab=collection',
    icon: '📚',
    isActive: p => p.startsWith('/library'),
  },
  {
    id: 'chat',
    label: 'Chat',
    href: '/chat',
    icon: '✨',
    isActive: p => p.startsWith('/chat'),
  },
  {
    id: 'profile',
    label: 'Profilo',
    href: '/profile',
    icon: '👤',
    isActive: p => p.startsWith('/profile') || p.startsWith('/settings'),
  },
];

export function UserTabBar() {
  const pathname = usePathname();
  const cta = getCtaForPathname(pathname);
  const fabHref = cta?.href ?? '/sessions/new';
  const fabGradient = cta?.gradient ?? 'from-emerald-500 to-teal-400';

  const leftTabs = TABS.slice(0, 2); // Home, Libreria
  const rightTabs = TABS.slice(2); // Chat, Profilo

  return (
    <nav
      className={cn(
        'fixed bottom-0 left-0 right-0 z-40',
        'flex items-center justify-around',
        'h-16',
        'bg-card/90 backdrop-blur-md backdrop-saturate-150',
        'border-t border-border/40',
        'lg:hidden'
      )}
      data-testid="user-tab-bar"
      role="navigation"
      aria-label="Main navigation"
    >
      {leftTabs.map(tab => (
        <TabLink key={tab.id} tab={tab} isActive={tab.isActive(pathname)} />
      ))}

      {/* Central FAB — contextual primary action */}
      <Link
        href={fabHref}
        aria-label="Azione principale"
        className={cn(
          'flex items-center justify-center',
          '-mt-4 w-14 h-14 rounded-full',
          'bg-gradient-to-r',
          fabGradient,
          'text-white text-2xl shadow-lg shadow-black/20',
          'hover:opacity-90 transition-opacity'
        )}
      >
        +
      </Link>

      {rightTabs.map(tab => (
        <TabLink key={tab.id} tab={tab} isActive={tab.isActive(pathname)} />
      ))}
    </nav>
  );
}

function TabLink({ tab, isActive }: { tab: TabConfig; isActive: boolean }) {
  return (
    <Link
      href={tab.href}
      aria-selected={isActive}
      aria-label={tab.label}
      className={cn(
        'flex flex-col items-center justify-center gap-0.5',
        'min-w-[44px] min-h-[44px]',
        'transition-colors duration-200',
        isActive ? 'text-foreground' : 'text-muted-foreground'
      )}
    >
      <span
        className={cn(
          'transition-all duration-200 leading-none',
          isActive ? 'text-2xl scale-110 bg-primary/15 rounded-full p-1.5' : 'text-xl'
        )}
        role="img"
        aria-label={tab.label}
      >
        {tab.icon}
      </span>
      <span
        className={cn(
          'text-[10px] font-medium leading-tight',
          isActive ? 'opacity-100' : 'opacity-70'
        )}
      >
        {tab.label}
      </span>
    </Link>
  );
}
