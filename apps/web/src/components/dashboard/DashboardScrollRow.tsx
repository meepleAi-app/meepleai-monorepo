'use client';

import { cn } from '@/lib/utils';

interface DashboardScrollRowProps {
  children: React.ReactNode;
  className?: string;
}

export function DashboardScrollRow({ children, className }: DashboardScrollRowProps) {
  return (
    <div
      className={cn(
        'flex gap-3 overflow-x-auto scrollbar-none snap-x snap-mandatory -mx-1 px-1 pb-1',
        className
      )}
    >
      {children}
    </div>
  );
}
