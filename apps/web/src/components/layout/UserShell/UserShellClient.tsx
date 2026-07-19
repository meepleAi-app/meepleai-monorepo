'use client';

import { Suspense, type ReactNode } from 'react';

import { usePathname } from 'next/navigation';

import { DashboardEngineProvider } from '@/components/dashboard';
import { StatusBanner } from '@/components/features/status-banner';
import { isImmersiveRoute } from '@/components/layout/AppNav/immersive-routes';
import { ContextualHandBottomBar } from '@/components/layout/ContextualHand';
import { BackToSessionFAB } from '@/components/session/BackToSessionFAB';

import { DesktopShell } from './DesktopShell';

interface UserShellClientProps {
  children: ReactNode;
}

export function UserShellClient({ children }: UserShellClientProps) {
  const pathname = usePathname();
  // #3146 Slice 2: immersive routes replace the global navbar chrome with an
  // in-session layout that owns its own bottom surface (e.g. the GameNight live
  // hub's tab-nav). Suppress ALL global fixed bottom chrome there — not just the
  // MobileBottomBar (which self-hides) — so nothing overlaps the in-session nav.
  const immersive = isImmersiveRoute(pathname);
  return (
    <DesktopShell>
      <StatusBanner />
      <DashboardEngineProvider>{children}</DashboardEngineProvider>
      {!immersive && (
        <>
          <Suspense>
            <BackToSessionFAB />
          </Suspense>
          <ContextualHandBottomBar />
        </>
      )}
    </DesktopShell>
  );
}
