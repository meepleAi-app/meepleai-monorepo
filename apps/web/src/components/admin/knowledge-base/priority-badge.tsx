import { memo } from 'react';

import { cn } from '@/lib/utils';

interface PriorityBadgeProps {
  priority: number;
  showNormal?: boolean;
  className?: string;
}

type PriorityLevel = 'urgent' | 'high' | 'normal' | 'low';

const priorityConfig: Record<
  PriorityLevel,
  { label: string; icon: string; containerClass: string; testId: string }
> = {
  urgent: {
    label: 'Urgente',
    icon: '\uD83D\uDD25',
    containerClass:
      'bg-red-50 text-red-700 border-red-200 dark:bg-red-950/40 dark:text-red-300 dark:border-red-800',
    testId: 'priority-badge-urgent',
  },
  high: {
    label: 'Alta',
    icon: '\u2B06',
    containerClass:
      'bg-orange-50 text-orange-700 border-orange-200 dark:bg-orange-950/40 dark:text-orange-300 dark:border-orange-800',
    testId: 'priority-badge-high',
  },
  normal: {
    label: 'Normale',
    icon: '\u25CF',
    containerClass: 'bg-muted/50 text-muted-foreground border-border/60 dark:bg-muted/30',
    testId: 'priority-badge-normal',
  },
  low: {
    label: 'Bassa',
    icon: '\u2B07',
    containerClass:
      'bg-sky-50 text-sky-700 border-sky-200 dark:bg-sky-950/40 dark:text-sky-300 dark:border-sky-800',
    testId: 'priority-badge-low',
  },
};

function getPriorityLevel(priority: number): PriorityLevel {
  if (priority >= 30) return 'urgent';
  if (priority >= 20) return 'high';
  if (priority >= 10) return 'normal';
  return 'low';
}

export const PriorityBadge = memo(function PriorityBadge({
  priority,
  showNormal = false,
  className,
}: PriorityBadgeProps) {
  const level = getPriorityLevel(priority);

  if (level === 'normal' && !showNormal) return null;

  const config = priorityConfig[level];

  return (
    <span
      className={cn(
        'inline-flex items-center gap-1 rounded-md border px-1.5 py-0.5 text-[10px] font-semibold',
        config.containerClass,
        className
      )}
      data-testid={config.testId}
    >
      <span aria-hidden="true">{config.icon}</span>
      {config.label}
    </span>
  );
});
