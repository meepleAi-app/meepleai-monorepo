'use client';

import { usePathname } from 'next/navigation';

import { filterNavByPermission } from '@/components/layout/main-nav/filter-nav-by-permission';
import { MAIN_NAV_ITEMS } from '@/components/layout/main-nav/main-nav-config';
import { MainNavList } from '@/components/layout/main-nav/MainNavList';
import { useCurrentUser } from '@/hooks/queries/useCurrentUser';

export interface MainSidebarProps {
  /**
   * Numeric badge displayed next to items with `showCounter: true`
   * (currently just `notifications`). MVP value is 0 — T6 will wire it
   * to the SSE notification stream.
   */
  notificationCount?: number;
}

/**
 * Persistent main user sidebar for desktop (>=lg). On smaller screens it is
 * hidden (`hidden lg:flex`); a drawer-equivalent for mobile will land in T7
 * (final integration). Mirrors the structure of `AdminSidebar` so that the
 * mobile drawer in T7 can share the same `MainNavList` renderer.
 */
export function MainSidebar({ notificationCount = 0 }: MainSidebarProps) {
  const { data: user } = useCurrentUser();
  const pathname = usePathname();
  const visibleItems = filterNavByPermission(MAIN_NAV_ITEMS, { authenticated: !!user });

  return (
    <aside className="hidden lg:flex w-[280px] shrink-0 flex-col border-r bg-background overflow-y-auto">
      <div className="px-3 py-3">
        <MainNavList
          items={visibleItems}
          pathname={pathname}
          notificationCount={notificationCount}
          ariaLabel="Main navigation"
        />
      </div>
    </aside>
  );
}
