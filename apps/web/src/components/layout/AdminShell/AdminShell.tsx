'use client';

import { type ReactNode, useState } from 'react';

import { DashboardEngineProvider } from '@/components/dashboard-v2';

import { AdminBreadcrumb } from './AdminBreadcrumb';
import { AdminMobileDrawer } from './AdminMobileDrawer';
import { AdminTabSidebar } from './AdminTabSidebar';
import { UserTopNav } from '../UserShell/UserTopNav';

interface AdminShellProps {
  children: ReactNode;
}

export function AdminShell({ children }: AdminShellProps) {
  const [drawerOpen, setDrawerOpen] = useState(false);

  return (
    <div className="flex h-dvh bg-background">
      <AdminTabSidebar />

      <div className="flex flex-col flex-1 min-w-0">
        <UserTopNav isAdmin onMenuToggle={() => setDrawerOpen(true)} isMenuOpen={drawerOpen} />
        <AdminBreadcrumb />
        <AdminMobileDrawer open={drawerOpen} onOpenChange={setDrawerOpen} />

        <DashboardEngineProvider>
          <main className="flex-1 overflow-y-auto">{children}</main>
        </DashboardEngineProvider>
      </div>
    </div>
  );
}
