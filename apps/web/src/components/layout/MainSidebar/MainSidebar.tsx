'use client';

import { usePathname } from 'next/navigation';

import { filterNavByPermission } from '@/components/layout/main-nav/filter-nav-by-permission';
import { MAIN_NAV_ITEMS } from '@/components/layout/main-nav/main-nav-config';
import { MainNavList } from '@/components/layout/main-nav/MainNavList';
import { useCurrentUser } from '@/hooks/queries/useCurrentUser';
import { useNotificationsCounter } from '@/hooks/use-notifications-counter';

export interface MainSidebarProps {
  /**
   * Optional override for the numeric badge displayed next to items with
   * `showCounter: true` (currently just `notifications`). When provided,
   * bypasses the internal SSE-driven counter — useful for Storybook /
   * tests / explicit consumer control.
   *
   * When omitted (default), the badge is driven by `useNotificationsCounter`
   * (T6 / DEC-3), which fetches `/api/v1/notifications/unread-count` and
   * subscribes to `/api/v1/notifications/stream` (SSE).
   */
  notificationCount?: number;
}

/**
 * Persistent main user sidebar for desktop (>=lg). On smaller screens it is
 * hidden (`hidden lg:flex`); a drawer-equivalent for mobile will land in T7
 * (final integration). Mirrors the structure of `AdminSidebar` so that the
 * mobile drawer in T7 can share the same `MainNavList` renderer.
 */
export function MainSidebar({ notificationCount }: MainSidebarProps = {}) {
  const { data: user } = useCurrentUser();
  const pathname = usePathname();
  const visibleItems = filterNavByPermission(MAIN_NAV_ITEMS, { authenticated: !!user });

  // SSE-driven counter (T6 DEC-3) — disabled when the user is not authenticated
  // to avoid a wasted /unread-count fetch that always 401s.
  const { count: liveCount } = useNotificationsCounter({ enabled: !!user });
  const effectiveCount = notificationCount ?? liveCount;

  return (
    <aside className="hidden lg:flex w-[280px] shrink-0 flex-col border-r bg-background overflow-y-auto">
      <div className="px-3 py-3">
        <MainNavList
          items={visibleItems}
          pathname={pathname}
          notificationCount={effectiveCount}
          ariaLabel="Main navigation"
        />
      </div>
    </aside>
  );
}
