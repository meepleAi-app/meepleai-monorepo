'use client';

/**
 * MeepleCardCompact - Compact variant renderer
 *
 * Inline layout with 40x40 avatar, minimal info, no cover image.
 *
 * @module components/ui/data-display/meeple-card/variants/MeepleCardCompact
 */

import React, { useState } from 'react';

import { cn } from '@/lib/utils';

import { ExtraMeepleCardDrawer } from '../../extra-meeple-card/ExtraMeepleCardDrawer';
import { BulkSelectCheckbox } from '../../meeple-card-features/BulkSelectCheckbox';
import { CardAgentAction } from '../../meeple-card-features/CardAgentAction';
import { EntityIndicator, MeepleCardSkeleton } from '../../meeple-card-parts';
import {
  entityColors,
  DRAWER_ENTITY_TYPE_MAP,
  meepleCardVariants,
  contentVariants,
} from '../../meeple-card-styles';
import { useMobileInteraction } from '../hooks/useMobileInteraction';
import { CardActions } from '../parts/CardActions';

import type { MeepleCardProps } from '../types';

export type MeepleCardCompactProps = MeepleCardProps;

/**
 * Compact variant: inline row with 40x40 avatar, title, and optional subtitle.
 * Minimal information density, no metadata, no cover image.
 */
export const MeepleCardCompact = React.memo(function MeepleCardCompact(
  props: MeepleCardCompactProps
) {
  const {
    id,
    entity,
    title,
    subtitle,
    imageUrl: _imageUrl,
    avatarUrl: _avatarUrl,
    actions = [],
    customColor,
    onClick,
    loading = false,
    className,
    'data-testid': testId,
    showWishlist,
    isWishlisted,
    onWishlistToggle,
    quickActions,
    userRole,
    selectable,
    selected,
    onSelect,
    flippable,
    entityQuickActions,
    showInfoButton,
    entityId,
    infoHref,
    infoTooltip,
    unreadCount,
    hasAgent,
    agentId,
    onCreateAgent,
    navigateTo,
    mechanicIcon: _mechanicIcon,
    stateLabel: _stateLabel,
  } = props;

  const variant = 'compact' as const;
  // eslint-disable-next-line security/detect-object-injection
  const color = customColor || entityColors[entity].hsl;
  const hasQuickActions = !!(quickActions && quickActions.length > 0);
  const showWishlistBtn = !!showWishlist && !hasQuickActions;
  // eslint-disable-next-line security/detect-object-injection
  const drawerEntityType = DRAWER_ENTITY_TYPE_MAP[entity];

  const [drawerOpen, setDrawerOpen] = useState(false);

  const hasMobileActions =
    hasQuickActions ||
    !!entityQuickActions ||
    showWishlistBtn ||
    !!(showInfoButton && (entityId || infoHref));

  const { isMobile, handleMobileClick, cardRef } = useMobileInteraction({
    hasMobileActions,
    flippable,
    onClick,
  });

  const isInteractive = !!onClick;

  const handleDesktopClick = () => {
    if (flippable) return;
    if (onClick) onClick();
  };

  const handleCardClick = isMobile
    ? handleMobileClick
    : isInteractive
      ? handleDesktopClick
      : undefined;

  if (loading) {
    return <MeepleCardSkeleton variant={variant} />;
  }

  const Component = isInteractive ? 'div' : 'article';

  return (
    <Component
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      ref={cardRef as React.Ref<any>}
      className={cn(
        meepleCardVariants({ variant }),
        selected && 'ring-2 ring-offset-2 bg-accent/10',
        selected && `ring-[hsl(${color})]`,
        className
      )}
      style={
        {
          '--mc-entity-color': `hsl(${color})`,
          viewTransitionName: entityId ? `meeple-card-${entityId}` : undefined,
        } as React.CSSProperties
      }
      onClick={handleCardClick}
      role={isInteractive || (isMobile && hasMobileActions) ? 'button' : undefined}
      tabIndex={isInteractive || (isMobile && hasMobileActions) ? 0 : undefined}
      onKeyDown={
        isInteractive
          ? e => {
              if (e.key === 'Enter' || e.key === ' ') {
                e.preventDefault();
                onClick?.();
              }
            }
          : undefined
      }
      // eslint-disable-next-line security/detect-object-injection
      aria-label={`${entityColors[entity].name}: ${title}`}
      data-testid={testId || 'meeple-card'}
      data-entity={entity}
      data-variant={variant}
    >
      {selectable && (
        <BulkSelectCheckbox
          selectable={selectable}
          selected={!!selected}
          onSelect={onSelect || (() => {})}
          id={testId || 'card'}
          entityColor={color}
        />
      )}

      <EntityIndicator entity={entity} variant={variant} customColor={customColor} />

      {/* Compact variant: no cover image (matching original monolith behavior) */}

      {/* Content area */}
      <div className={contentVariants({ variant })}>
        <CardActions
          variant={variant}
          entity={entity}
          customColor={customColor}
          actions={actions}
        />

        <h3 className="font-quicksand font-bold leading-tight text-sm text-card-foreground">
          {title}
        </h3>

        {subtitle && <p className="text-muted-foreground text-xs mb-0">{subtitle}</p>}
      </div>

      {/* Agent action footer */}
      {entity === 'game' && hasAgent !== undefined && id && (
        <CardAgentAction
          hasAgent={hasAgent}
          agentId={agentId}
          gameId={id}
          onCreateAgent={onCreateAgent}
          variant={variant}
          hasNavFooter={!!(navigateTo && navigateTo.length > 0)}
        />
      )}

      {entityId && drawerEntityType && (
        <ExtraMeepleCardDrawer
          entityType={drawerEntityType}
          entityId={entityId}
          open={drawerOpen}
          onClose={() => setDrawerOpen(false)}
        />
      )}
    </Component>
  );
});
