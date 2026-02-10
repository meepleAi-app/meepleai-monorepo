/**
 * useWishlistHighlights Hook - Issue #3920
 *
 * React Query hook for fetching wishlist highlight items
 *
 * @see Issue #3917 - Wishlist Management API (Backend)
 * @see Issue #3920 - Frontend: Wishlist Highlights Component
 */

import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import type { WishlistHighlightItem } from '@/components/dashboard/WishlistHighlights';
import { fetchWishlistHighlights } from '@/lib/api/wishlist';

/** 5 minutes in milliseconds - matches backend cache TTL */
const FIVE_MINUTES_MS = 5 * 60 * 1000;

/**
 * Fetch and cache wishlist highlights
 *
 * Features:
 * - 5-minute stale time (matches backend cache TTL)
 * - Auto-refresh every 5 minutes
 * - Retry on failure (2 attempts)
 */
export function useWishlistHighlights(): UseQueryResult<WishlistHighlightItem[], Error> {
  return useQuery({
    queryKey: ['wishlist', 'highlights'],
    queryFn: fetchWishlistHighlights,
    staleTime: FIVE_MINUTES_MS,
    refetchInterval: FIVE_MINUTES_MS,
    retry: 2,
    refetchOnWindowFocus: false,
  });
}
