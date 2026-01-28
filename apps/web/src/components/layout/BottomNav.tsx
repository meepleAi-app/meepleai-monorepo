/**
 * BottomNav - Mobile bottom navigation (Global)
 *
 * Primary mobile navigation with 4 main app sections.
 * Fixed bottom sticky nav, hidden on desktop (≥768px).
 *
 * Design: Playful Boardroom - wireframes-playful-boardroom.md
 * Issue: #1829 [UI-002] BottomNav Component (Mobile-First)
 * Issue: #3104 - Updated to 4 items (Settings moved to top bar)
 *
 * Features:
 * - 4 nav items: Catalogo, I Miei Giochi, Dashboard, Chat
 * - Active state detection (path matching)
 * - Touch-friendly targets (44x44px minimum - WCAG 2.1 AA)
 * - Lucide icons with labels
 * - Orange primary color for active state
 * - Smooth transitions (200ms)
 * - Keyboard navigation support
 */

'use client';

import { Gamepad2, MessageSquare, BookOpen, LayoutDashboard } from 'lucide-react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';

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
    href: '/games',
    icon: Gamepad2,
    label: 'Catalogo',
    ariaLabel: 'Navigate to games catalog',
    testId: 'bottomnav-games',
  },
  {
    href: '/library',
    icon: BookOpen,
    label: 'I Miei Giochi',
    ariaLabel: 'Navigate to your game library',
    testId: 'bottomnav-library',
  },
  {
    href: '/dashboard',
    icon: LayoutDashboard,
    label: 'Dashboard',
    ariaLabel: 'Navigate to dashboard',
    testId: 'bottomnav-dashboard',
  },
  {
    href: '/chat',
    icon: MessageSquare,
    label: 'Chat',
    ariaLabel: 'Navigate to chat interface',
    testId: 'bottomnav-chat',
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
      className={cn(
        'md:hidden fixed bottom-0 left-0 right-0 h-[72px] z-50',
        // Light mode: Glass morphism bottom bar
        'bg-background/95 backdrop-blur-[16px] backdrop-saturate-[180%]',
        // Dark mode: Solid professional
        'dark:bg-card dark:backdrop-blur-none',
        'border-t border-border/50 dark:border-border/30',
        'shadow-lg dark:shadow-2xl dark:shadow-black/20'
      )}
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
                'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary dark:focus-visible:ring-accent focus-visible:ring-offset-2 rounded-md',
                active
                  ? 'text-primary dark:text-primary/90 font-semibold' // Active: Orange
                  : 'text-muted-foreground hover:text-foreground dark:hover:text-foreground/90'
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
