'use client';

import { Suspense, type ReactNode } from 'react';

import { DashboardEngineProvider } from '@/components/dashboard';
import { BackToSessionFAB } from '@/components/session/BackToSessionFAB';

import { ContextBar } from '../ContextBar';
import { UserDesktopSidebar } from './UserDesktopSidebar';
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
        <UserDesktopSidebar />
      </Suspense>
      <main className="lg:ml-[52px] flex-1 min-w-0 overflow-y-auto pb-16 lg:pb-0">
        <DashboardEngineProvider>{children}</DashboardEngineProvider>
      </main>
      <Suspense>
        <UserTabBar />
      </Suspense>
      <BackToSessionFAB />
    </div>
  );
}
