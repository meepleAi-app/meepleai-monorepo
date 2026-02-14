import { apiClient } from "./client";

export interface WishlistItem {
  id: string;
  userId: string;
  gameId: string;
  priority: string; // "LOW" | "MEDIUM" | "HIGH"
  targetPrice?: number;
  notes?: string;
  addedAt: string;
}

export async function getWishlistHighlights(): Promise<WishlistItem[]> {
  const response = await apiClient.get<WishlistItem[]>("/wishlist/highlights");
  if (!response) return [];
  return response;
}

export const fetchWishlistHighlights = getWishlistHighlights;
