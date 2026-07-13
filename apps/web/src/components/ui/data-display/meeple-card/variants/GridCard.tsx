'use client';

import Link from 'next/link';

import { useConnectionSource } from '../hooks/useConnectionSource';
import { AccentBorder } from '../parts/AccentBorder';
import { CardFooter } from '../parts/CardFooter';
import { ConnectionChipStrip } from '../parts/ConnectionChipStrip';
import { Cover } from '../parts/Cover';
import { CoverAttributionChip } from '../parts/CoverAttributionChip';
import { EntityBadge } from '../parts/EntityBadge';
import { ManaPips } from '../parts/ManaPips';
import { MenuPlaceholder } from '../parts/MenuPlaceholder';
import { MetaChips } from '../parts/MetaChips';
import { QuickActions } from '../parts/QuickActions';
import { Rating } from '../parts/Rating';
import { TagStrip } from '../parts/TagStrip';
import { entityHsl } from '../tokens';

import type { MeepleCardProps } from '../types';

export function GridCard(props: MeepleCardProps) {
  const {
    entity,
    title,
    id,
    subtitle,
    imageUrl,
    coverEmoji,
    headingLevel,
    rating,
    ratingMax,
    metadata = [],
    tags = [],
    status,
    badge,
    actions = [],
    manaPips,
    showQuickActions,
    onClick,
    href,
    className = '',
    attribution,
  } = props;
  const testId = props['data-testid'];

  const { source, items: csItems, variant: csVariant } = useConnectionSource(props);

  const glowColor = entityHsl(entity, 0.4);

  const rootClassName = `group relative flex cursor-pointer flex-col overflow-hidden rounded-2xl border border-[var(--mc-border)] bg-[var(--mc-bg-card)] shadow-[var(--mc-shadow-sm)] outline-2 outline-offset-2 outline-transparent backdrop-blur-[12px] backdrop-saturate-[180%] transition-all duration-[350ms] [transition-timing-function:cubic-bezier(0.4,0,0.2,1)] hover:-translate-y-1.5 hover:shadow-[var(--mc-shadow-xl)] hover:outline-[var(--mc-glow)] ${className}`;
  const rootStyle = { '--mc-glow': glowColor } as React.CSSProperties;

  const content = (
    <>
      <AccentBorder entity={entity} />
      <div className="relative">
        <Cover
          entity={entity}
          variant="grid"
          imageUrl={imageUrl}
          alt={title}
          gameId={id}
          coverEmoji={coverEmoji}
        />
        {/* Top-left badge stack: EntityBadge only (StatusBadge moved to footer per #1856 DEC-5). */}
        <div
          className="absolute left-2.5 top-2 z-10 flex flex-col items-start gap-1"
          data-slot="badge-stack"
        >
          <EntityBadge entity={entity} stacked />
        </div>
        {/* Top-right hover-visible 3-dot menu placeholder (#1856 DEC-4). */}
        {(!showQuickActions || actions.length === 0) && <MenuPlaceholder />}
        {tags.length > 0 && <TagStrip tags={tags} entity={entity} topClass="top-9" />}
        {showQuickActions && actions.length > 0 && <QuickActions actions={actions} />}
      </div>
      <div className="flex flex-1 flex-col gap-[3px] px-3.5 py-2.5 pb-2">
        {(() => {
          const HeadingTag = `h${headingLevel ?? 3}` as 'h2' | 'h3' | 'h4';
          return (
            <HeadingTag className="font-[var(--font-quicksand)] text-[0.95rem] font-bold leading-tight text-[var(--mc-text-primary)]">
              {title}
            </HeadingTag>
          );
        })()}
        {subtitle && (
          <p className="text-[0.78rem] leading-tight text-[var(--mc-text-secondary)]">{subtitle}</p>
        )}
        {rating !== undefined && <Rating value={rating} max={ratingMax} />}
        {metadata.length > 0 && <MetaChips metadata={metadata} />}
        <CoverAttributionChip attribution={attribution} />
      </div>
      {manaPips && manaPips.length > 0 && <ManaPips pips={manaPips} size="md" />}
      {source === 'connections' && csItems.length > 0 && (
        <ConnectionChipStrip connections={csItems} variant={csVariant} />
      )}
      {/* Footer: StatusDot + uppercase mono badge (#1856 DEC-5). */}
      <CardFooter status={status} badge={badge} />
    </>
  );

  if (href) {
    return (
      <Link
        href={href}
        prefetch
        className={`${rootClassName} no-underline`}
        style={rootStyle}
        data-entity={entity}
        data-testid={testId}
      >
        {content}
      </Link>
    );
  }

  return (
    <div
      className={rootClassName}
      style={rootStyle}
      onClick={onClick}
      role={onClick ? 'button' : undefined}
      tabIndex={onClick ? 0 : undefined}
      data-entity={entity}
      data-testid={testId}
    >
      {content}
    </div>
  );
}
