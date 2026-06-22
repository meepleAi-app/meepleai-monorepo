'use client';

import { Suspense, type ReactNode } from 'react';

import { DashboardEngineProvider } from '@/components/dashboard';
import { StatusBanner } from '@/components/features/status-banner';
import { ContextualHandBottomBar } from '@/components/layout/ContextualHand';
import { BackToSessionFAB } from '@/components/session/BackToSessionFAB';

import { DesktopShell } from './DesktopShell';

interface UserShellClientProps {
  children: ReactNode;
}

export function UserShellClient({ children }: UserShellClientProps) {
  return (
    <DesktopShell>
      <StatusBanner />
      <DashboardEngineProvider>{children}</DashboardEngineProvider>
      <Suspense>
        <BackToSessionFAB />
      </Suspense>
      <ContextualHandBottomBar />
    </DesktopShell>
  );
}
