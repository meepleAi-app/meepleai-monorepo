import type { JSX, ReactNode } from 'react';

import clsx from 'clsx';

export interface SettingsListProps {
  readonly children: ReactNode;
  readonly ariaLabel?: string;
  readonly className?: string;
}

export function SettingsList({
  children,
  ariaLabel = 'Impostazioni',
  className,
}: SettingsListProps): JSX.Element {
  return (
    <nav aria-label={ariaLabel} className={className}>
      <ul role="list" className={clsx('divide-y divide-border rounded-xl bg-card overflow-hidden')}>
        {children}
      </ul>
    </nav>
  );
}
