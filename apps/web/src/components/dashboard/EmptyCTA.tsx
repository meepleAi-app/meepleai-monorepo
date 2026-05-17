'use client';

import type { ReactElement } from 'react';

import Link from 'next/link';

import type { MeepleEntityType } from '@/components/ui/data-display/meeple-card';

export interface EmptyCTAAction {
  label: string;
  href: string;
  primary?: boolean;
}

export interface EmptyCTAProps {
  entity: MeepleEntityType;
  icon: string;
  title: string;
  sub: string;
  actions: EmptyCTAAction[];
}

export function EmptyCTA({ entity, icon, title, sub, actions }: EmptyCTAProps): ReactElement {
  return (
    <div
      data-entity={entity}
      role="status"
      className={`e-${entity} flex flex-col items-center gap-3 rounded-xl border border-dashed
                  border-[hsl(var(--e)/0.25)] bg-[hsl(var(--e)/0.04)] p-8 text-center`}
    >
      <span className="text-[32px]" aria-hidden="true">
        {icon}
      </span>
      <div>
        <p className="font-quicksand text-base font-bold text-foreground">{title}</p>
        <p className="mt-1 max-w-sm text-sm leading-relaxed text-muted-foreground">{sub}</p>
      </div>
      {actions.length > 0 && (
        <div className="flex flex-wrap justify-center gap-2">
          {actions.map(action => (
            <Link
              key={action.href}
              href={action.href}
              className={
                action.primary
                  ? 'inline-flex items-center gap-1 rounded-full bg-[hsl(var(--e))] px-4 py-2 font-quicksand text-xs font-bold text-white shadow-sm transition-transform hover:scale-[1.03]'
                  : 'inline-flex items-center gap-1 rounded-full border border-[hsl(var(--e)/0.6)] bg-transparent px-4 py-2 font-quicksand text-xs font-bold text-[hsl(var(--e))]'
              }
            >
              {action.label}
            </Link>
          ))}
        </div>
      )}
    </div>
  );
}
