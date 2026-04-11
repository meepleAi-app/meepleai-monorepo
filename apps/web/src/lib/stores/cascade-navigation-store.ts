/**
 * Cascade Navigation Store
 *
 * Manages the Mana Pip -> DeckStack -> Drawer cascade navigation flow.
 * Zustand store (not React context) so state is shared between
 * SessionPanel and SessionBar components.
 */

import { create } from 'zustand';
import { devtools } from 'zustand/middleware';

import type { MeepleEntityType } from '@/components/ui/data-display/meeple-card';

// ============================================================================
// Types
// ============================================================================

export interface DrawerStackEntry {
  entityType: MeepleEntityType;
  entityId: string;
  activeTabId?: string;
}

export interface CascadeNavigationState {
  state: 'closed' | 'deckStack' | 'drawer';
  activeEntityType: MeepleEntityType | null;
  activeEntityId: string | null;
  activeTabId: string | null;
  sourceEntityId: string | null;
  anchorRect: DOMRect | null;
  deckStackSkipped: boolean;
  drawerStack: DrawerStackEntry[];
  openDeckStack: (entityType: MeepleEntityType, sourceEntityId: string, anchor?: DOMRect) => void;
  openDrawer: (entityType: MeepleEntityType, entityId: string, tabId?: string) => void;
  closeDrawer: () => void;
  closeCascade: () => void;
  pushDrawer: (entityType: MeepleEntityType, entityId: string) => void;
  popDrawer: () => void;
}

// ============================================================================
// Initial State
// ============================================================================

const initialState = {
  state: 'closed' as const,
  activeEntityType: null as MeepleEntityType | null,
  activeEntityId: null as string | null,
  activeTabId: null as string | null,
  sourceEntityId: null as string | null,
  anchorRect: null as DOMRect | null,
  deckStackSkipped: false,
  drawerStack: [] as DrawerStackEntry[],
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

      openDrawer: (entityType: MeepleEntityType, entityId: string, tabId?: string) => {
        const current = get();
        const skipped = current.state !== 'deckStack';

        set(
          {
            state: 'drawer',
            activeEntityType: entityType,
            activeEntityId: entityId,
            activeTabId: tabId ?? null,
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

      pushDrawer: (entityType: MeepleEntityType, entityId: string) => {
        const current = get();
        const entry: DrawerStackEntry = {
          entityType: current.activeEntityType!,
          entityId: current.activeEntityId!,
          activeTabId: current.activeTabId ?? undefined,
        };
        const stack = [...current.drawerStack, entry].slice(-3); // max 3
        set(
          {
            state: 'drawer',
            activeEntityType: entityType,
            activeEntityId: entityId,
            activeTabId: null,
            drawerStack: stack,
          },
          false,
          'pushDrawer'
        );
      },

      popDrawer: () => {
        const current = get();
        if (current.drawerStack.length === 0) {
          get().closeDrawer();
          return;
        }
        const stack = [...current.drawerStack];
        const prev = stack.pop()!;
        set(
          {
            state: 'drawer',
            activeEntityType: prev.entityType,
            activeEntityId: prev.entityId,
            activeTabId: prev.activeTabId ?? null,
            drawerStack: stack,
          },
          false,
          'popDrawer'
        );
      },
    }),
    { name: 'CascadeNavigationStore' }
  )
);
