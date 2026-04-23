'use client';

import { navItemsToConnections } from '../adapters/navItemsToConnections';
import { useConnectionSource } from '../hooks/useConnectionSource';
import { AccentBorder } from '../parts/AccentBorder';
import { ConnectionChipStrip } from '../parts/ConnectionChipStrip';
import { Cover } from '../parts/Cover';
import { EntityBadge } from '../parts/EntityBadge';
import { ManaPips } from '../parts/ManaPips';
import { MetaChips } from '../parts/MetaChips';
import { NavFooter } from '../parts/NavFooter';
import { QuickActions } from '../parts/QuickActions';
import { Rating } from '../parts/Rating';
import { StatusBadge } from '../parts/StatusBadge';
import { TagStrip } from '../parts/TagStrip';
import { entityHsl } from '../tokens';

import type { MeepleCardProps } from '../types';

export function GridCard(props: MeepleCardProps) {
  const {
    entity,
    title,
    subtitle,
    imageUrl,
    rating,
    ratingMax,
    metadata = [],
    tags = [],
    status,
    badge,
    actions = [],
    navItems = [],
    manaPips,
    showQuickActions,
    onClick,
    className = '',
  } = props;
  const testId = props['data-testid'];

  const { source, items: csItems, variant: csVariant } = useConnectionSource(props);

  const glowColor = entityHsl(entity, 0.4);

  return (
    <div
      className={`group relative flex cursor-pointer flex-col overflow-hidden rounded-2xl border border-[var(--mc-border)] bg-[var(--mc-bg-card)] shadow-[var(--mc-shadow-sm)] outline-2 outline-offset-2 outline-transparent backdrop-blur-[12px] backdrop-saturate-[180%] transition-all duration-[350ms] [transition-timing-function:cubic-bezier(0.4,0,0.2,1)] hover:-translate-y-1.5 hover:shadow-[var(--mc-shadow-xl)] hover:outline-[var(--mc-glow)] ${className}`}
      style={{ '--mc-glow': glowColor } as React.CSSProperties}
      onClick={onClick}
      role={onClick ? 'button' : undefined}
      tabIndex={onClick ? 0 : undefined}
      data-entity={entity}
      data-testid={testId}
    >
      <AccentBorder entity={entity} />
      <div className="relative">
        <Cover entity={entity} variant="grid" imageUrl={imageUrl} alt={title} />
        <EntityBadge entity={entity} />
        {status && <StatusBadge status={status} />}
        {tags.length > 0 && <TagStrip tags={tags} entity={entity} />}
        {showQuickActions && actions.length > 0 && <QuickActions actions={actions} />}
      </div>
      <div className="flex flex-1 flex-col gap-[3px] px-3.5 py-2.5 pb-2">
        <div className="flex items-start justify-between gap-2">
          <h3 className="font-[var(--font-quicksand)] text-[0.95rem] font-bold leading-tight text-[var(--mc-text-primary)]">
            {title}
          </h3>
          {badge && (
            <span
              className="shrink-0 rounded-full border border-[var(--mc-border)] bg-black/10 px-2 py-0.5 text-[9px] font-bold uppercase tracking-wide text-[var(--mc-text-primary)] dark:bg-white/15"
              data-slot="badge"
            >
              {badge}
            </span>
          )}
        </div>
        {subtitle && (
          <p className="text-[0.78rem] leading-tight text-[var(--mc-text-secondary)]">{subtitle}</p>
        )}
        {rating !== undefined && <Rating value={rating} max={ratingMax} />}
        {metadata.length > 0 && <MetaChips metadata={metadata} />}
      </div>
      {manaPips && manaPips.length > 0 && <ManaPips pips={manaPips} size="md" />}
      {source === 'connections' && csItems.length > 0 && (
        <ConnectionChipStrip connections={csItems} variant={csVariant} />
      )}
      {source === 'navItems' && !props.__useConnectionsForNavItems && navItems.length > 0 && (
        <NavFooter items={navItems} />
      )}
      {source === 'navItems' && props.__useConnectionsForNavItems && navItems.length > 0 && (
        <ConnectionChipStrip connections={navItemsToConnections(navItems)} variant={csVariant} />
      )}
    </div>
  );
}
