/**
 * Wishlist API Client (Issue #4114)
 */

const API_BASE = process.env.NEXT_PUBLIC_API_BASE_URL || 'http://localhost:8080';

export interface WishlistItem {
  id: string;
  gameId: string;
  gameName: string;
  priority: number;
  addedAt: string;
}

export const wishlistClient = {
  getAll: async (): Promise<WishlistItem[]> => {
    const res = await fetch(`${API_BASE}/api/v1/wishlist`, { credentials: 'include' });
    if (!res.ok) throw new Error('Failed to fetch wishlist');
    return res.json();
  },

  add: async (gameId: string): Promise<void> => {
    const res = await fetch(`${API_BASE}/api/v1/wishlist`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ gameId }),
      credentials: 'include',
    });
    if (!res.ok) throw new Error('Failed to add to wishlist');
  },

  remove: async (id: string): Promise<void> => {
    const res = await fetch(`${API_BASE}/api/v1/wishlist/${id}`, {
      method: 'DELETE',
      credentials: 'include',
    });
    if (!res.ok) throw new Error('Failed to remove from wishlist');
  },
};
