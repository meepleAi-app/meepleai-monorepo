/**
 * MobileTabBar - Persistent bottom tab bar for mobile navigation
 *
 * Issue #1 from mobile-first-ux-epic.md
 *
 * Replaces deprecated BottomNav with a glassmorphism design that
 * coexists with FloatingActionBar (contextual actions above).
 *
 * 5 tabs: Dashboard, Library, Discover, Chat, Profile
 * Auth-gated: Guest sees Dashboard + Discover only
 * Touch targets: 44x44px minimum (WCAG 2.1 AA)
 */

'use client';

import { Gamepad2, LayoutDashboard, LibraryBig, MessageSquare, User } from 'lucide-react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';

import { useCurrentUser } from '@/hooks/queries/useCurrentUser';
import { cn } from '@/lib/utils';

// ─── Types ───────────────────────────────────────────────────────────────────

interface TabItem {
  href: string;
  icon: React.ComponentType<{ className?: string }>;
  label: string;
  ariaLabel: string;
  testId: string;
  /** Only show for authenticated users */
  authOnly?: boolean;
}

// ─── Tab Configuration ───────────────────────────────────────────────────────

const TAB_ITEMS: TabItem[] = [
  {
    href: '/dashboard',
    icon: LayoutDashboard,
    label: 'Dashboard',
    ariaLabel: 'Navigate to dashboard',
    testId: 'mobile-tab-dashboard',
  },
  {
    href: '/library',
    icon: LibraryBig,
    label: 'Library',
    ariaLabel: 'Navigate to your game library',
    testId: 'mobile-tab-library',
    authOnly: true,
  },
  {
    href: '/games',
    icon: Gamepad2,
    label: 'Discover',
    ariaLabel: 'Browse games catalog',
    testId: 'mobile-tab-discover',
  },
  {
    href: '/chat/new',
    icon: MessageSquare,
    label: 'Chat',
    ariaLabel: 'Navigate to chat',
    testId: 'mobile-tab-chat',
    authOnly: true,
  },
  {
    href: '/profile',
    icon: User,
    label: 'Profile',
    ariaLabel: 'Navigate to your profile',
    testId: 'mobile-tab-profile',
    authOnly: true,
  },
];

// ─── Active State Detection ──────────────────────────────────────────────────

function isTabActive(href: string, pathname: string | null): boolean {
  if (!pathname) return false;

  if (href === '/dashboard') {
    return pathname === '/dashboard' || pathname === '/';
  }
  if (href === '/profile') {
    return pathname === '/profile' || pathname.startsWith('/profile/');
  }
  if (href === '/chat/new') {
    return pathname.startsWith('/chat');
  }
  return pathname.startsWith(href);
}

// ─── MobileTabBar ────────────────────────────────────────────────────────────

export function MobileTabBar() {
  const pathname = usePathname();
  const { data: user } = useCurrentUser();

  const isAuthenticated = !!user;

  const visibleTabs = TAB_ITEMS.filter(tab => !tab.authOnly || isAuthenticated);

  return (
    <nav
      className={cn(
        // Mobile only
        'md:hidden',
        // Positioning
        'fixed bottom-0 left-0 right-0 z-40',
        // Height with safe area
        'h-[72px] pb-[env(safe-area-inset-bottom)]',
        // Glassmorphism
        'bg-card/90 backdrop-blur-md backdrop-saturate-150',
        'border-t border-border/50',
        'shadow-lg shadow-black/5',
        'dark:bg-card/95 dark:border-border/30 dark:shadow-black/20'
      )}
      aria-label="Primary navigation"
      data-testid="mobile-tab-bar"
    >
      <div className="flex items-center justify-around h-full px-2">
        {visibleTabs.map(tab => {
          const active = isTabActive(tab.href, pathname);
          return <TabLink key={tab.href} tab={tab} active={active} />;
        })}
      </div>
    </nav>
  );
}

// ─── TabLink ─────────────────────────────────────────────────────────────────

interface TabLinkProps {
  tab: TabItem;
  active: boolean;
}

function TabLink({ tab, active }: TabLinkProps) {
  const Icon = tab.icon;

  return (
    <Link
      href={tab.href}
      aria-label={tab.ariaLabel}
      aria-current={active ? 'page' : undefined}
      data-testid={tab.testId}
      className={cn(
        'flex flex-col items-center justify-center gap-0.5',
        // Touch target (WCAG 2.1 AA)
        'min-w-[44px] min-h-[44px]',
        'px-3 py-1.5',
        'rounded-lg',
        'transition-colors duration-200',
        // Focus ring
        'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary focus-visible:ring-offset-2',
        // Active / inactive state
        active ? 'text-primary font-semibold' : 'text-muted-foreground hover:text-foreground'
      )}
    >
      <Icon className={cn('h-5 w-5', active && 'stroke-[2.5]')} aria-hidden="true" />
      <span className="text-[10px] font-nunito leading-tight">{tab.label}</span>
    </Link>
  );
}
