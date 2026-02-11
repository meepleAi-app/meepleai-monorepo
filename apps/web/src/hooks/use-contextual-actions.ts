/**
 * useContextualActions - Context-aware action filtering for MeepleCard
 *
 * Wraps useEntityActions to filter/augment actions based on:
 * - User subscription tier (free/basic/pro/enterprise)
 * - Collection context (library/catalog/wishlist/shared)
 * - Whether the entity is already in the user's collection
 *
 * @module hooks/use-contextual-actions
 * @see Issue #4062 - MeepleCard: Context-Aware Actions
 */

import { useMemo } from 'react';

import {
  BookmarkPlus,
  BookmarkMinus,
  Heart,
  HeartOff,
} from 'lucide-react';

import type { QuickAction } from '@/components/ui/data-display/meeple-card-quick-actions';

import { useEntityActions, type UseEntityActionsProps, type EntityActions } from './use-entity-actions';

// ============================================================================
// Types
// ============================================================================

export type UserTier = 'free' | 'basic' | 'pro' | 'enterprise';
export type CollectionContext = 'library' | 'catalog' | 'wishlist' | 'shared';

export interface UseContextualActionsProps extends UseEntityActionsProps {
  /** User's subscription tier */
  tier?: UserTier;
  /** Whether entity is in user's collection/library */
  inCollection?: boolean;
  /** Collection context for action filtering */
  collectionContext?: CollectionContext;
  /** Callback: add entity to library */
  onAddToLibrary?: () => void;
  /** Callback: remove entity from library */
  onRemoveFromLibrary?: () => void;
  /** Whether entity is wishlisted */
  isWishlisted?: boolean;
  /** Callback: toggle wishlist */
  onWishlistToggle?: () => void;
}

// ============================================================================
// Tier-based action gating
// ============================================================================

/** Actions requiring at least this tier level */
const TIER_LEVELS: Record<UserTier, number> = {
  free: 0,
  basic: 1,
  pro: 2,
  enterprise: 3,
};

/** Labels that require at least 'pro' tier */
const PRO_TIER_LABELS = new Set([
  'Chat con Agent',
  'Chat',
  'Chat sui contenuti',
  'Statistiche',
  'Usa Toolkit',
]);

/** Labels that require at least 'basic' tier */
const BASIC_TIER_LABELS = new Set([
  'Avvia Sessione',
  'Riprendi',
  'Esporta',
]);

function getRequiredTier(label: string): UserTier {
  if (PRO_TIER_LABELS.has(label)) return 'pro';
  if (BASIC_TIER_LABELS.has(label)) return 'basic';
  return 'free';
}

function meetsMinTier(userTier: UserTier, requiredTier: UserTier): boolean {
  return TIER_LEVELS[userTier] >= TIER_LEVELS[requiredTier];
}

// ============================================================================
// Hook
// ============================================================================

export function useContextualActions({
  tier = 'free',
  inCollection = false,
  collectionContext = 'library',
  onAddToLibrary,
  onRemoveFromLibrary,
  isWishlisted = false,
  onWishlistToggle,
  ...entityProps
}: UseContextualActionsProps): EntityActions {
  const baseActions = useEntityActions(entityProps);

  return useMemo(() => {
    let quickActions: QuickAction[] = [];

    // Step 1: Filter base actions by tier
    quickActions = baseActions.quickActions.map(action => ({
      ...action,
      disabled: action.disabled || !meetsMinTier(tier, getRequiredTier(action.label)),
    }));

    // Step 2: Add collection-context-specific actions
    if (entityProps.entity === 'game') {
      switch (collectionContext) {
        case 'catalog':
          if (!inCollection && onAddToLibrary) {
            // Catalog: add "Add to Library" as first action
            quickActions = [
              {
                icon: BookmarkPlus,
                label: 'Aggiungi alla libreria',
                onClick: onAddToLibrary,
              },
              ...quickActions,
            ];
          }
          if (inCollection && onRemoveFromLibrary) {
            // In library from catalog view: allow removal
            quickActions = [
              {
                icon: BookmarkMinus,
                label: 'Rimuovi dalla libreria',
                onClick: onRemoveFromLibrary,
              },
              ...quickActions,
            ];
          }
          break;

        case 'wishlist': {
          // Context actions first (prioritized over base actions in max limit)
          const wishlistContextActions: QuickAction[] = [];
          if (onAddToLibrary) {
            wishlistContextActions.push({
              icon: BookmarkPlus,
              label: 'Aggiungi alla libreria',
              onClick: onAddToLibrary,
            });
          }
          if (onWishlistToggle) {
            wishlistContextActions.push({
              icon: HeartOff,
              label: 'Rimuovi dalla wishlist',
              onClick: onWishlistToggle,
            });
          }
          quickActions = [...wishlistContextActions, ...quickActions];
          break;
        }

        case 'library':
          // Library context: remove "Add to Library" (already there)
          if (onRemoveFromLibrary) {
            quickActions.push({
              icon: BookmarkMinus,
              label: 'Rimuovi dalla libreria',
              onClick: onRemoveFromLibrary,
            });
          }
          break;

        case 'shared':
          // Shared context: add wishlist toggle if not in collection
          if (!inCollection && onWishlistToggle && !isWishlisted) {
            quickActions.push({
              icon: Heart,
              label: 'Aggiungi alla wishlist',
              onClick: onWishlistToggle,
            });
          }
          break;
      }
    }

    // Step 3: Limit to max 4 visible actions (UX constraint)
    quickActions = quickActions.slice(0, 4);

    return {
      quickActions,
      moreActions: baseActions.moreActions,
    };
  }, [
    baseActions,
    tier,
    inCollection,
    collectionContext,
    entityProps.entity,
    onAddToLibrary,
    onRemoveFromLibrary,
    isWishlisted,
    onWishlistToggle,
  ]);
}
