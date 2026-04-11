'use client';

import { BarChart3 } from 'lucide-react';

import { useDashboardMode } from '@/components/dashboard';
import { useCascadeNavigationStore } from '@/lib/stores/cascade-navigation-store';
import { cn } from '@/lib/utils';

export function SessionBanner() {
  const { isGameMode, activeSessionId } = useDashboardMode();

  const openDrawer = useCascadeNavigationStore(s => s.openDrawer);

  if (!isGameMode || !activeSessionId) return null;

  return (
    <div
      className={cn(
        'hidden md:flex h-8 shrink-0 items-center gap-3 px-4',
        'bg-indigo-600/10 border-b border-indigo-400/30'
      )}
      data-testid="session-banner"
    >
      <span className="font-nunito text-xs font-semibold text-indigo-700 truncate min-w-0">
        {`Sessione #${activeSessionId.slice(0, 6)}`}
      </span>

      <div className="ml-auto flex items-center gap-1">
        <button
          type="button"
          onClick={() => openDrawer('session', activeSessionId, 'live')}
          className="flex items-center gap-1 rounded-md px-2 py-0.5 text-[10px] font-bold text-indigo-600 hover:bg-indigo-100 transition-colors"
        >
          <BarChart3 className="h-3 w-3" />
          <span>Classifica</span>
        </button>
      </div>
    </div>
  );
}
