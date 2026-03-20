'use client';

import { type ReactNode } from 'react';

import { DashboardEngineProvider } from '@/components/dashboard-v2';

import { ContextBar } from '../ContextBar';
import { UserTopNav } from './UserTopNav';

interface UserShellClientProps {
  children: ReactNode;
}

export function UserShellClient({ children }: UserShellClientProps) {
  return (
    <div className="flex flex-col h-dvh bg-background">
      <UserTopNav />
      <ContextBar />
      <main className="flex-1 overflow-y-auto">
        <DashboardEngineProvider>{children}</DashboardEngineProvider>
      </main>
    </div>
  );
}
