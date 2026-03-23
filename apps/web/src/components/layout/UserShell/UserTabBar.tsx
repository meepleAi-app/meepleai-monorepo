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
 * - Updates sectionTitle when tab changes
 */

import { BookOpen, Dice5, House, MessageCircle } from 'lucide-react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';

import { cn } from '@/lib/utils';

import type { LucideIcon } from 'lucide-react';

interface TabConfig {
  id: string;
  label: string;
  href: string;
  icon: LucideIcon;
  /** HSL value from design-tokens.css (no wrapping hsl()) */
  colorVar: string;
  activePattern: RegExp;
}

const TABS: TabConfig[] = [
  {
    id: 'home',
    label: 'Home',
    href: '/library',
    icon: House,
    colorVar: 'hsl(var(--primary))',
    activePattern: /^\/library$/,
  },
  {
    id: 'library',
    label: 'Libreria',
    href: '/library?tab=collection',
    icon: BookOpen,
    colorVar: 'hsl(var(--color-entity-game))',
    activePattern: /^\/library\?/,
  },
  {
    id: 'play',
    label: 'Gioca',
    href: '/sessions',
    icon: Dice5,
    colorVar: 'hsl(var(--color-entity-session))',
    activePattern: /^\/sessions/,
  },
  {
    id: 'chat',
    label: 'Chat',
    href: '/chat',
    icon: MessageCircle,
    colorVar: 'hsl(var(--color-entity-chat))',
    activePattern: /^\/chat/,
  },
];

export function UserTabBar() {
  const pathname = usePathname();

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
        const isActive =
          tab.activePattern.test(pathname) || (tab.id === 'home' && pathname === '/library');
        const Icon = tab.icon;

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
            <Icon
              className={cn('transition-all duration-200', isActive ? 'w-6 h-6' : 'w-5 h-5')}
              style={isActive ? { color: tab.colorVar } : undefined}
              fill={isActive ? 'currentColor' : 'none'}
              strokeWidth={isActive ? 2 : 1.5}
            />
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
