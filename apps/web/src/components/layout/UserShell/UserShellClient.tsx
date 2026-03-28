'use client';

import { Suspense, type ReactNode } from 'react';

import { DashboardEngineProvider } from '@/components/dashboard';
import { BackToSessionFAB } from '@/components/session/BackToSessionFAB';
import { MobileBottomNav } from '@/components/ui/navigation/MobileBottomNav';

import { ContextBar } from '../ContextBar';
import { HybridSidebar } from './HybridSidebar';
import { UserTabBar } from './UserTabBar';
import { UserTopNav } from './UserTopNav';

interface UserShellClientProps {
  children: ReactNode;
}

export function UserShellClient({ children }: UserShellClientProps) {
  return (
    <div className="flex flex-col h-dvh bg-background">
      <UserTopNav />
      <ContextBar />
      <Suspense>
        <HybridSidebar />
      </Suspense>
      <main className="lg:ml-[52px] flex-1 min-w-0 overflow-y-auto pb-16 lg:pb-0">
        <DashboardEngineProvider>{children}</DashboardEngineProvider>
      </main>
      <Suspense>
        <UserTabBar />
      </Suspense>
      <BackToSessionFAB />
      <MobileBottomNav />
    </div>
  );
}
