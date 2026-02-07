/**
 * Wishlist Store - Issue #3832
 *
 * Zustand store for managing user's wishlist state with localStorage persistence.
 *
 * Features:
 * - Set-based storage (O(1) lookup)
 * - localStorage persistence
 * - Toggle/add/remove/clear methods
 * - Optimistic UI updates
 */

import { create } from 'zustand';
import { persist } from 'zustand/middleware';

// ============================================================================
// Types
// ============================================================================

interface WishlistState {
  /** Set of game IDs in wishlist */
  wishlistIds: Set<string>;
  /** Toggle game in/out of wishlist */
  toggleWishlist: (gameId: string) => void;
  /** Add game to wishlist */
  addToWishlist: (gameId: string) => void;
  /** Remove game from wishlist */
  removeFromWishlist: (gameId: string) => void;
  /** Clear entire wishlist */
  clearWishlist: () => void;
  /** Check if game is in wishlist */
  isWishlisted: (gameId: string) => boolean;
}

// ============================================================================
// Store
// ============================================================================

export const useWishlist = create<WishlistState>()(
  persist(
    (set, get) => ({
      wishlistIds: new Set<string>(),

      toggleWishlist: (gameId: string) =>
        set((state) => {
          const next = new Set(state.wishlistIds);
          if (next.has(gameId)) {
            next.delete(gameId);
          } else {
            next.add(gameId);
          }
          return { wishlistIds: next };
        }),

      addToWishlist: (gameId: string) =>
        set((state) => {
          const next = new Set(state.wishlistIds);
          next.add(gameId);
          return { wishlistIds: next };
        }),

      removeFromWishlist: (gameId: string) =>
        set((state) => {
          const next = new Set(state.wishlistIds);
          next.delete(gameId);
          return { wishlistIds: next };
        }),

      clearWishlist: () => set({ wishlistIds: new Set<string>() }),

      isWishlisted: (gameId: string) => get().wishlistIds.has(gameId),
    }),
    {
      name: 'meepleai-wishlist', // localStorage key
      // Serialize Set to Array for JSON storage
      partialize: (state) => ({
        wishlistIds: Array.from(state.wishlistIds),
      }),
      // Deserialize Array back to Set
      merge: (persistedState: any, currentState) => ({
        ...currentState,
        wishlistIds: new Set(persistedState?.wishlistIds || []),
      }),
    }
  )
);
