'use client';

import { type ReactNode, useEffect, useState } from 'react';

import { Menu } from 'lucide-react';

import { DashboardEngineProvider } from '@/components/dashboard';
import { UserMenuDropdown } from '@/components/layout/UserMenuDropdown';
import { NotificationBell } from '@/components/notifications';
import { useNavbarHeightStore } from '@/lib/stores/navbar-height-store';

import { AdminBreadcrumb } from './AdminBreadcrumb';
import { AdminMobileDrawer } from './AdminMobileDrawer';
import { AdminTabSidebar } from './AdminTabSidebar';

interface AdminShellProps {
  children: ReactNode;
}

export function AdminShell({ children }: AdminShellProps) {
  const [drawerOpen, setDrawerOpen] = useState(false);
  const setNavbarHeight = useNavbarHeightStore(s => s.setHeight);

  useEffect(() => {
    setNavbarHeight(52);
  }, [setNavbarHeight]);

  return (
    <div className="flex h-dvh bg-background">
      <a
        href="#main-content"
        className="sr-only focus:not-sr-only focus:absolute focus:z-50 focus:px-4 focus:py-2 focus:bg-background focus:text-foreground focus:rounded-md"
      >
        Salta al contenuto
      </a>
      <AdminTabSidebar />

      <div className="flex flex-col flex-1 min-w-0">
        <header
          role="banner"
          className="sticky top-0 z-40 h-[52px] flex items-center gap-3 px-4 border-b bg-background/95 backdrop-blur-md shrink-0"
        >
          <button
            aria-label="Apri menu"
            onClick={() => setDrawerOpen(true)}
            className="p-1.5 rounded-md hover:bg-accent md:hidden"
          >
            <Menu className="w-5 h-5" />
          </button>
          <div className="flex-1" />
          <NotificationBell />
          <UserMenuDropdown />
        </header>
        <AdminBreadcrumb />
        <AdminMobileDrawer open={drawerOpen} onOpenChange={setDrawerOpen} />

        <DashboardEngineProvider>
          <main id="main-content" className="flex-1 overflow-y-auto">
            {children}
          </main>
        </DashboardEngineProvider>
      </div>
    </div>
  );
}
