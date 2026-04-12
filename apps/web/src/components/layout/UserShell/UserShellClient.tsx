'use client';

import { Suspense, type ReactNode } from 'react';

import { DashboardEngineProvider } from '@/components/dashboard';
import { ContextualHandBottomBar } from '@/components/layout/ContextualHand';
import { BackToSessionFAB } from '@/components/session/BackToSessionFAB';

import { DesktopShell } from './DesktopShell';

interface UserShellClientProps {
  children: ReactNode;
}

export function UserShellClient({ children }: UserShellClientProps) {
  return (
    <DesktopShell>
      <DashboardEngineProvider>{children}</DashboardEngineProvider>
      <Suspense>
        <BackToSessionFAB />
      </Suspense>
      <ContextualHandBottomBar />
    </DesktopShell>
  );
}
