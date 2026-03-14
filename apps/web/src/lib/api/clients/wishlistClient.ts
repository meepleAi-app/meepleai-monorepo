/**
 * Wishlist API Client
 */

import { z } from 'zod';

import {
  WishlistItemDtoSchema,
  type WishlistItemDto,
  type AddToWishlistRequest,
  type UpdateWishlistItemRequest,
} from '../schemas/wishlist.schemas';

import type { HttpClient } from '../core/httpClient';

export interface WishlistClient {
  list(): Promise<WishlistItemDto[]>;
  highlights(): Promise<WishlistItemDto[]>;
  add(data: AddToWishlistRequest): Promise<WishlistItemDto>;
  update(id: string, data: UpdateWishlistItemRequest): Promise<WishlistItemDto>;
  remove(id: string): Promise<void>;
}

export function createWishlistClient({ httpClient }: { httpClient: HttpClient }): WishlistClient {
  return {
    async list() {
      const data = await httpClient.get<WishlistItemDto[]>('/api/v1/wishlist');
      return z.array(WishlistItemDtoSchema).parse(data);
    },

    async highlights() {
      const data = await httpClient.get<WishlistItemDto[]>('/api/v1/wishlist/highlights');
      return z.array(WishlistItemDtoSchema).parse(data);
    },

    async add(input) {
      const data = await httpClient.post<WishlistItemDto>('/api/v1/wishlist', input);
      return WishlistItemDtoSchema.parse(data);
    },

    async update(id, input) {
      const data = await httpClient.put<WishlistItemDto>(`/api/v1/wishlist/${id}`, input);
      return WishlistItemDtoSchema.parse(data);
    },

    async remove(id) {
      await httpClient.delete(`/api/v1/wishlist/${id}`);
    },
  };
}
