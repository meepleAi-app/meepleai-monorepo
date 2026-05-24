'use client';

import { usePathname } from 'next/navigation';

import { ADMIN_NAV_GROUPS } from '@/components/layout/admin-nav/admin-nav-config';
import { AdminNavList } from '@/components/layout/admin-nav/AdminNavList';
import { filterNavByRole } from '@/components/layout/admin-nav/filter-nav-by-role';
import { useCurrentUser } from '@/hooks/queries/useCurrentUser';

/**
 * Persistent admin sidebar for desktop (>=lg). On smaller screens it is hidden
 * (`hidden lg:flex`) and navigation is provided by AdminSideDrawer instead.
 */
export function AdminSidebar() {
  const { data: user } = useCurrentUser();
  const pathname = usePathname();
  const visibleGroups = filterNavByRole(ADMIN_NAV_GROUPS, user ?? null);

  return (
    <aside className="hidden lg:flex w-[280px] shrink-0 flex-col border-r bg-background overflow-y-auto">
      <div className="px-3 py-3">
        <AdminNavList groups={visibleGroups} pathname={pathname} ariaLabel="Admin sidebar" />
      </div>
    </aside>
  );
}
