'use client';

import { useState, type ReactNode } from 'react';

import { DashboardEngineProvider } from '@/components/dashboard';
import { AdminSidebar } from '@/components/layout/AdminSidebar/AdminSidebar';
import { AdminSideDrawer } from '@/components/layout/AdminSideDrawer/AdminSideDrawer';
import { AppTopBar } from '@/components/layout/AppNav/AppTopBar';
import { MobileTopBar } from '@/components/layout/AppNav/MobileTopBar';

interface AdminShellProps {
  children: ReactNode;
}

/**
 * Admin shell (sp4-dashboard navbar). The new top bar sits above the persistent
 * admin sidebar (lg+). The admin drawer is opened from the mobile top bar (< md)
 * and from the top bar hamburger on the md–lg range (where the sidebar is hidden).
 */
export function AdminShell({ children }: AdminShellProps) {
  const [drawerOpen, setDrawerOpen] = useState(false);

  return (
    <div data-admin-shell data-theme="dark" className="flex min-h-dvh flex-col bg-[var(--bg)]">
      <AppTopBar adminMode onMenuClick={() => setDrawerOpen(true)} />
      <MobileTopBar adminMode onHamburgerClick={() => setDrawerOpen(true)} />

      <div className="flex flex-1 min-h-0 overflow-hidden">
        <AdminSidebar />
        <main id="main-content" className="flex-1 overflow-y-auto overflow-x-clip">
          <DashboardEngineProvider>{children}</DashboardEngineProvider>
        </main>
      </div>

      <AdminSideDrawer open={drawerOpen} onClose={() => setDrawerOpen(false)} />
    </div>
  );
}
