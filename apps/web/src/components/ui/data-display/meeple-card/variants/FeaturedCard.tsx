'use client';

import { entityHsl } from '../tokens';
import type { MeepleCardProps } from '../types';
import { Cover } from '../parts/Cover';
import { EntityBadge } from '../parts/EntityBadge';
import { AccentBorder } from '../parts/AccentBorder';
import { StatusBadge } from '../parts/StatusBadge';
import { QuickActions } from '../parts/QuickActions';
import { Rating } from '../parts/Rating';
import { MetaChips } from '../parts/MetaChips';
import { NavFooter } from '../parts/NavFooter';

export function FeaturedCard(props: MeepleCardProps) {
  const {
    entity, title, subtitle, imageUrl, rating, ratingMax,
    metadata = [], status, actions = [], navItems = [],
    showQuickActions, onClick, className = '',
  } = props;

  return (
    <div
      className={`group relative flex w-[400px] cursor-pointer flex-col overflow-hidden rounded-[20px] border border-[var(--mc-border)] bg-[var(--mc-bg-card)] shadow-[var(--mc-shadow-lg)] outline-2 outline-offset-2 outline-transparent backdrop-blur-[12px] backdrop-saturate-[180%] transition-all duration-[350ms] [transition-timing-function:cubic-bezier(0.4,0,0.2,1)] hover:-translate-y-1.5 hover:shadow-[var(--mc-shadow-2xl)] hover:outline-[var(--mc-glow)] ${className}`}
      style={{ '--mc-glow': entityHsl(entity, 0.4) } as React.CSSProperties}
      onClick={onClick}
    >
      <AccentBorder entity={entity} />
      <div className="relative">
        <Cover entity={entity} variant="featured" imageUrl={imageUrl} alt={title} />
        <EntityBadge entity={entity} />
        {status && <StatusBadge status={status} />}
        {showQuickActions && actions.length > 0 && <QuickActions actions={actions} />}
      </div>
      <div className="flex flex-1 flex-col gap-1 px-4 py-3">
        <h3 className="font-[var(--font-quicksand)] text-[1.1rem] font-bold leading-tight text-[var(--mc-text-primary)]">{title}</h3>
        {subtitle && <p className="text-[0.82rem] text-[var(--mc-text-secondary)]">{subtitle}</p>}
        {rating !== undefined && <Rating value={rating} max={ratingMax} />}
        {metadata.length > 0 && <MetaChips metadata={metadata} />}
      </div>
      {navItems.length > 0 && <NavFooter items={navItems} size="md" />}
    </div>
  );
}
