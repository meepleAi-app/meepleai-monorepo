/**
 * useMeepleCardActions - Auth-aware action factory for MeepleCard button visibility
 *
 * Computes quick actions for MeepleCard buttons with 3 explicit visibility states:
 * - Visible + Enabled:  action available and clickable
 * - Visible + Disabled: action exists but unavailable (auth/permission), shows disabledTooltip
 * - Hidden:             action not applicable in this entity state/context
 *
 * The hook encapsulates the combination of:
 * - User auth state (via useCurrentUser)
 * - Entity-specific state (e.g. game in library via useGameInLibraryStatus)
 * - List context ('catalog' | 'library' | 'wishlist')
 *
 * @module hooks/useMeepleCardActions
 * @see Issue #4899 - MeepleCard button visibility system
 */

import { useMemo } from 'react';

import { BookMarked, BookX, Library } from 'lucide-react';

import type { QuickAction } from '@/components/ui/data-display/meeple-card-quick-actions';
import { useCurrentUser } from '@/hooks/queries/useCurrentUser';
import {
  useAddGameToLibrary,
  useGameInLibraryStatus,
  useRemoveGameFromLibrary,
} from '@/hooks/queries/useLibrary';

// ============================================================================
// Types
// ============================================================================

/**
 * The context in which the MeepleCard is displayed.
 * Determines which actions are relevant and how they behave.
 */
export type MeepleCardContext = 'catalog' | 'library' | 'wishlist';

export interface UseMeepleCardActionsOptions {
  /** Callback to open the add-to-library wizard instead of direct add */
  onAddToLibrary?: () => void;
  /** Callback to open the remove-from-library confirmation */
  onRemoveFromLibrary?: () => void;
}

// ============================================================================
// Context-specific action builders
// ============================================================================

/**
 * Builds library actions for a game in 'catalog' context.
 *
 * Visibility matrix:
 * | Action              | Guest              | User, not in lib   | User, in lib |
 * |---------------------|--------------------|--------------------|--------------|
 * | Add to library      | visible + disabled | visible + enabled  | hidden       |
 * | Remove from library | hidden             | hidden             | visible + enabled |
 */
function useGameCatalogActions(
  gameId: string,
  options: UseMeepleCardActionsOptions
): QuickAction[] {
  const { data: user } = useCurrentUser();
  const isAuthenticated = !!user;

  // Guard: only query when both authenticated AND gameId is non-empty
  // Non-game entities pass gameId='' to this function — we must not fire a bad API call
  const { data: libraryStatus } = useGameInLibraryStatus(
    gameId,
    isAuthenticated && !!gameId
  );

  const isInLibrary = libraryStatus?.inLibrary ?? false;

  const addToLibrary = useAddGameToLibrary();
  const removeFromLibrary = useRemoveGameFromLibrary();

  // Destructure callbacks to avoid options object reference in deps (prevents infinite re-renders)
  const { onAddToLibrary, onRemoveFromLibrary } = options;

  return useMemo((): QuickAction[] => {
    const addAction: QuickAction = {
      icon: Library,
      label: 'Aggiungi a Libreria',
      onClick: () => {
        if (onAddToLibrary) {
          onAddToLibrary();
        } else {
          addToLibrary.mutate({ gameId });
        }
      },
      // Guest: visible but disabled — action exists, requires login
      disabled: !isAuthenticated,
      disabledTooltip: 'Accedi per aggiungere alla libreria',
      // Already in library: hide entirely — action not applicable
      hidden: isInLibrary,
    };

    const removeAction: QuickAction = {
      icon: BookX,
      label: 'Rimuovi da Libreria',
      onClick: () => {
        if (onRemoveFromLibrary) {
          onRemoveFromLibrary();
        } else {
          removeFromLibrary.mutate(gameId);
        }
      },
      // Guest or not in library: hide entirely — action not applicable
      hidden: !isAuthenticated || !isInLibrary,
    };

    return [addAction, removeAction];
  }, [
    gameId,
    isAuthenticated,
    isInLibrary,
    addToLibrary,
    removeFromLibrary,
    onAddToLibrary,
    onRemoveFromLibrary,
  ]);
}

/**
 * Builds library actions for a game in 'library' context.
 *
 * In library view, only removal makes sense.
 * Add action is always hidden (user is browsing their own library).
 */
function useGameLibraryActions(
  gameId: string,
  options: UseMeepleCardActionsOptions
): QuickAction[] {
  const { data: user } = useCurrentUser();
  const isAuthenticated = !!user;

  const removeFromLibrary = useRemoveGameFromLibrary();

  // Destructure callback to avoid options object reference in deps
  const { onRemoveFromLibrary } = options;

  return useMemo((): QuickAction[] => [
    {
      icon: BookMarked,
      label: 'Rimuovi da Libreria',
      onClick: () => {
        if (onRemoveFromLibrary) {
          onRemoveFromLibrary();
        } else {
          removeFromLibrary.mutate(gameId);
        }
      },
      // Should always be enabled in library context, but guard against edge cases
      disabled: !isAuthenticated,
      disabledTooltip: 'Accedi per rimuovere dalla libreria',
      hidden: false,
    },
  ], [gameId, isAuthenticated, removeFromLibrary, onRemoveFromLibrary]);
}

// ============================================================================
// Main Hook
// ============================================================================

/**
 * Auth-aware quick action factory for MeepleCard.
 *
 * @param entityType - The type of entity displayed in the card ('game' | 'player' | ...)
 * @param entityId   - The entity UUID
 * @param context    - The list context where the card is rendered
 * @param options    - Optional callbacks for custom action handling
 * @returns Array of QuickAction with computed disabled/hidden/disabledTooltip
 *
 * @example
 * // In a catalog list component:
 * const actions = useMeepleCardActions('game', game.id, 'catalog', {
 *   onAddToLibrary: () => openWizard(game.id),
 * });
 *
 * <MeepleCard
 *   entityQuickActions={actions}
 *   // ...
 * />
 */
export function useMeepleCardActions(
  entityType: 'game' | 'player' | 'session' | 'agent' | 'kb' | 'chatSession' | 'event' | 'custom',
  entityId: string,
  context: MeepleCardContext,
  options: UseMeepleCardActionsOptions = {}
): QuickAction[] {
  // Game-specific context handlers
  const gameCatalogActions = useGameCatalogActions(
    entityType === 'game' ? entityId : '',
    options
  );
  const gameLibraryActions = useGameLibraryActions(
    entityType === 'game' ? entityId : '',
    options
  );

  return useMemo((): QuickAction[] => {
    if (entityType === 'game') {
      switch (context) {
        case 'catalog':
          return gameCatalogActions;
        case 'library':
          return gameLibraryActions;
        case 'wishlist':
          // Wishlist context: reuses catalog actions intentionally.
          // "Aggiungi a Libreria" is the primary CTA on a wishlist card.
          // If the game is already in library (hidden=true), the card will have no
          // library action — which is correct UX (game already owned, no need to add).
          return gameCatalogActions;
        default:
          return [];
      }
    }

    // Other entity types: return empty (extend in future issues)
    return [];
  }, [entityType, context, gameCatalogActions, gameLibraryActions]);
}
