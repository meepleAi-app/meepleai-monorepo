'use client';

import Link from 'next/link';

import { cn } from '@/lib/utils';

interface QuickAction {
  icon: React.ReactNode;
  label: string;
  href: string;
  entityColor?: string;
}

interface QuickActionsRowProps {
  actions: QuickAction[];
  className?: string;
}

export function QuickActionsRow({ actions, className }: QuickActionsRowProps) {
  return (
    <div className={cn('flex gap-2 overflow-x-auto scrollbar-none pb-1 -mx-1 px-1', className)}>
      {actions.map(action => (
        <Link
          key={action.href}
          href={action.href}
          className="flex flex-col items-center gap-1 min-w-[56px] shrink-0"
          aria-label={action.label}
        >
          <div
            className="w-11 h-11 rounded-xl flex items-center justify-center bg-card border border-border [box-shadow:var(--shadow-warm-sm)] text-muted-foreground hover:text-foreground transition-colors"
            style={
              action.entityColor ? { borderColor: `hsl(${action.entityColor} / 0.3)` } : undefined
            }
          >
            {action.icon}
          </div>
          <span className="text-[9px] font-semibold text-muted-foreground text-center leading-tight">
            {action.label}
          </span>
        </Link>
      ))}
    </div>
  );
}
