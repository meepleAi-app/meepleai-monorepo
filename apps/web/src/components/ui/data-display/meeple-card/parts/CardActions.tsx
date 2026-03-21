'use client';

/**
 * CardActions - Action strip + featured/hero action buttons
 *
 * CardActionStrip: vertical right-edge strip with stagger slide-in (rendered inside CardCover)
 * CardActions: featured/hero ActionButtons (rendered in content area)
 */

import React from 'react';

import { cn } from '@/lib/utils';

import { QuickActionsMenu } from '../../meeple-card-features/QuickActionsMenu';
import { WishlistButton } from '../../meeple-card-features/WishlistButton';
import { MeepleCardInfoButton } from '../../meeple-card-info-button';
import { ActionButtons } from '../../meeple-card-parts';
import { MeepleCardQuickActions } from '../../meeple-card-quick-actions';

import type { DrawerEntityType } from '../../extra-meeple-card/ExtraMeepleCardDrawer';
import type {
  MeepleCardProps,
  MeepleCardAction,
  MeepleCardVariant,
  MeepleEntityType,
  QuickAction,
} from '../types';

// ============================================================================
// CardActionStrip — vertical right-edge strip inside cover
// ============================================================================

export interface CardActionStripProps {
  entity: MeepleEntityType;
  customColor?: string;
  entityQuickActions?: QuickAction[];
  quickActions?: MeepleCardProps['quickActions'];
  userRole?: MeepleCardProps['userRole'];
  showWishlistBtn: boolean;
  isWishlisted?: boolean;
  onWishlistToggle?: (id: string, isWishlisted: boolean) => void;
  showInfoButton?: boolean;
  entityId?: string;
  infoHref?: string;
  infoTooltip?: string;
  drawerEntityType?: DrawerEntityType;
  onDrawerOpen?: () => void;
  testId?: string;
  hasQuickActions: boolean;
}

export function CardActionStrip({
  entity,
  customColor,
  entityQuickActions,
  quickActions,
  userRole,
  showWishlistBtn,
  isWishlisted,
  onWishlistToggle,
  showInfoButton,
  entityId,
  infoHref,
  infoTooltip,
  drawerEntityType,
  onDrawerOpen,
  testId,
  hasQuickActions,
}: CardActionStripProps) {
  const items: React.ReactNode[] = [];

  if (entityQuickActions && entityQuickActions.length > 0) {
    items.push(
      <MeepleCardQuickActions
        key="entity-qa"
        actions={entityQuickActions}
        entityType={entity}
        customColor={customColor}
        size="sm"
      />
    );
  }

  if (hasQuickActions && !entityQuickActions && quickActions) {
    items.push(
      <QuickActionsMenu key="legacy-qa" actions={quickActions} userRole={userRole} size="sm" />
    );
  }

  if (showWishlistBtn && !entityQuickActions && onWishlistToggle) {
    items.push(
      <WishlistButton
        key="wishlist"
        gameId={testId || 'card'}
        isWishlisted={!!isWishlisted}
        onToggle={onWishlistToggle}
        size="sm"
      />
    );
  }

  if (showInfoButton && entityId && drawerEntityType) {
    items.push(
      <MeepleCardInfoButton
        key="info-drawer"
        onClick={onDrawerOpen}
        entityType={entity}
        customColor={customColor}
        tooltip={infoTooltip}
        size="sm"
      />
    );
  } else if (showInfoButton && infoHref && !entityId) {
    items.push(
      <MeepleCardInfoButton
        key="info-link"
        href={infoHref}
        entityType={entity}
        customColor={customColor}
        tooltip={infoTooltip}
        size="sm"
      />
    );
  }

  if (items.length === 0) return null;

  return (
    <div
      className={cn(
        'absolute right-1.5 top-10 bottom-10',
        'flex flex-col items-center justify-center gap-1.5',
        'z-15 pointer-events-none',
        'hidden md:flex'
      )}
      data-testid="card-action-strip"
    >
      {items.map((item, index) => (
        <div
          key={index}
          className="pointer-events-auto opacity-0 group-hover:animate-mc-slide-in-right"
          style={{ animationDelay: `${index * 50}ms` }}
        >
          {item}
        </div>
      ))}
    </div>
  );
}

// ============================================================================
// CardActions — featured/hero ActionButtons (content area)
// ============================================================================

export interface CardActionsProps {
  variant: MeepleCardVariant;
  entity: MeepleEntityType;
  customColor?: string;
  actions: MeepleCardAction[];
}

export function CardActions({ variant, entity, customColor, actions }: CardActionsProps) {
  const showActions = actions.length > 0 && (variant === 'featured' || variant === 'hero');
  if (!showActions) return null;

  return <ActionButtons actions={actions} entity={entity} customColor={customColor} />;
}
