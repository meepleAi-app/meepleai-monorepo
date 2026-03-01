/**
 * AdminClient — EntityLink Methods Tests (Issue #5142 — Epic A EntityRelationships)
 *
 * Coverage: getAdminEntityLinks, createAdminEntityLink, deleteAdminEntityLink, importBggExpansions
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { createAdminClient } from '../adminClient';
import type { HttpClient } from '../../core/httpClient';

const mockEntityLinkDto = {
  id: '550e8400-e29b-41d4-a716-446655440000',
  sourceEntityType: 'Game',
  sourceEntityId: '550e8400-e29b-41d4-a716-446655440001',
  targetEntityType: 'Game',
  targetEntityId: '550e8400-e29b-41d4-a716-446655440002',
  linkType: 'ExpansionOf',
  isBidirectional: false,
  scope: 'Shared',
  ownerUserId: '550e8400-e29b-41d4-a716-446655440003',
  metadata: null,
  isAdminApproved: true,
  isBggImported: true,
  createdAt: '2026-02-01T10:00:00Z',
  updatedAt: '2026-02-01T10:00:00Z',
  isOwner: false,
};

describe('adminClient — EntityLink Methods (Issue #5142)', () => {
  let mockHttpClient: HttpClient;
  let adminClient: ReturnType<typeof createAdminClient>;

  beforeEach(() => {
    mockHttpClient = {
      get: vi.fn(),
      post: vi.fn(),
      put: vi.fn(),
      delete: vi.fn(),
      patch: vi.fn(),
      postFile: vi.fn(),
    } as unknown as HttpClient;

    adminClient = createAdminClient({ httpClient: mockHttpClient });
  });

  // ── getAdminEntityLinks ─────────────────────────────────────────────────

  describe('getAdminEntityLinks', () => {
    it('calls correct URL with required params', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValueOnce([mockEntityLinkDto]);

      await adminClient.getAdminEntityLinks({ entityType: 'Game', entityId: 'game-uuid-1' });

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/admin/entity-links?sourceType=Game&sourceId=game-uuid-1',
        expect.anything()
      );
    });

    it('appends linkType when provided', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValueOnce([]);

      await adminClient.getAdminEntityLinks({
        entityType: 'Game',
        entityId: 'game-uuid-1',
        linkType: 'ExpansionOf',
      });

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/admin/entity-links?sourceType=Game&sourceId=game-uuid-1&linkType=ExpansionOf',
        expect.anything()
      );
    });

    it('returns parsed entity links array', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValueOnce([mockEntityLinkDto]);

      const result = await adminClient.getAdminEntityLinks({
        entityType: 'Game',
        entityId: 'game-uuid-1',
      });

      expect(result).toHaveLength(1);
      expect(result[0].linkType).toBe('ExpansionOf');
    });

    it('returns empty array when response is null', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValueOnce(null);

      const result = await adminClient.getAdminEntityLinks({
        entityType: 'Game',
        entityId: 'game-uuid-1',
      });

      expect(result).toEqual([]);
    });

    it('returns empty array when response is empty', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValueOnce([]);

      const result = await adminClient.getAdminEntityLinks({
        entityType: 'Game',
        entityId: 'game-uuid-1',
      });

      expect(result).toEqual([]);
    });
  });

  // ── createAdminEntityLink ───────────────────────────────────────────────

  describe('createAdminEntityLink', () => {
    it('posts to correct URL with request body', async () => {
      vi.mocked(mockHttpClient.post).mockResolvedValueOnce(mockEntityLinkDto);

      const request = {
        sourceEntityType: 'Game' as const,
        sourceEntityId: 'game-uuid-1',
        targetEntityType: 'Game' as const,
        targetEntityId: 'game-uuid-2',
        linkType: 'RelatedTo' as const,
      };

      await adminClient.createAdminEntityLink(request);

      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/admin/entity-links',
        request,
        expect.anything()
      );
    });

    it('returns created EntityLinkDto', async () => {
      vi.mocked(mockHttpClient.post).mockResolvedValueOnce(mockEntityLinkDto);

      const result = await adminClient.createAdminEntityLink({
        sourceEntityType: 'Game',
        sourceEntityId: 'game-uuid-1',
        targetEntityType: 'Game',
        targetEntityId: 'game-uuid-2',
        linkType: 'RelatedTo',
      });

      expect(result.id).toBe(mockEntityLinkDto.id);
      expect(result.linkType).toBe('ExpansionOf');
    });

    it('throws when response is null', async () => {
      vi.mocked(mockHttpClient.post).mockResolvedValueOnce(null);

      await expect(
        adminClient.createAdminEntityLink({
          sourceEntityType: 'Game',
          sourceEntityId: 'game-uuid-1',
          targetEntityType: 'Game',
          targetEntityId: 'game-uuid-2',
          linkType: 'RelatedTo',
        })
      ).rejects.toThrow('Failed to create entity link');
    });
  });

  // ── deleteAdminEntityLink ───────────────────────────────────────────────

  describe('deleteAdminEntityLink', () => {
    it('calls delete with correct URL', async () => {
      vi.mocked(mockHttpClient.delete).mockResolvedValueOnce(undefined);
      const linkId = '550e8400-e29b-41d4-a716-446655440099';

      await adminClient.deleteAdminEntityLink(linkId);

      expect(mockHttpClient.delete).toHaveBeenCalledWith(`/api/v1/admin/entity-links/${linkId}`);
    });

    it('resolves without error on successful delete', async () => {
      vi.mocked(mockHttpClient.delete).mockResolvedValueOnce(undefined);

      await expect(
        adminClient.deleteAdminEntityLink('550e8400-e29b-41d4-a716-446655440099')
      ).resolves.toBeUndefined();
    });
  });

  // ── importBggExpansions ─────────────────────────────────────────────────

  describe('importBggExpansions', () => {
    it('posts to correct URL with sharedGameId', async () => {
      vi.mocked(mockHttpClient.post).mockResolvedValueOnce({ created: 3 });
      const sharedGameId = '550e8400-e29b-41d4-a716-446655440010';

      await adminClient.importBggExpansions(sharedGameId);

      expect(mockHttpClient.post).toHaveBeenCalledWith(
        `/api/v1/admin/entity-links/import-bgg/${sharedGameId}`,
        {},
        expect.anything()
      );
    });

    it('returns ImportBggExpansionsResponse with created count', async () => {
      vi.mocked(mockHttpClient.post).mockResolvedValueOnce({ created: 5 });

      const result = await adminClient.importBggExpansions('some-game-uuid');

      expect(result.created).toBe(5);
    });

    it('returns created=0 when no new links', async () => {
      vi.mocked(mockHttpClient.post).mockResolvedValueOnce({ created: 0 });

      const result = await adminClient.importBggExpansions('some-game-uuid');

      expect(result.created).toBe(0);
    });

    it('throws when response is null', async () => {
      vi.mocked(mockHttpClient.post).mockResolvedValueOnce(null);

      await expect(adminClient.importBggExpansions('some-game-uuid')).rejects.toThrow(
        'Failed to import BGG expansions'
      );
    });
  });
});
