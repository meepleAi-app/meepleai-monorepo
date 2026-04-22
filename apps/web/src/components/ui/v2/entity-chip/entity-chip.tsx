import type { JSX } from 'react';

import { getEntityToken, type EntityType } from '../entity-tokens';

export interface EntityChipProps {
  readonly entity: EntityType;
  readonly label: string;
  readonly size?: 'sm' | 'md';
}

export function EntityChip({ entity, label, size = 'sm' }: EntityChipProps): JSX.Element {
  const t = getEntityToken(entity);
  const sizeClasses = size === 'sm' ? 'text-xs px-2 py-0.5' : 'text-sm px-3 py-1';
  return (
    <span
      className={`inline-flex items-center gap-1 rounded-full ${t.bgSoft} ${t.text} ${sizeClasses} font-medium`}
    >
      <span aria-hidden="true">{t.emoji}</span>
      <span>{label}</span>
    </span>
  );
}
