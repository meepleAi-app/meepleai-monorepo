/**
 * LibraryClient — EntityLink Methods Tests (Issue #5142 — Epic A EntityRelationships)
 *
 * Coverage: getEntityLinks, getEntityLinkCount, createEntityLink, deleteEntityLink
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { createLibraryClient } from '../libraryClient';
import type { HttpClient } from '../../core/httpClient';

const mockEntityLinkDto = {
  id: '550e8400-e29b-41d4-a716-446655440000',
  sourceEntityType: 'Game',
  sourceEntityId: '550e8400-e29b-41d4-a716-446655440001',
  targetEntityType: 'Game',
  targetEntityId: '550e8400-e29b-41d4-a716-446655440002',
  linkType: 'RelatedTo',
  isBidirectional: true,
  scope: 'User',
  ownerUserId: '550e8400-e29b-41d4-a716-446655440003',
  metadata: null,
  isAdminApproved: true,
  isBggImported: false,
  createdAt: '2026-02-01T10:00:00Z',
  updatedAt: '2026-02-01T10:00:00Z',
  isOwner: true,
};

describe('libraryClient — EntityLink Methods (Issue #5142)', () => {
  let mockHttpClient: HttpClient;
  let libraryClient: ReturnType<typeof createLibraryClient>;

  beforeEach(() => {
    mockHttpClient = {
      get: vi.fn(),
      post: vi.fn(),
      put: vi.fn(),
      delete: vi.fn(),
      patch: vi.fn(),
    } as unknown as HttpClient;

    libraryClient = createLibraryClient({ httpClient: mockHttpClient });
  });

  // ── getEntityLinks ──────────────────────────────────────────────────────

  describe('getEntityLinks', () => {
    it('calls correct URL with required params', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValueOnce([mockEntityLinkDto]);

      await libraryClient.getEntityLinks({ entityType: 'Game', entityId: 'game-uuid-1' });

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/library/entity-links?entityType=Game&entityId=game-uuid-1',
        expect.anything()
      );
    });

    it('appends linkType when provided', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValueOnce([]);

      await libraryClient.getEntityLinks({
        entityType: 'Game',
        entityId: 'game-uuid-1',
        linkType: 'ExpansionOf',
      });

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/library/entity-links?entityType=Game&entityId=game-uuid-1&linkType=ExpansionOf',
        expect.anything()
      );
    });

    it('returns parsed entity links', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValueOnce([mockEntityLinkDto]);

      const result = await libraryClient.getEntityLinks({
        entityType: 'Game',
        entityId: 'game-uuid-1',
      });

      expect(result).toHaveLength(1);
      expect(result[0].scope).toBe('User');
      expect(result[0].isOwner).toBe(true);
    });

    it('returns empty array when response is null', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValueOnce(null);

      const result = await libraryClient.getEntityLinks({
        entityType: 'Game',
        entityId: 'game-uuid-1',
      });

      expect(result).toEqual([]);
    });

    it('returns empty array for empty list', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValueOnce([]);

      const result = await libraryClient.getEntityLinks({
        entityType: 'Game',
        entityId: 'game-uuid-1',
      });

      expect(result).toEqual([]);
    });

    it('supports all MeepleEntityType values', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue([]);

      const types = [
        'Game',
        'Player',
        'Session',
        'Agent',
        'Document',
        'ChatSession',
        'Event',
        'Toolkit',
      ];
      for (const entityType of types) {
        await libraryClient.getEntityLinks({ entityType, entityId: 'uuid-1' });
        expect(mockHttpClient.get).toHaveBeenCalledWith(
          expect.stringContaining(`entityType=${entityType}`),
          expect.anything()
        );
      }
    });
  });

  // ── getEntityLinkCount ──────────────────────────────────────────────────

  describe('getEntityLinkCount', () => {
    it('calls correct URL with entityType and entityId', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValueOnce({ count: 5 });

      await libraryClient.getEntityLinkCount('Game', 'game-uuid-1');

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/library/entity-links/count?entityType=Game&entityId=game-uuid-1',
        expect.anything()
      );
    });

    it('returns the count number', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValueOnce({ count: 7 });

      const result = await libraryClient.getEntityLinkCount('Game', 'game-uuid-1');

      expect(result).toBe(7);
    });

    it('returns 0 when response is null', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValueOnce(null);

      const result = await libraryClient.getEntityLinkCount('Game', 'game-uuid-1');

      expect(result).toBe(0);
    });

    it('returns 0 when count is zero', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValueOnce({ count: 0 });

      const result = await libraryClient.getEntityLinkCount('Game', 'game-uuid-1');

      expect(result).toBe(0);
    });
  });

  // ── createEntityLink ────────────────────────────────────────────────────

  describe('createEntityLink', () => {
    it('posts to correct URL with request body', async () => {
      vi.mocked(mockHttpClient.post).mockResolvedValueOnce(mockEntityLinkDto);

      const request = {
        sourceEntityType: 'Game' as const,
        sourceEntityId: 'game-uuid-1',
        targetEntityType: 'Game' as const,
        targetEntityId: 'game-uuid-2',
        linkType: 'RelatedTo' as const,
      };

      await libraryClient.createEntityLink(request);

      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/library/entity-links',
        request,
        expect.anything()
      );
    });

    it('returns created EntityLinkDto', async () => {
      vi.mocked(mockHttpClient.post).mockResolvedValueOnce(mockEntityLinkDto);

      const result = await libraryClient.createEntityLink({
        sourceEntityType: 'Game',
        sourceEntityId: 'game-uuid-1',
        targetEntityType: 'Game',
        targetEntityId: 'game-uuid-2',
        linkType: 'RelatedTo',
      });

      expect(result.id).toBe(mockEntityLinkDto.id);
      expect(result.isOwner).toBe(true);
    });

    it('throws when response is null', async () => {
      vi.mocked(mockHttpClient.post).mockResolvedValueOnce(null);

      await expect(
        libraryClient.createEntityLink({
          sourceEntityType: 'Game',
          sourceEntityId: 'game-uuid-1',
          targetEntityType: 'Game',
          targetEntityId: 'game-uuid-2',
          linkType: 'RelatedTo',
        })
      ).rejects.toThrow('Failed to create entity link');
    });

    it('supports metadata field', async () => {
      const withMetadata = { ...mockEntityLinkDto, metadata: '{"note":"test"}' };
      vi.mocked(mockHttpClient.post).mockResolvedValueOnce(withMetadata);

      const result = await libraryClient.createEntityLink({
        sourceEntityType: 'Game',
        sourceEntityId: 'game-uuid-1',
        targetEntityType: 'Game',
        targetEntityId: 'game-uuid-2',
        linkType: 'RelatedTo',
        metadata: '{"note":"test"}',
      });

      expect(result.metadata).toBe('{"note":"test"}');
    });
  });

  // ── deleteEntityLink ────────────────────────────────────────────────────

  describe('deleteEntityLink', () => {
    it('calls delete with correct URL', async () => {
      vi.mocked(mockHttpClient.delete).mockResolvedValueOnce(undefined);
      const linkId = '550e8400-e29b-41d4-a716-446655440099';

      await libraryClient.deleteEntityLink(linkId);

      expect(mockHttpClient.delete).toHaveBeenCalledWith(`/api/v1/library/entity-links/${linkId}`);
    });

    it('resolves without error on successful delete', async () => {
      vi.mocked(mockHttpClient.delete).mockResolvedValueOnce(undefined);

      await expect(
        libraryClient.deleteEntityLink('550e8400-e29b-41d4-a716-446655440099')
      ).resolves.toBeUndefined();
    });
  });
});
