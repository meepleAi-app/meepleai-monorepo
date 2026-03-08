'use client';

/**
 * AdminMobileTabBar — Admin-specific bottom tab bar for mobile
 * Mobile UX Epic — Issue 17
 *
 * Persistent bottom navigation on mobile for admin dashboard.
 * 5 tabs: Overview, Users, Games, AI, Knowledge Base
 *
 * Design:
 * - md:hidden (mobile only)
 * - Fixed bottom, z-40
 * - Glassmorphism: bg-card/90 backdrop-blur-md
 * - Active: amber accent + filled icon
 * - Touch targets: 44x44px minimum
 */

import { BookOpenIcon, BotIcon, LayoutDashboardIcon, ShareIcon, UsersIcon } from 'lucide-react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';

import { cn } from '@/lib/utils';

// ─── Tab Configuration ───────────────────────────────────────────────────────

interface AdminTab {
  id: string;
  label: string;
  href: string;
  icon: typeof LayoutDashboardIcon;
  /** Prefix match for active state */
  activePrefix: string;
}

const ADMIN_TABS: AdminTab[] = [
  {
    id: 'overview',
    label: 'Overview',
    href: '/admin/overview',
    icon: LayoutDashboardIcon,
    activePrefix: '/admin/overview',
  },
  {
    id: 'users',
    label: 'Utenti',
    href: '/admin/users',
    icon: UsersIcon,
    activePrefix: '/admin/users',
  },
  {
    id: 'games',
    label: 'Giochi',
    href: '/admin/shared-games/all',
    icon: ShareIcon,
    activePrefix: '/admin/shared-games',
  },
  {
    id: 'ai',
    label: 'AI',
    href: '/admin/agents',
    icon: BotIcon,
    activePrefix: '/admin/agents',
  },
  {
    id: 'kb',
    label: 'KB',
    href: '/admin/knowledge-base',
    icon: BookOpenIcon,
    activePrefix: '/admin/knowledge-base',
  },
];

// ─── Component ───────────────────────────────────────────────────────────────

export function AdminMobileTabBar() {
  const pathname = usePathname();

  return (
    <nav
      data-testid="admin-mobile-tab-bar"
      className={cn(
        // Mobile only
        'md:hidden',
        // Fixed bottom
        'fixed bottom-0 left-0 right-0 z-40',
        // Height + safe area
        'h-[72px] pb-[env(safe-area-inset-bottom)]',
        // Glassmorphism
        'bg-card/90 backdrop-blur-md backdrop-saturate-150',
        'border-t border-border/50',
        // Layout
        'flex items-center justify-around px-1'
      )}
      aria-label="Admin navigation"
    >
      {ADMIN_TABS.map(tab => {
        const isActive = pathname === tab.href || pathname.startsWith(tab.activePrefix + '/');
        const Icon = tab.icon;

        return (
          <Link
            key={tab.id}
            href={tab.href}
            data-testid={`admin-tab-${tab.id}`}
            aria-current={isActive ? 'page' : undefined}
            className={cn(
              // Touch target
              'flex flex-col items-center justify-center',
              'min-w-[44px] min-h-[44px] py-1 px-2',
              'rounded-xl',
              'transition-colors duration-150',
              // Active state
              isActive
                ? 'text-amber-700 dark:text-amber-400'
                : 'text-muted-foreground hover:text-foreground'
            )}
          >
            <Icon className={cn('h-5 w-5 mb-0.5', isActive && 'stroke-[2.5]')} aria-hidden="true" />
            <span
              className={cn(
                'text-[10px] font-nunito leading-tight',
                isActive ? 'font-bold' : 'font-medium'
              )}
            >
              {tab.label}
            </span>
          </Link>
        );
      })}
    </nav>
  );
}
