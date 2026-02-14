/**
 * BottomNav - Mobile bottom navigation (Global)
 *
 * @deprecated Issue #3479 - Use UnifiedActionBar instead.
 * This component is deprecated and will be removed in a future release.
 * For authenticated pages, use AuthenticatedLayout which includes UnifiedActionBar.
 *
 * Primary mobile navigation with role-based visibility.
 * Fixed bottom sticky nav, hidden on desktop (≥768px).
 *
 * Design: Playful Boardroom - wireframes-playful-boardroom.md
 * Issue: #1829 [UI-002] BottomNav Component (Mobile-First)
 * Issue: #3104 - Updated to 4 items (Settings moved to top bar)
 * Issue: #2860 - Updated to 5 items (Home, Library, Catalog, Chat, Profile)
 *
 * Visibility rules:
 * - Anonymous users: Home, Catalogo
 * - Authenticated users: Home, Library, Catalogo, Chat, Profilo
 *
 * Features:
 * - Role-based nav items visibility
 * - Active state detection (path matching)
 * - Touch-friendly targets (44x44px minimum - WCAG 2.1 AA)
 * - Lucide icons with labels
 * - Purple (#8b5cf6) active state, Orange (#d2691e) hover
 * - Smooth transitions (200ms)
 * - Keyboard navigation support
 */

'use client';

import { Home, Gamepad2, MessageSquare, BookOpen, User } from 'lucide-react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';

import { useCurrentUser } from '@/hooks/queries/useCurrentUser';
import { cn } from '@/lib/utils';

interface NavItem {
  href: string;
  icon: React.ComponentType<{ className?: string }>;
  label: string;
  ariaLabel: string;
  testId: string;
  /** Only show for authenticated users */
  authOnly?: boolean;
}

/**
 * Navigation items for mobile bottom nav
 *
 * Visibility rules:
 * - authOnly: only visible when logged in
 * - no flag: visible to everyone
 *
 * Anonymous users see: Home, Catalogo
 * Authenticated users see: Home, Libreria, Catalogo, Chat, Profilo
 */
const navItems: NavItem[] = [
  {
    href: '/',
    icon: Home,
    label: 'Home',
    ariaLabel: 'Navigate to home page',
    testId: 'bottomnav-home',
  },
  {
    href: '/library',
    icon: BookOpen,
    label: 'Libreria',
    ariaLabel: 'Navigate to your game library',
    testId: 'bottomnav-library',
    authOnly: true,
  },
  {
    href: '/games',
    icon: Gamepad2,
    label: 'Catalogo',
    ariaLabel: 'Navigate to games catalog',
    testId: 'bottomnav-games',
  },
  {
    href: '/chat/new',
    icon: MessageSquare,
    label: 'Chat',
    ariaLabel: 'Navigate to chat interface',
    testId: 'bottomnav-chat',
    authOnly: true,
  },
  {
    href: '/profile',
    icon: User,
    label: 'Profilo',
    ariaLabel: 'Navigate to your profile',
    testId: 'bottomnav-profile',
    authOnly: true,
  },
];

export function BottomNav() {
  const pathname = usePathname();
  const { data: user } = useCurrentUser();

  const isAuthenticated = !!user;

  // Filter nav items based on authentication status
  const visibleNavItems = navItems.filter(item => {
    if (item.authOnly && !isAuthenticated) return false;
    return true;
  });

  const isActive = (href: string) => {
    // Exact match for home and profile, starts-with for other routes
    if (href === '/') {
      return pathname === '/';
    }
    if (href === '/profile') {
      return pathname === '/profile' || pathname?.startsWith('/profile/');
    }
    // Chat routes: /chat/new, /chat/{threadId}, etc.
    if (href === '/chat/new') {
      return pathname?.startsWith('/chat') ?? false;
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
        {visibleNavItems.map(({ href, icon: Icon, label, ariaLabel, testId }) => {
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
                'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[hsl(262_83%_62%)] focus-visible:ring-offset-2 rounded-md',
                active
                  ? 'text-[hsl(262_83%_62%)] font-semibold' // Active: Purple (#8b5cf6)
                  : 'text-muted-foreground hover:text-primary' // Hover: Orange (#d2691e)
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
