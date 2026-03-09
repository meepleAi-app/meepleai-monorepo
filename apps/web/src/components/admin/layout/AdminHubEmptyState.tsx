import { type ReactNode } from 'react';

import { cn } from '@/lib/utils';

interface AdminHubEmptyStateProps {
  icon: ReactNode;
  title: string;
  description: string;
  action?: ReactNode;
}

/**
 * Consistent empty state for admin hub tabs.
 * Glassmorphic card with centered content.
 */
export function AdminHubEmptyState({ icon, title, description, action }: AdminHubEmptyStateProps) {
  return (
    <div
      className={cn(
        'flex flex-col items-center justify-center py-16 px-6 text-center',
        'rounded-2xl border border-dashed border-border/50',
        'bg-white/30 dark:bg-zinc-800/20 backdrop-blur-sm'
      )}
    >
      <div className="flex h-14 w-14 items-center justify-center rounded-2xl bg-muted/50 [&>svg]:h-7 [&>svg]:w-7 [&>svg]:text-muted-foreground/40">
        {icon}
      </div>
      <h3 className="mt-4 font-quicksand text-sm font-semibold text-foreground">{title}</h3>
      <p className="mt-1.5 max-w-sm text-xs text-muted-foreground leading-relaxed">{description}</p>
      {action && <div className="mt-4">{action}</div>}
    </div>
  );
}
