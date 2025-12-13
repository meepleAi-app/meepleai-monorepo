/**
 * BottomNav - Mobile bottom navigation (Global)
 *
 * Primary mobile navigation with 5 main app sections.
 * Fixed bottom sticky nav, hidden on desktop (≥768px).
 *
 * Design: Playful Boardroom - wireframes-playful-boardroom.md
 * Issue: #1829 [UI-002] BottomNav Component (Mobile-First)
 *
 * Features:
 * - 5 nav items: Home, Games, Chat, Settings, Profile
 * - Active state detection (path matching)
 * - Touch-friendly targets (44x44px minimum - WCAG 2.1 AA)
 * - Lucide icons with labels
 * - Orange primary color for active state
 * - Smooth transitions (200ms)
 * - Keyboard navigation support
 */

'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { Home, Gamepad2, MessageSquare, Settings, User } from 'lucide-react';
import { cn } from '@/lib/utils';

interface NavItem {
  href: string;
  icon: React.ComponentType<{ className?: string }>;
  label: string;
  ariaLabel: string;
  testId: string;
}

const navItems: NavItem[] = [
  {
    href: '/dashboard',
    icon: Home,
    label: 'Home',
    ariaLabel: 'Navigate to dashboard home',
    testId: 'bottomnav-home',
  },
  {
    href: '/games',
    icon: Gamepad2,
    label: 'Giochi',
    ariaLabel: 'Navigate to games catalog',
    testId: 'bottomnav-games',
  },
  {
    href: '/chat',
    icon: MessageSquare,
    label: 'Chat',
    ariaLabel: 'Navigate to chat interface',
    testId: 'bottomnav-chat',
  },
  {
    href: '/settings',
    icon: Settings,
    label: 'Config',
    ariaLabel: 'Navigate to settings',
    testId: 'bottomnav-settings',
  },
  {
    href: '/profile',
    icon: User,
    label: 'Profilo',
    ariaLabel: 'Navigate to user profile',
    testId: 'bottomnav-profile',
  },
];

export function BottomNav() {
  const pathname = usePathname();

  const isActive = (href: string) => {
    // Exact match for home, starts-with for other routes
    if (href === '/dashboard') {
      return pathname === '/dashboard' || pathname === '/';
    }
    return pathname?.startsWith(href) ?? false;
  };

  return (
    <nav
      className="md:hidden fixed bottom-0 left-0 right-0 h-[72px] bg-card border-t border-border shadow-lg z-50"
      aria-label="Primary mobile navigation"
    >
      <div className="flex justify-around items-center h-full px-4">
        {navItems.map(({ href, icon: Icon, label, ariaLabel, testId }) => {
          const active = isActive(href);
          return (
            <Link
              key={href}
              href={href}
              aria-label={ariaLabel}
              aria-current={active ? 'page' : undefined}
              data-testid={testId}
              className={cn(
                'flex flex-col items-center justify-center gap-1',
                'min-w-[44px] min-h-[44px]', // Touch target WCAG 2.1 AA
                'transition-colors duration-200',
                'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary focus-visible:ring-offset-2 rounded-md',
                active
                  ? 'text-primary font-semibold' // Active: Orange (#F97316)
                  : 'text-muted-foreground hover:text-foreground'
              )}
            >
              <Icon className="w-6 h-6" aria-hidden="true" />
              <span className="text-[10px] font-[Inter]">{label}</span>
            </Link>
          );
        })}
      </div>
    </nav>
  );
}
