'use client';

/**
 * CardActions - Quick actions menu, wishlist button, action buttons
 *
 * Composes QuickActionsMenu, WishlistButton, MeepleCardInfoButton,
 * MeepleCardQuickActions, and ActionButtons into a variant-aware
 * actions rendering layer.
 *
 * @module components/ui/data-display/meeple-card/parts/CardActions
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

export interface CardActionsProps {
  /** Card layout variant */
  variant: MeepleCardVariant;
  /** Entity type */
  entity: MeepleEntityType;
  /** Custom entity color */
  customColor?: string;
  /** Action buttons (featured/hero) */
  actions: MeepleCardAction[];
  /** Entity quick actions (hover-reveal) */
  entityQuickActions?: QuickAction[];
  /** Legacy quick actions menu items */
  quickActions?: MeepleCardProps['quickActions'];
  /** User role for legacy quick actions */
  userRole?: MeepleCardProps['userRole'];
  /** Show wishlist button */
  showWishlistBtn: boolean;
  /** Is currently wishlisted */
  isWishlisted?: boolean;
  /** Wishlist toggle handler */
  onWishlistToggle?: (id: string, isWishlisted: boolean) => void;
  /** Show info button */
  showInfoButton?: boolean;
  /** Entity ID for drawer mode */
  entityId?: string;
  /** Info href for link mode (deprecated) */
  infoHref?: string;
  /** Info button tooltip */
  infoTooltip?: string;
  /** Drawer entity type (derived from entity) */
  drawerEntityType?: DrawerEntityType;
  /** Callback to open drawer */
  onDrawerOpen?: () => void;
  /** Test ID for the card (used by wishlist) */
  testId?: string;
  /** Entity type for chat session unread positioning */
  unreadCount?: number;
  /** Has legacy quick actions */
  hasQuickActions: boolean;
}

/**
 * Renders the top-right action buttons row (desktop: hover-reveal, mobile: hidden)
 * and the featured/hero action buttons.
 */
export function CardActions({
  variant,
  entity,
  customColor,
  actions,
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
  unreadCount,
  hasQuickActions,
}: CardActionsProps) {
  const showActions = actions.length > 0 && (variant === 'featured' || variant === 'hero');

  const hasTopRightActions =
    entityQuickActions ||
    (showInfoButton && (entityId || infoHref)) ||
    showWishlistBtn ||
    hasQuickActions;

  return (
    <>
      {/* Top-right actions row (desktop hover-reveal) */}
      {hasTopRightActions && (
        <div
          className={cn(
            'absolute right-2.5 flex items-center gap-1.5 z-15',
            entity === 'chatSession' && unreadCount && unreadCount > 0 ? 'top-10' : 'top-2.5',
            'hidden md:flex md:opacity-0 md:group-hover:opacity-100 md:transition-opacity md:duration-300'
          )}
        >
          {/* New entity quick actions */}
          {entityQuickActions && entityQuickActions.length > 0 && (
            <MeepleCardQuickActions
              actions={entityQuickActions}
              entityType={entity}
              customColor={customColor}
              size="sm"
            />
          )}

          {/* Legacy quick actions menu */}
          {hasQuickActions && !entityQuickActions && quickActions && (
            <QuickActionsMenu actions={quickActions} userRole={userRole} size="sm" />
          )}

          {/* Wishlist button */}
          {showWishlistBtn && !entityQuickActions && onWishlistToggle && (
            <WishlistButton
              gameId={testId || 'card'}
              isWishlisted={!!isWishlisted}
              onToggle={onWishlistToggle}
              size="sm"
            />
          )}

          {/* Info button (drawer mode) */}
          {showInfoButton && entityId && drawerEntityType && (
            <MeepleCardInfoButton
              onClick={onDrawerOpen}
              entityType={entity}
              customColor={customColor}
              tooltip={infoTooltip}
              size="sm"
            />
          )}
          {/* Info button (link mode - backward compat) */}
          {showInfoButton && infoHref && !entityId && (
            <MeepleCardInfoButton
              href={infoHref}
              entityType={entity}
              customColor={customColor}
              tooltip={infoTooltip}
              size="sm"
            />
          )}
        </div>
      )}

      {/* Action buttons (featured/hero only) */}
      {showActions && <ActionButtons actions={actions} entity={entity} customColor={customColor} />}
    </>
  );
}
