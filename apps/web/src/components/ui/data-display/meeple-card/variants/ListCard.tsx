'use client';

import { useConnectionSource } from '../hooks/useConnectionSource';
import { ConnectionChipStrip } from '../parts/ConnectionChipStrip';
import { Cover } from '../parts/Cover';
import { MetaChips } from '../parts/MetaChips';
import { Rating } from '../parts/Rating';
import { entityHsl } from '../tokens';

import type { MeepleCardProps } from '../types';

export function ListCard(props: MeepleCardProps) {
  const {
    entity,
    title,
    id,
    subtitle,
    imageUrl,
    rating,
    ratingMax,
    metadata = [],
    badge,
    onClick,
    className = '',
    headingLevel,
  } = props;
  const testId = props['data-testid'];

  // ListCard uses `inline` variant regardless of connectionsVariant.
  const { source, items: csItems } = useConnectionSource({
    ...props,
    connectionsVariant: 'inline',
  });

  return (
    <div
      className={`group flex items-center gap-3.5 rounded-xl border border-[var(--mc-border)] bg-[var(--mc-bg-card)] px-3.5 py-3 shadow-[var(--mc-shadow-sm)] backdrop-blur-[12px] transition-all duration-300 hover:translate-x-1 hover:shadow-[var(--mc-shadow-md)] ${className}`}
      onClick={onClick}
      role={onClick ? 'button' : undefined}
      tabIndex={onClick ? 0 : undefined}
      data-entity={entity}
      data-testid={testId}
    >
      <div
        className="h-2 w-2 flex-shrink-0 rounded-full"
        style={{ background: entityHsl(entity) }}
      />
      <div className="h-[52px] w-[52px] flex-shrink-0 overflow-hidden rounded-lg">
        <Cover entity={entity} variant="compact" imageUrl={imageUrl} alt={title} gameId={id} />
      </div>
      <div className="flex min-w-0 flex-1 flex-col gap-0.5">
        <div className="flex items-center gap-2">
          {(() => {
            const HeadingTag = `h${headingLevel ?? 3}` as 'h2' | 'h3' | 'h4';
            return (
              <HeadingTag className="truncate font-[var(--font-quicksand)] text-[0.88rem] font-bold text-[var(--mc-text-primary)]">
                {title}
              </HeadingTag>
            );
          })()}
          {badge && (
            <span
              className="shrink-0 rounded-full border border-[var(--mc-border)] bg-foreground/10 px-1.5 py-0.5 text-[8px] font-bold uppercase tracking-wide text-[var(--mc-text-primary)] dark:bg-card/15"
              data-slot="badge"
            >
              {badge}
            </span>
          )}
        </div>
        {subtitle && (
          <p className="truncate text-[0.75rem] text-[var(--mc-text-secondary)]">{subtitle}</p>
        )}
        <div className="flex items-center gap-2">
          {rating !== undefined && <Rating value={rating} max={ratingMax} />}
          {metadata.length > 0 && <MetaChips metadata={metadata} />}
        </div>
      </div>
      {source === 'connections' && csItems.length > 0 && (
        <div className="flex-shrink-0">
          <ConnectionChipStrip connections={csItems} variant="inline" />
        </div>
      )}
    </div>
  );
}
