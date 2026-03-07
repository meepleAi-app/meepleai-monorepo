'use client';

import { type ReactNode } from 'react';

import { usePathname } from 'next/navigation';

import { NavActionBar } from '@/components/layout/ActionBar/NavActionBar';
import { TooltipProvider } from '@/components/ui/overlays/tooltip';
import { getActiveSection } from '@/config/admin-dashboard-navigation';
import { NavigationProvider } from '@/context/NavigationContext';
import { cn } from '@/lib/utils';

import { AdminContextualSidebar } from './AdminContextualSidebar';
import { AdminMobileNav } from './AdminMobileNav';
import { AdminMobileTabBar } from './AdminMobileTabBar';
import { AdminTopNav } from './AdminTopNav';

export interface AdminShellProps {
  children: ReactNode;
  /** Badge counts keyed by badgeKey */
  badges?: Record<string, number>;
  /** Current user display name */
  userName?: string;
  /** Current user email */
  userEmail?: string;
}

export function AdminShell({ children, badges = {}, userName, userEmail }: AdminShellProps) {
  const pathname = usePathname() ?? '/admin/overview';
  const activeSection = getActiveSection(pathname);

  return (
    <NavigationProvider>
      <TooltipProvider>
        <div className="flex flex-col h-screen overflow-hidden bg-gradient-to-br from-slate-50 via-amber-50/20 to-orange-50/10 dark:from-zinc-950 dark:via-zinc-900 dark:to-zinc-950">
          {/* Top Navigation */}
          <AdminTopNav badges={badges} userName={userName} userEmail={userEmail} />

          {/* Mobile nav trigger integrated into a slim bar on mobile */}
          <div className="md:hidden flex items-center gap-2 px-3 py-2 border-b border-slate-200/60 dark:border-zinc-700/40 bg-white/60 dark:bg-zinc-900/60 backdrop-blur-md">
            <AdminMobileNav section={activeSection} badges={badges} />
            {activeSection && (
              <div className="flex items-center gap-2 text-sm">
                <activeSection.icon className="h-4 w-4 text-amber-700 dark:text-amber-400" />
                <span className="font-quicksand font-semibold text-foreground">
                  {activeSection.label}
                </span>
              </div>
            )}
          </div>

          {/* Content area with sidebar */}
          <div className="flex flex-1 overflow-hidden">
            {/* Contextual Sidebar (desktop only) */}
            <AdminContextualSidebar section={activeSection} badges={badges} />

            {/* Main content */}
            <main id="main-content" className={cn('flex-1 overflow-y-auto', 'bg-transparent')}>
              {/* Subtle paper texture overlay */}
              <div
                className="fixed inset-0 opacity-[0.012] pointer-events-none dark:opacity-0"
                style={{
                  backgroundImage: `url("data:image/svg+xml,%3Csvg width='200' height='200' xmlns='http://www.w3.org/2000/svg'%3E%3Cfilter id='noise'%3E%3CfeTurbulence type='fractalNoise' baseFrequency='0.9' numOctaves='4' /%3E%3C/filter%3E%3Crect width='100%25' height='100%25' filter='url(%23noise)' /%3E%3C/svg%3E")`,
                }}
              />

              <div className="relative max-w-[1400px] mx-auto px-4 sm:px-6 lg:px-8 py-6">
                {children}
              </div>

              {/* NavActionBar (L3) — contextual actions registered by admin hub pages */}
              <NavActionBar />
            </main>
          </div>
        </div>
      </TooltipProvider>
      {/* Admin bottom tab bar (mobile only) */}
      <AdminMobileTabBar />
    </NavigationProvider>
  );
}
