'use client';

import { useState, type ReactNode } from 'react';

import { DashboardEngineProvider } from '@/components/dashboard';
import { AdminSideDrawer } from '@/components/layout/AdminSideDrawer/AdminSideDrawer';
import { SearchOverlay } from '@/components/layout/SearchOverlay';
import { TopBarV2 } from '@/components/layout/UserShell/TopBarV2';

interface AdminShellProps {
  children: ReactNode;
}

export function AdminShell({ children }: AdminShellProps) {
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [searchOpen, setSearchOpen] = useState(false);

  return (
    <div className="flex min-h-dvh flex-col bg-[var(--bg-base)]">
      <TopBarV2
        onHamburgerClick={() => setDrawerOpen(true)}
        onSearchClick={() => setSearchOpen(true)}
        adminMode
      />

      <main id="main-content" className="flex-1 overflow-y-auto overflow-x-clip">
        <DashboardEngineProvider>{children}</DashboardEngineProvider>
      </main>

      <AdminSideDrawer open={drawerOpen} onClose={() => setDrawerOpen(false)} />
      <SearchOverlay open={searchOpen} onClose={() => setSearchOpen(false)} />
    </div>
  );
}
