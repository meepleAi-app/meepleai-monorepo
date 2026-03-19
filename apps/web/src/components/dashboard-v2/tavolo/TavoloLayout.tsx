'use client';
import { type ReactNode } from 'react';

import { ActivityFeed } from './ActivityFeed';

interface TavoloLayoutProps {
  children: ReactNode;
}

export function TavoloLayout({ children }: TavoloLayoutProps) {
  return (
    <div className="grid grid-cols-1 gap-4 lg:grid-cols-[1fr_280px]">
      <div className="min-w-0 space-y-6">{children}</div>
      <aside className="hidden lg:block">
        <div className="sticky top-20 rounded-xl border border-dashed border-[#30363d] bg-[#0d1117] p-4">
          <h3 className="mb-3 text-[11px] font-bold uppercase tracking-wider text-[#a855f7]">
            📜 Feed Attività
          </h3>
          <ActivityFeed />
        </div>
      </aside>
    </div>
  );
}
