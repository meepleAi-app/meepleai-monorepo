'use client';

import { ConnectionChip } from './ConnectionChip';

import type { ConnectionChipProps } from '../types';

export type ConnectionChipStripVariant = 'footer' | 'inline';

interface ConnectionChipStripProps {
  variant: ConnectionChipStripVariant;
  connections: ConnectionChipProps[];
  className?: string;
}

/**
 * Layout container for multiple ConnectionChip.
 *
 * - `footer`: md chips, border-top, labels visible, used in grid/featured/hero card footer.
 * - `inline`: sm chips, no border, no labels, used in list/compact/focus meta-row.
 */
export function ConnectionChipStrip({ variant, connections, className }: ConnectionChipStripProps) {
  if (connections.length === 0) return null;

  const defaultSize: 'sm' | 'md' = variant === 'footer' ? 'md' : 'sm';

  const containerClass =
    variant === 'footer'
      ? 'flex items-center justify-center gap-3 border-t border-[var(--mc-border-light)] bg-[var(--mc-nav-footer-bg)] px-2.5 py-2 backdrop-blur-lg'
      : 'flex items-center gap-1.5';

  return (
    <div className={`${containerClass} ${className ?? ''}`.trim()} data-strip-variant={variant}>
      {connections.map((chipProps, i) => (
        <ConnectionChip
          key={`${chipProps.entityType}-${i}`}
          size={chipProps.size ?? defaultSize}
          showLabel={chipProps.showLabel ?? variant === 'footer'}
          {...chipProps}
        />
      ))}
    </div>
  );
}
