/**
 * Cascade Navigation Store
 *
 * Manages the Mana Pip -> DeckStack -> Drawer cascade navigation flow.
 * Zustand store (not React context) so state is shared between
 * SessionPanel and SessionBar components.
 */

import { create } from 'zustand';
import { devtools } from 'zustand/middleware';

import type { MeepleEntityType } from '@/components/ui/data-display/meeple-card-styles';

// ============================================================================
// Types
// ============================================================================

export interface CascadeNavigationState {
  state: 'closed' | 'deckStack' | 'drawer';
  activeEntityType: MeepleEntityType | null;
  activeEntityId: string | null;
  sourceEntityId: string | null;
  anchorRect: DOMRect | null;
  deckStackSkipped: boolean;
  openDeckStack: (entityType: MeepleEntityType, sourceEntityId: string, anchor?: DOMRect) => void;
  openDrawer: (entityType: MeepleEntityType, entityId: string) => void;
  closeDrawer: () => void;
  closeCascade: () => void;
}

// ============================================================================
// Initial State
// ============================================================================

const initialState = {
  state: 'closed' as const,
  activeEntityType: null as MeepleEntityType | null,
  activeEntityId: null as string | null,
  sourceEntityId: null as string | null,
  anchorRect: null as DOMRect | null,
  deckStackSkipped: false,
};

// ============================================================================
// Store
// ============================================================================

export const useCascadeNavigationStore = create<CascadeNavigationState>()(
  devtools(
    (set, get) => ({
      ...initialState,

      openDeckStack: (entityType: MeepleEntityType, sourceEntityId: string, anchor?: DOMRect) => {
        set(
          {
            state: 'deckStack',
            activeEntityType: entityType,
            activeEntityId: null,
            sourceEntityId,
            anchorRect: anchor ?? null,
            deckStackSkipped: false,
          },
          false,
          'openDeckStack'
        );
      },

      openDrawer: (entityType: MeepleEntityType, entityId: string) => {
        const current = get();
        const skipped = current.state !== 'deckStack';

        set(
          {
            state: 'drawer',
            activeEntityType: entityType,
            activeEntityId: entityId,
            deckStackSkipped: skipped,
          },
          false,
          'openDrawer'
        );
      },

      closeDrawer: () => {
        const { deckStackSkipped } = get();

        if (deckStackSkipped) {
          // Skipped deck stack, so go back to closed
          set({ ...initialState }, false, 'closeDrawer/toClosed');
        } else {
          // Came from deck stack, so return there
          set(
            {
              state: 'deckStack',
              activeEntityId: null,
            },
            false,
            'closeDrawer/toDeckStack'
          );
        }
      },

      closeCascade: () => {
        set({ ...initialState }, false, 'closeCascade');
      },
    }),
    { name: 'CascadeNavigationStore' }
  )
);
