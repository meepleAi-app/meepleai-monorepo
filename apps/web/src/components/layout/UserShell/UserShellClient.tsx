'use client';

import { type ReactNode } from 'react';

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
    <div className="flex h-dvh bg-background">
      <UserDesktopSidebar />
      <div className="flex flex-col flex-1 min-w-0">
        <UserTopNav />
        <ContextBar />
        <main className="flex-1 overflow-y-auto pb-16 lg:pb-0">
          <DashboardEngineProvider>{children}</DashboardEngineProvider>
        </main>
        <UserTabBar />
        <BackToSessionFAB />
      </div>
    </div>
  );
}
