'use client';

/**
 * MeepleCardExpanded - Expanded variant renderer
 *
 * Full-screen card designed for the card browser overlay / focus view.
 * Shows cover image, title, subtitle, metadata chips, tags, and action bar.
 * No hover effects — this is a focus view, not a browse view.
 *
 * @module components/ui/data-display/meeple-card/variants/MeepleCardExpanded
 */

import React from 'react';

import { ArrowRight } from 'lucide-react';

import { cn } from '@/lib/utils';

import { StatusBadge } from '../../meeple-card-features/StatusBadge';
import { WishlistButton } from '../../meeple-card-features/WishlistButton';
import { CoverImage } from '../../meeple-card-parts';
import { entityColors, meepleCardVariants, contentVariants } from '../../meeple-card-styles';

import type { MeepleCardProps } from '../types';

export type MeepleCardExpandedProps = MeepleCardProps;

/**
 * Expanded variant: full-screen card for card browser overlay.
 * Cover image with entity color border-top, title, subtitle,
 * metadata chips (max 4), horizontal-scroll tags, and action bar.
 */
export const MeepleCardExpanded = React.memo(function MeepleCardExpanded(
  props: MeepleCardExpandedProps
) {
  const {
    entity,
    title,
    subtitle,
    imageUrl,
    avatarUrl,
    metadata = [],
    tags = [],
    maxVisibleTags,
    customColor,
    onClick,
    className,
    'data-testid': testId,
    status,
    showStatusIcon,
    showWishlist,
    isWishlisted,
    onWishlistToggle,
    id,
  } = props;

  const variant = 'expanded' as const;
  const coverSrc = entity === 'player' ? avatarUrl || imageUrl : imageUrl;
  // eslint-disable-next-line security/detect-object-injection
  const color = customColor || entityColors[entity].hsl;

  // Limit metadata to 4 items
  const visibleMetadata = metadata.slice(0, 4);

  return (
    <article
      className={cn(meepleCardVariants({ variant }), className)}
      style={
        {
          '--mc-entity-color': `hsl(${color})`,
        } as React.CSSProperties
      }
      aria-label={`${entityColors[entity].name}: ${title}`}
      data-testid={testId || 'meeple-card'}
      data-entity={entity}
      data-variant={variant}
    >
      {/* Cover image with entity color border-top */}
      <div className="relative border-t-4" style={{ borderColor: `hsl(${color})` }}>
        <div style={{ height: 'max(40vh, 200px)' }} className="w-full overflow-hidden">
          {coverSrc ? (
            <CoverImage
              src={coverSrc}
              alt={title}
              variant={variant}
              entity={entity}
              customColor={customColor}
            />
          ) : (
            <div
              className="w-full h-full flex items-center justify-center"
              style={{ backgroundColor: `hsla(${color}, 0.1)` }}
            >
              <span className="text-4xl text-muted-foreground/40">
                {title.charAt(0).toUpperCase()}
              </span>
            </div>
          )}
        </div>

        {/* Status badges overlay */}
        {status && (
          <div className="absolute top-3 left-3 z-10">
            <StatusBadge status={status} showIcon={showStatusIcon} />
          </div>
        )}
      </div>

      {/* Content area */}
      <div className={contentVariants({ variant })}>
        {/* Title */}
        <h3
          className="text-lg font-heading font-bold line-clamp-2 text-card-foreground"
          data-testid="expanded-title"
        >
          {title}
        </h3>

        {/* Subtitle */}
        {subtitle && <p className="text-sm text-muted-foreground line-clamp-1">{subtitle}</p>}

        {/* Metadata chips (max 4) */}
        {visibleMetadata.length > 0 && (
          <div className="flex flex-wrap gap-2" data-testid="expanded-metadata">
            {visibleMetadata.map((item, index) => (
              <span
                key={index}
                className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-medium bg-muted text-muted-foreground"
              >
                {item.icon && <item.icon className="w-3.5 h-3.5" aria-hidden="true" />}
                {item.label || item.value}
              </span>
            ))}
          </div>
        )}

        {/* Tags row (horizontal scroll) */}
        {tags.length > 0 && (
          <div
            className="flex overflow-x-auto gap-1.5 flex-nowrap scrollbar-none"
            data-testid="expanded-tags"
          >
            {tags.map((tag, index) => {
              const tagLabel = typeof tag === 'string' ? tag : tag.label;
              return (
                <span
                  key={index}
                  className="inline-flex items-center whitespace-nowrap px-2 py-0.5 rounded text-[11px] font-semibold bg-accent/60 text-accent-foreground shrink-0"
                >
                  {tagLabel}
                </span>
              );
            })}
          </div>
        )}

        {/* Action bar */}
        <div className="flex items-center justify-between mt-auto pt-2">
          {showWishlist && id && onWishlistToggle ? (
            <WishlistButton
              gameId={id}
              isWishlisted={!!isWishlisted}
              onToggle={onWishlistToggle}
              size="sm"
            />
          ) : (
            <div />
          )}

          {onClick && (
            <button
              type="button"
              onClick={e => {
                e.stopPropagation();
                onClick();
              }}
              className="inline-flex items-center gap-1 text-sm font-medium text-primary hover:underline"
              data-testid="expanded-see-all"
            >
              See all
              <ArrowRight className="w-4 h-4" />
            </button>
          )}
        </div>
      </div>
    </article>
  );
});
