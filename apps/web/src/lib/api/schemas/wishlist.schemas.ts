/**
 * Wishlist Zod Schemas
 */

import { z } from 'zod';

export const WishlistItemDtoSchema = z.object({
  id: z.string().uuid(),
  userId: z.string().uuid(),
  gameId: z.string().uuid(),
  gameName: z.string().optional(),
  priority: z.string(),
  targetPrice: z.number().nullable(),
  notes: z.string().nullable(),
  addedAt: z.string(),
  updatedAt: z.string().nullable(),
  visibility: z.string(),
});
export type WishlistItemDto = z.infer<typeof WishlistItemDtoSchema>;

export const AddToWishlistRequestSchema = z.object({
  gameId: z.string().uuid(),
  priority: z.string(),
  targetPrice: z.number().nullable().optional(),
  notes: z.string().nullable().optional(),
});
export type AddToWishlistRequest = z.infer<typeof AddToWishlistRequestSchema>;

export const UpdateWishlistItemRequestSchema = z.object({
  priority: z.string().optional(),
  targetPrice: z.number().nullable().optional(),
  clearTargetPrice: z.boolean().optional(),
  notes: z.string().nullable().optional(),
  clearNotes: z.boolean().optional(),
});
export type UpdateWishlistItemRequest = z.infer<typeof UpdateWishlistItemRequestSchema>;
