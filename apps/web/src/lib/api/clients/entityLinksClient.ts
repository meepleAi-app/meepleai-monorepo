/**
 * EntityLinks API Client (Issue #5188)
 *
 * Handles user-facing entity link operations.
 * Endpoint: GET /api/v1/library/entity-links
 */

import { z } from 'zod';

import { EntityLinkDtoSchema, type EntityLinkDto } from '../schemas/entity-link.schemas';

import type { HttpClient } from '../core/httpClient';

export interface EntityLinksClient {
  /**
   * Get entity links for a given entity, with optional filters.
   *
   * @param entityType - Source entity type (e.g. "Game")
   * @param entityId   - Source entity UUID
   * @param options    - Optional filters: linkType, targetEntityType
   */
  getEntityLinks(
    entityType: string,
    entityId: string,
    options?: {
      linkType?: string;
      targetEntityType?: string;
    }
  ): Promise<EntityLinkDto[]>;
}

export function createEntityLinksClient(http: HttpClient): EntityLinksClient {
  return {
    async getEntityLinks(entityType, entityId, options) {
      const params = new URLSearchParams({ entityType, entityId });
      if (options?.linkType) params.set('linkType', options.linkType);
      if (options?.targetEntityType) params.set('targetEntityType', options.targetEntityType);

      const data = await http.get(`/api/v1/library/entity-links?${params.toString()}`);
      return z.array(EntityLinkDtoSchema).parse(data);
    },
  };
}
