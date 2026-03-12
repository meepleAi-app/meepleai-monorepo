'use client';

import { Check } from 'lucide-react';

import { cn } from '@/lib/utils';

interface ActivationStepProps {
  stepNumber: number;
  title: string;
  completed: boolean;
  collapsed?: boolean;
  disabled?: boolean;
  testId: string;
  children?: React.ReactNode;
}

export function ActivationStep({
  stepNumber,
  title,
  completed,
  collapsed = false,
  disabled = false,
  testId,
  children,
}: ActivationStepProps) {
  return (
    <div
      data-testid={testId}
      data-completed={completed}
      data-collapsed={collapsed}
      className={cn(
        'rounded-lg border p-4 transition-all',
        completed ? 'border-green-500/30 bg-green-500/5' : 'border-border',
        disabled && 'opacity-50'
      )}
    >
      <div className="flex items-center gap-3">
        <div
          className={cn(
            'flex h-7 w-7 items-center justify-center rounded-full text-sm font-medium',
            completed
              ? 'bg-green-500 text-white'
              : 'border border-muted-foreground text-muted-foreground'
          )}
        >
          {completed ? <Check className="h-4 w-4" /> : stepNumber}
        </div>
        <span className={cn('font-medium', completed && 'text-green-600 dark:text-green-400')}>
          {title}
        </span>
      </div>
      {!collapsed && children && <div className="mt-3 pl-10">{children}</div>}
    </div>
  );
}
