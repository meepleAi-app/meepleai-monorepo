'use client';

/**
 * UserTabBar — Bottom tab navigation for mobile in the user layout.
 *
 * Features:
 * - Fixed bottom bar with 4 tabs: Home, Libreria, Play, Chat
 * - Entity-colored active states using design token CSS variables
 * - Touch-safe targets (min 44x44)
 * - Glassmorphism styling
 * - Hidden on desktop (lg:hidden)
 * - Pathname-based active state detection
 */

import Link from 'next/link';
import { usePathname, useSearchParams } from 'next/navigation';

import { cn } from '@/lib/utils';

interface TabConfig {
  id: string;
  label: string;
  href: string;
  icon: string;
  /** HSL value from design-tokens.css (no wrapping hsl()) */
  colorVar: string;
  /** Check if tab is active given current pathname and search params */
  isActive: (pathname: string, searchParams: URLSearchParams) => boolean;
}

const TABS: TabConfig[] = [
  {
    id: 'home',
    label: 'Home',
    href: '/dashboard',
    icon: '🏠',
    colorVar: 'hsl(var(--primary))',
    isActive: p => p === '/dashboard',
  },
  {
    id: 'library',
    label: 'Libreria',
    href: '/library?tab=collection',
    icon: '📚',
    colorVar: 'hsl(var(--color-entity-game))',
    isActive: (p, sp) => p === '/library' && sp.has('tab'),
  },
  {
    id: 'play',
    label: 'Gioca',
    href: '/sessions',
    icon: '🎲',
    colorVar: 'hsl(var(--color-entity-session))',
    isActive: p => p.startsWith('/sessions'),
  },
  {
    id: 'chat',
    label: 'Chat',
    href: '/chat',
    icon: '✨',
    colorVar: 'hsl(var(--color-entity-chat))',
    isActive: p => p.startsWith('/chat'),
  },
];

export function UserTabBar() {
  const pathname = usePathname();
  const searchParams = useSearchParams();

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
      role="tablist"
      aria-label="Main navigation"
    >
      {TABS.map(tab => {
        const isActive = tab.isActive(pathname, searchParams);

        return (
          <Link
            key={tab.id}
            href={tab.href}
            role="tab"
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
                'text-[10px] font-medium leading-tight transition-all duration-200',
                isActive ? 'opacity-100' : 'opacity-70 text-xs'
              )}
              style={isActive ? { color: tab.colorVar } : undefined}
            >
              {tab.label}
            </span>
          </Link>
        );
      })}
    </nav>
  );
}
