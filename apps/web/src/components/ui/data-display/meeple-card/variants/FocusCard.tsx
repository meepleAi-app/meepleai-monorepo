'use client';

import { useConnectionSource } from '../hooks/useConnectionSource';
import { AccentBorder } from '../parts/AccentBorder';
import { ConnectionChipStrip } from '../parts/ConnectionChipStrip';
import { Cover } from '../parts/Cover';
import { MetaChips } from '../parts/MetaChips';
import { Rating } from '../parts/Rating';

import type { MeepleCardProps } from '../types';

/** Full-width entity header card. Connections render as horizontal chips. */
export function FocusCard(props: MeepleCardProps) {
  const {
    entity,
    title,
    subtitle,
    imageUrl,
    rating,
    ratingMax,
    metadata = [],
    onClick,
    className = '',
  } = props;
  const testId = props['data-testid'];

  // FocusCard's chip row is visually an `inline` strip.
  const { source, items: csItems } = useConnectionSource({
    ...props,
    connectionsVariant: 'inline',
  });

  return (
    <div
      className={`relative w-full overflow-hidden rounded-2xl border border-[var(--mc-border)] bg-[var(--mc-bg-card)] shadow-[var(--mc-shadow-sm)] backdrop-blur-[12px] ${className}`}
      data-entity={entity}
      data-testid={testId}
      onClick={onClick}
    >
      <AccentBorder entity={entity} />

      {/* Hero row: cover + info */}
      <div className="flex gap-4 p-4">
        <div className="h-24 w-24 shrink-0 overflow-hidden rounded-xl">
          <Cover entity={entity} variant="compact" imageUrl={imageUrl} alt={title} />
        </div>

        <div className="flex min-w-0 flex-1 flex-col justify-center gap-1">
          <h2 className="font-[var(--font-quicksand)] text-xl font-bold leading-tight text-[var(--mc-text-primary)]">
            {title}
          </h2>
          {subtitle && <p className="text-sm text-[var(--mc-text-secondary)]">{subtitle}</p>}
          {rating !== undefined && <Rating value={rating} max={ratingMax} />}
          {metadata.length > 0 && <MetaChips metadata={metadata} />}
        </div>
      </div>

      {/* Connections chip row — inline ConnectionChipStrip below the hero */}
      {source === 'connections' && csItems.length > 0 && (
        <div className="border-t border-[var(--mc-border-light)] px-4 py-3">
          <ConnectionChipStrip connections={csItems} variant="inline" />
        </div>
      )}
    </div>
  );
}
