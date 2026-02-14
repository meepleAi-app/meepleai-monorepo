/**
 * Chat Info Panel Store (Issue #4365)
 *
 * Manages ChatInfoPanel UI state with localStorage persistence:
 * - Panel collapsed/expanded state per breakpoint
 * - Active section tracking
 *
 * Middleware Stack:
 * - devtools: Browser DevTools integration
 * - persist: localStorage for collapse state
 * - immer: Mutable state updates
 */

import { create } from 'zustand';
import { devtools, persist, createJSONStorage } from 'zustand/middleware';
import { immer } from 'zustand/middleware/immer';

// ============================================================================
// Store State Interface
// ============================================================================

export interface ChatInfoState {
  /** Panel collapsed on desktop (lg+) */
  isCollapsed: boolean;
  /** Panel open on mobile/tablet (Sheet drawer) */
  isMobileOpen: boolean;

  // Actions
  toggleCollapsed: () => void;
  setCollapsed: (collapsed: boolean) => void;
  setMobileOpen: (open: boolean) => void;
  toggleMobileOpen: () => void;
}

// ============================================================================
// Store Creation
// ============================================================================

export const useChatInfoStore = create<ChatInfoState>()(
  devtools(
    persist(
      immer((set) => ({
        // Initial state
        isCollapsed: false,
        isMobileOpen: false,

        toggleCollapsed: () => {
          set(state => {
            state.isCollapsed = !state.isCollapsed;
          });
        },

        setCollapsed: (collapsed: boolean) => {
          set(state => {
            state.isCollapsed = collapsed;
          });
        },

        setMobileOpen: (open: boolean) => {
          set(state => {
            state.isMobileOpen = open;
          });
        },

        toggleMobileOpen: () => {
          set(state => {
            state.isMobileOpen = !state.isMobileOpen;
          });
        },
      })),
      {
        name: 'meepleai-chat-info-panel',
        storage: createJSONStorage(() =>
          typeof window !== 'undefined'
            ? localStorage
            : {
                getItem: () => null,
                setItem: () => {},
                removeItem: () => {},
              }
        ),
        // Only persist collapse state, not mobile drawer
        partialize: state => ({
          isCollapsed: state.isCollapsed,
        }),
      }
    ),
    {
      name: 'ChatInfoStore',
    }
  )
);

// ============================================================================
// Selectors
// ============================================================================

export const selectIsCollapsed = (state: ChatInfoState) => state.isCollapsed;
export const selectIsMobileOpen = (state: ChatInfoState) => state.isMobileOpen;
