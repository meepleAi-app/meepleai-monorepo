/**
 * useWishlist — React Query hooks for Wishlist
 */

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type {
  AddToWishlistRequest,
  UpdateWishlistItemRequest,
} from '@/lib/api/schemas/wishlist.schemas';

export const wishlistKeys = {
  all: ['wishlist'] as const,
  list: () => [...wishlistKeys.all, 'list'] as const,
  highlights: () => [...wishlistKeys.all, 'highlights'] as const,
};

export function useWishlist() {
  return useQuery({
    queryKey: wishlistKeys.list(),
    queryFn: () => api.wishlist.list(),
  });
}

export function useWishlistHighlights() {
  return useQuery({
    queryKey: wishlistKeys.highlights(),
    queryFn: () => api.wishlist.highlights(),
  });
}

export function useAddToWishlist() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: AddToWishlistRequest) => api.wishlist.add(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: wishlistKeys.all });
    },
  });
}

export function useUpdateWishlistItem() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: UpdateWishlistItemRequest }) =>
      api.wishlist.update(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: wishlistKeys.all });
    },
  });
}

export function useRemoveFromWishlist() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.wishlist.remove(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: wishlistKeys.all });
    },
  });
}
