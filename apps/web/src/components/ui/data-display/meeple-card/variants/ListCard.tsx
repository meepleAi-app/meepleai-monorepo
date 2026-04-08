'use client';

import { MetaChips } from '../parts/MetaChips';
import { NavFooter } from '../parts/NavFooter';
import { Rating } from '../parts/Rating';
import { entityHsl, entityIcon } from '../tokens';

import type { MeepleCardProps } from '../types';

export function ListCard(props: MeepleCardProps) {
  const {
    entity,
    title,
    subtitle,
    imageUrl,
    rating,
    ratingMax,
    metadata = [],
    badge,
    navItems = [],
    onClick,
    className = '',
  } = props;
  const testId = props['data-testid'];

  return (
    <div
      className={`group flex items-center gap-3.5 rounded-xl border border-[var(--mc-border)] bg-[var(--mc-bg-card)] px-3.5 py-3 shadow-[var(--mc-shadow-sm)] backdrop-blur-[12px] transition-all duration-300 hover:translate-x-1 hover:shadow-[var(--mc-shadow-md)] ${className}`}
      onClick={onClick}
      role={onClick ? 'button' : undefined}
      tabIndex={onClick ? 0 : undefined}
      data-testid={testId}
    >
      <div
        className="h-2 w-2 flex-shrink-0 rounded-full"
        style={{ background: entityHsl(entity) }}
      />
      <div className="h-[52px] w-[52px] flex-shrink-0 overflow-hidden rounded-lg">
        {imageUrl ? (
          <img src={imageUrl} alt={title} className="h-full w-full object-cover" loading="lazy" />
        ) : (
          <div
            className="flex h-full w-full items-center justify-center text-xl opacity-50"
            style={{ background: entityHsl(entity, 0.08) }}
          >
            {entityIcon[entity]}
          </div>
        )}
      </div>
      <div className="flex min-w-0 flex-1 flex-col gap-0.5">
        <div className="flex items-center gap-2">
          <h3 className="truncate font-[var(--font-quicksand)] text-[0.88rem] font-bold text-[var(--mc-text-primary)]">
            {title}
          </h3>
          {badge && (
            <span
              className="shrink-0 rounded-full border border-[var(--mc-border)] bg-[var(--mc-bg-muted)] px-1.5 py-0.5 text-[8px] font-semibold uppercase tracking-wide text-[var(--mc-text-secondary)]"
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
      {navItems.length > 0 && (
        <div className="flex-shrink-0">
          <NavFooter items={navItems} size="sm" />
        </div>
      )}
    </div>
  );
}
