/**
 * Dismissed Status Banners store (Issue #1089).
 *
 * Persists dismissed banner messageIds in LocalStorage so a user who dismissed
 * a banner stays dismissed across reloads (the same `messageId` is returned by
 * the backend until the underlying banner content changes).
 *
 * FIFO cap of 20 entries prevents unbounded growth — if an org churns a lot of
 * banners, only the most recent 20 dismissals are remembered. Older dismissed
 * banners may reappear, which is acceptable given the message content has
 * already been seen.
 */

import { create } from 'zustand';
import { persist, createJSONStorage } from 'zustand/middleware';

export const DISMISSED_BANNERS_CAP = 20;
export const DISMISSED_BANNERS_STORAGE_KEY = 'meeple-dismissed-banners';

interface DismissedBannersState {
  dismissedIds: string[];
  dismiss: (messageId: string) => void;
  isDismissed: (messageId: string) => boolean;
  /** Internal: clear all (used by tests). */
  reset: () => void;
}

export const useDismissedBannersStore = create<DismissedBannersState>()(
  persist(
    (set, get) => ({
      dismissedIds: [],
      dismiss: (messageId: string) =>
        set(state => {
          if (state.dismissedIds.includes(messageId)) return state;
          // FIFO cap: drop the oldest entry when over capacity.
          const next = [...state.dismissedIds, messageId];
          if (next.length > DISMISSED_BANNERS_CAP) {
            next.splice(0, next.length - DISMISSED_BANNERS_CAP);
          }
          return { dismissedIds: next };
        }),
      isDismissed: (messageId: string) => get().dismissedIds.includes(messageId),
      reset: () => set({ dismissedIds: [] }),
    }),
    {
      name: DISMISSED_BANNERS_STORAGE_KEY,
      storage: createJSONStorage(() => {
        // SSR-safe: zustand calls this lazily, but guard for the server pass.
        if (typeof window === 'undefined') {
          return {
            getItem: () => null,
            setItem: () => undefined,
            removeItem: () => undefined,
          };
        }
        return window.localStorage;
      }),
      partialize: state => ({ dismissedIds: state.dismissedIds }),
    }
  )
);
