/**
 * EntityLinks API Client
 *
 * Issue #5129 — Epic C, A13: EntityLink frontend API client
 *
 * Handles entity relationship CRUD:
 * - Get links for an entity (with optional linkType / targetEntityType filter)
 * - Get link count for badge
 * - Create a link
 * - Delete a link
 */

import { z } from 'zod';

import {
  EntityLinkDtoSchema,
  EntityLinkCountResponseSchema,
  type EntityLinkDto,
  type EntityLinkType,
  type LinkEntityType,
  type CreateEntityLinkRequest,
} from '../schemas/entity-links.schemas';

import type { HttpClient } from '../core/httpClient';

export interface EntityLinksClient {
  /**
   * Get all links for an entity (bidirectional — entity may be source or target).
   *
   * @param entityType - The entity type (e.g. 'Game')
   * @param entityId   - The entity UUID
   * @param options    - Optional filters: linkType, targetEntityType
   */
  getEntityLinks(
    entityType: LinkEntityType,
    entityId: string,
    options?: {
      linkType?: EntityLinkType;
      targetEntityType?: string;
    }
  ): Promise<EntityLinkDto[]>;

  /**
   * Get total link count for an entity (used by EntityLinkBadge).
   *
   * @param entityType - The entity type
   * @param entityId   - The entity UUID
   */
  getEntityLinkCount(entityType: LinkEntityType, entityId: string): Promise<number>;

  /**
   * Create a new entity link.
   *
   * @param request - Link creation payload
   * @returns Created EntityLinkDto (201)
   */
  createEntityLink(request: CreateEntityLinkRequest): Promise<EntityLinkDto>;

  /**
   * Soft-delete an entity link.
   *
   * @param linkId - UUID of the link to delete
   */
  deleteEntityLink(linkId: string): Promise<void>;
}

export function createEntityLinksClient({
  httpClient,
}: {
  httpClient: HttpClient;
}): EntityLinksClient {
  return {
    async getEntityLinks(entityType, entityId, options) {
      const params = new URLSearchParams({ entityType, entityId });
      if (options?.linkType) params.set('linkType', options.linkType);
      if (options?.targetEntityType) params.set('targetEntityType', options.targetEntityType);

      const response = await httpClient.get<EntityLinkDto[]>(
        `/api/v1/library/entity-links?${params.toString()}`
      );
      return z.array(EntityLinkDtoSchema).parse(response ?? []);
    },

    async getEntityLinkCount(entityType, entityId) {
      const params = new URLSearchParams({ entityType, entityId });
      const response = await httpClient.get<{ count: number }>(
        `/api/v1/library/entity-links/count?${params.toString()}`
      );
      return EntityLinkCountResponseSchema.parse(response).count;
    },

    async createEntityLink(request) {
      const response = await httpClient.post<EntityLinkDto>(
        '/api/v1/library/entity-links',
        request
      );
      return EntityLinkDtoSchema.parse(response);
    },

    async deleteEntityLink(linkId) {
      await httpClient.delete(`/api/v1/library/entity-links/${linkId}`);
    },
  };
}
