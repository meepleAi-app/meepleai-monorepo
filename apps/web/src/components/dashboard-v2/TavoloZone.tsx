'use client';

import type { ReactNode } from 'react';

import { cn } from '@/lib/utils';

interface TavoloZoneProps {
  children: ReactNode;
  isEmpty?: boolean;
  className?: string;
}

export function TavoloZone({ children, isEmpty, className }: TavoloZoneProps) {
  if (isEmpty) return null;

  return (
    <section
      data-testid="tavolo-zone"
      className={cn(
        'env-tavolo relative rounded-2xl p-6 overflow-hidden',
        'bg-[var(--env-bg)] dark:bg-[var(--env-bg-dark)]',
        'shadow-lg shadow-[var(--env-shadow-color)]',
        className
      )}
    >
      {/* Subtle texture overlay — reads from CSS env variable */}
      <div
        className="absolute inset-0 pointer-events-none bg-repeat opacity-[var(--env-texture-opacity)]"
        style={{ backgroundImage: 'var(--env-texture)' }}
        aria-hidden="true"
      />
      <div className="relative z-10">{children}</div>
    </section>
  );
}
