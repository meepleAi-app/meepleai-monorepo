import type { JSX, ReactNode } from 'react';

export interface KPIStatGridProps {
  readonly children: ReactNode;
  readonly columns?: 2 | 3 | 4;
  readonly className?: string;
}

const COLUMNS_CLASS: Record<2 | 3 | 4, string> = {
  2: 'grid-cols-2',
  3: 'grid-cols-2 sm:grid-cols-3',
  4: 'grid-cols-2 sm:grid-cols-4',
};

export function KPIStatGrid({ children, columns = 4, className }: KPIStatGridProps): JSX.Element {
  return (
    <div
      role="list"
      className={['grid gap-2.5 sm:gap-3.5', COLUMNS_CLASS[columns], className ?? '']
        .filter(Boolean)
        .join(' ')}
    >
      {children}
    </div>
  );
}
