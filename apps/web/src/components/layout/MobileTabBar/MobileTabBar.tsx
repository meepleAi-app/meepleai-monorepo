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
 *
 * Center tab morphs to contextual entity when on detail pages.
 */

'use client';

import { Gamepad2, LayoutDashboard, LibraryBig, MessageSquare, User } from 'lucide-react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';

import { useCurrentUser } from '@/hooks/queries/useCurrentUser';
import type { ContextualEntity } from '@/hooks/useContextualEntity';
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

interface MobileTabBarProps {
  contextualEntity?: ContextualEntity | null;
  hasSheetContent?: boolean;
  onCenterTabPress?: () => void;
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

export function MobileTabBar({
  contextualEntity,
  hasSheetContent = false,
  onCenterTabPress,
}: MobileTabBarProps) {
  const pathname = usePathname();
  const { data: user } = useCurrentUser();

  const isAuthenticated = !!user;

  const visibleTabs = TAB_ITEMS.filter(tab => !tab.authOnly || isAuthenticated);

  const hasMorphedCenter = isAuthenticated && contextualEntity != null;
  const centerIdx = visibleTabs.findIndex(t => t.href === '/games');

  return (
    <nav
      className={cn(
        // Mobile only
        'md:hidden',
        // Positioning
        'fixed bottom-0 left-0 right-0 z-40',
        // Height with safe area
        'pb-[env(safe-area-inset-bottom)]',
        // Glassmorphism
        'bg-card/90 backdrop-blur-md backdrop-saturate-150',
        'border-t border-border/50',
        'shadow-lg shadow-black/5',
        'dark:bg-card/95 dark:border-border/30 dark:shadow-black/20'
      )}
      aria-label="Primary navigation"
      data-testid="mobile-tab-bar"
    >
      {/* Swipe-up indicator */}
      {hasSheetContent && hasMorphedCenter && (
        <div className="absolute -top-2 left-1/2 -translate-x-1/2 w-9 h-1 bg-white/15 rounded-full" />
      )}
      <div className="flex items-center justify-around h-16 px-2">
        {visibleTabs.map((tab, idx) => {
          if (idx === centerIdx && hasMorphedCenter) {
            return (
              <MorphedCenterTab
                key="morphed-center"
                entity={contextualEntity!}
                onPress={onCenterTabPress}
              />
            );
          }
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

// ─── MorphedCenterTab ─────────────────────────────────────────────────────────

function MorphedCenterTab({ entity, onPress }: { entity: ContextualEntity; onPress?: () => void }) {
  const Icon = entity.icon;

  return (
    <button
      type="button"
      onClick={onPress}
      aria-label={`${entity.title} actions`}
      data-testid="mobile-tab-morphed-center"
      className={cn(
        'flex flex-col items-center justify-center gap-0.5',
        'min-w-[44px] min-h-[44px]',
        'px-2 py-1.5',
        'rounded-lg',
        'transition-all duration-300',
        'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary focus-visible:ring-offset-2'
      )}
    >
      <div
        className="flex items-center justify-center w-11 h-11 rounded-xl border-2 transition-colors"
        style={{
          borderColor: `hsl(${entity.color} / 0.4)`,
          background: `hsl(${entity.color} / 0.12)`,
        }}
      >
        <span style={{ color: `hsl(${entity.color})` }}>
          <Icon className="w-5 h-5" />
        </span>
      </div>
      <span
        className="text-[10px] font-nunito font-semibold leading-tight truncate max-w-[60px]"
        style={{ color: `hsl(${entity.color})` }}
      >
        {entity.title}
      </span>
    </button>
  );
}
