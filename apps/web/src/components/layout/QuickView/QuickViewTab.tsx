'use client';

import { cn } from '@/lib/utils';
import type { QuickViewTab as TabType } from '@/store/quick-view';

interface QuickViewTabProps {
  tab: TabType;
  label: string;
  isActive: boolean;
  onClick: (tab: TabType) => void;
}

export function QuickViewTab({ tab, label, isActive, onClick }: QuickViewTabProps) {
  return (
    <button
      role="tab"
      aria-selected={isActive}
      onClick={() => onClick(tab)}
      className={cn(
        'flex-1 px-3 py-2 text-sm font-medium rounded-lg transition-colors',
        isActive
          ? 'bg-primary/10 text-primary'
          : 'text-muted-foreground hover:text-foreground hover:bg-muted'
      )}
    >
      {label}
    </button>
  );
}
