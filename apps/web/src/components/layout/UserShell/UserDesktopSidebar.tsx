'use client';

/**
 * UserDesktopSidebar — Desktop sidebar navigation for the user layout.
 *
 * Features:
 * - Hidden on mobile, visible on desktop (lg:flex)
 * - Collapsed (56px) -> expands to 180px on hover
 * - 4 icon buttons matching UserTabBar tabs and entity colors
 * - Active tab: left border accent + entity-colored icon
 */

import { BookOpen, Dice5, House, MessageCircle } from 'lucide-react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';

import { cn } from '@/lib/utils';

import type { LucideIcon } from 'lucide-react';

interface SidebarTabConfig {
  id: string;
  label: string;
  href: string;
  icon: LucideIcon;
  /** CSS color value for active state */
  colorVar: string;
  activePattern: RegExp;
}

const SIDEBAR_TABS: SidebarTabConfig[] = [
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

export function UserDesktopSidebar() {
  const pathname = usePathname();

  return (
    <aside
      className={cn(
        'hidden lg:flex flex-col',
        'w-14 hover:w-[180px]',
        'transition-[width] duration-300 ease-in-out',
        'border-r border-border/40 bg-background',
        'group overflow-hidden'
      )}
      data-testid="user-desktop-sidebar"
    >
      {/* Main navigation */}
      <nav className="flex-1 flex flex-col gap-1 py-4" aria-label="Desktop navigation">
        {SIDEBAR_TABS.map(tab => {
          const isActive =
            tab.activePattern.test(pathname) || (tab.id === 'home' && pathname === '/library');
          const Icon = tab.icon;

          return (
            <Link
              key={tab.id}
              href={tab.href}
              className={cn(
                'relative flex items-center gap-3 px-4 py-3',
                'transition-colors duration-200',
                'hover:bg-muted/50',
                isActive ? 'text-foreground' : 'text-muted-foreground hover:text-foreground'
              )}
              aria-label={tab.label}
              aria-current={isActive ? 'page' : undefined}
            >
              {/* Active indicator — left border */}
              {isActive && (
                <div
                  className="absolute left-0 top-1/2 -translate-y-1/2 w-0.5 h-6 rounded-r-full"
                  style={{ backgroundColor: tab.colorVar }}
                />
              )}

              <Icon
                className="w-5 h-5 shrink-0"
                style={isActive ? { color: tab.colorVar } : undefined}
              />

              <span
                className={cn(
                  'text-sm font-medium whitespace-nowrap',
                  'opacity-0 group-hover:opacity-100',
                  'transition-opacity duration-300'
                )}
              >
                {tab.label}
              </span>
            </Link>
          );
        })}
      </nav>
    </aside>
  );
}
