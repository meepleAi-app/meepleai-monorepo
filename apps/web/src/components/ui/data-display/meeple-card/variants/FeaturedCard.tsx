'use client';

import { navItemsToConnections } from '../adapters/navItemsToConnections';
import { useConnectionSource } from '../hooks/useConnectionSource';
import { AccentBorder } from '../parts/AccentBorder';
import { ConnectionChipStrip } from '../parts/ConnectionChipStrip';
import { Cover } from '../parts/Cover';
import { EntityBadge } from '../parts/EntityBadge';
import { MetaChips } from '../parts/MetaChips';
import { NavFooter } from '../parts/NavFooter';
import { QuickActions } from '../parts/QuickActions';
import { Rating } from '../parts/Rating';
import { StatusBadge } from '../parts/StatusBadge';
import { entityHsl } from '../tokens';

import type { MeepleCardProps } from '../types';

export function FeaturedCard(props: MeepleCardProps) {
  const {
    entity,
    title,
    subtitle,
    imageUrl,
    rating,
    ratingMax,
    metadata = [],
    status,
    badge,
    actions = [],
    navItems = [],
    showQuickActions,
    onClick,
    className = '',
  } = props;
  const testId = props['data-testid'];

  const { source, items: csItems, variant: csVariant } = useConnectionSource(props);

  return (
    <div
      className={`group relative flex w-[400px] cursor-pointer flex-col overflow-hidden rounded-[20px] border border-[var(--mc-border)] bg-[var(--mc-bg-card)] shadow-[var(--mc-shadow-lg)] outline-2 outline-offset-2 outline-transparent backdrop-blur-[12px] backdrop-saturate-[180%] transition-all duration-[350ms] [transition-timing-function:cubic-bezier(0.4,0,0.2,1)] hover:-translate-y-1.5 hover:shadow-[var(--mc-shadow-2xl)] hover:outline-[var(--mc-glow)] ${className}`}
      style={{ '--mc-glow': entityHsl(entity, 0.4) } as React.CSSProperties}
      onClick={onClick}
      role={onClick ? 'button' : undefined}
      tabIndex={onClick ? 0 : undefined}
      data-entity={entity}
      data-testid={testId}
    >
      <AccentBorder entity={entity} />
      <div className="relative">
        <Cover entity={entity} variant="featured" imageUrl={imageUrl} alt={title} />
        {/* Top-left badge stack — see GridCard for rationale */}
        <div
          className="absolute left-2.5 top-2 z-10 flex flex-col items-start gap-1"
          data-slot="badge-stack"
        >
          <EntityBadge entity={entity} stacked />
          {status && <StatusBadge status={status} stacked />}
        </div>
        {showQuickActions && actions.length > 0 && <QuickActions actions={actions} />}
      </div>
      <div className="flex flex-1 flex-col gap-1 px-4 py-3">
        <div className="flex items-start justify-between gap-2">
          <h3 className="font-[var(--font-quicksand)] text-[1.1rem] font-bold leading-tight text-[var(--mc-text-primary)]">
            {title}
          </h3>
          {badge && (
            <span
              className="shrink-0 rounded-full border border-[var(--mc-border)] bg-black/10 px-2.5 py-0.5 text-[10px] font-bold uppercase tracking-wide text-[var(--mc-text-primary)] dark:bg-white/15"
              data-slot="badge"
            >
              {badge}
            </span>
          )}
        </div>
        {subtitle && <p className="text-[0.82rem] text-[var(--mc-text-secondary)]">{subtitle}</p>}
        {rating !== undefined && <Rating value={rating} max={ratingMax} />}
        {metadata.length > 0 && <MetaChips metadata={metadata} />}
      </div>
      {source === 'connections' && csItems.length > 0 && (
        <ConnectionChipStrip connections={csItems} variant={csVariant} />
      )}
      {source === 'navItems' && !props.__useConnectionsForNavItems && navItems.length > 0 && (
        <NavFooter items={navItems} size="md" />
      )}
      {source === 'navItems' && props.__useConnectionsForNavItems && navItems.length > 0 && (
        <ConnectionChipStrip connections={navItemsToConnections(navItems)} variant={csVariant} />
      )}
    </div>
  );
}
