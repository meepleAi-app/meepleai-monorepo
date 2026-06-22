'use client';

import { cn } from '@/lib/utils';

import type { LucideIcon } from 'lucide-react';

export interface DrawerAction {
  icon: LucideIcon;
  label: string;
  onClick: () => void;
  variant: 'primary' | 'secondary' | 'danger';
  enabled: boolean;
}

interface DrawerActionFooterProps {
  actions: DrawerAction[];
  className?: string;
}

const variantStyles: Record<DrawerAction['variant'], string> = {
  primary:
    'bg-[var(--bg-card)] text-[var(--text)] font-bold hover:bg-[var(--nh-bg-surface-hover)]',
  secondary: 'bg-transparent text-[var(--text-sec)] hover:bg-[var(--bg-card)]',
  danger: 'bg-transparent text-red-600 hover:bg-red-50 dark:hover:bg-red-950/20',
};

export function DrawerActionFooter({ actions, className }: DrawerActionFooterProps) {
  const visible = actions.filter(a => a.enabled);
  if (visible.length === 0) return null;

  return (
    <div
      className={cn(
        'flex items-center gap-2 border-t border-[var(--border)] px-4 py-3',
        className
      )}
      data-testid="drawer-action-footer"
    >
      {visible.map(action => {
        const Icon = action.icon;
        return (
          <button
            key={action.label}
            type="button"
            onClick={action.onClick}
            className={cn(
              'flex items-center gap-1.5 rounded-lg px-3 py-2 text-xs font-semibold transition-colors',
              variantStyles[action.variant]
            )}
          >
            <Icon className="h-3.5 w-3.5" aria-hidden="true" />
            {action.label}
          </button>
        );
      })}
    </div>
  );
}
