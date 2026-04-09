'use client';

import { Suspense, type ReactNode } from 'react';

import { DashboardEngineProvider } from '@/components/dashboard';
import { AppNavbar } from '@/components/layout/AppNavbar';
import { BackToSessionFAB } from '@/components/session/BackToSessionFAB';
import { isUxRedesignEnabled } from '@/lib/feature-flags';

import { DesktopShell } from './v2';

interface UserShellClientProps {
  children: ReactNode;
}

export function UserShellClient({ children }: UserShellClientProps) {
  const useNewShell = isUxRedesignEnabled();

  if (useNewShell) {
    return (
      <DesktopShell>
        <DashboardEngineProvider>{children}</DashboardEngineProvider>
        <Suspense>
          <BackToSessionFAB />
        </Suspense>
      </DesktopShell>
    );
  }

  return (
    <div className="min-h-dvh bg-background">
      <AppNavbar />
      <main className="flex-1 min-w-0">
        <DashboardEngineProvider>{children}</DashboardEngineProvider>
      </main>
      <Suspense>
        <BackToSessionFAB />
      </Suspense>
    </div>
  );
}
