/**
 * Library Client Tests - Issue #3026 (Frontend 85% Coverage)
 *
 * Coverage: User library API client tests
 * - Library CRUD: getLibrary, getStats, getQuota, addGame, removeGame, updateEntry, getGameStatus
 * - Game state: updateGameState
 * - Agent config: getAgentConfig, updateAgentConfig, saveAgentConfig
 * - Library sharing: getShareLink, createShareLink, updateShareLink, revokeShareLink, getSharedLibrary
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';

import { createLibraryClient } from '../libraryClient';
import type { HttpClient } from '../../core/httpClient';

const mockHttpClient: HttpClient = {
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
  delete: vi.fn(),
  patch: vi.fn(),
} as HttpClient;

const mockLibraryEntry = {
  id: 'entry-123',
  gameId: 'game-456',
  gameName: 'Catan',
  gameImageUrl: '/games/catan.jpg',
  addedAt: '2024-01-15T10:00:00Z',
  isFavorite: true,
  notes: 'Great family game',
  state: 'Owned',
};

describe('LibraryClient - Issue #3026', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('getLibrary', () => {
    it('should fetch library with default parameters', async () => {
      const mockResponse = {
        items: [mockLibraryEntry],
        page: 1,
        pageSize: 20,
        totalCount: 1,
        totalPages: 1,
        hasNextPage: false,
        hasPreviousPage: false,
      };
      vi.mocked(mockHttpClient.get).mockResolvedValue(mockResponse);

      const client = createLibraryClient({ httpClient: mockHttpClient });
      const result = await client.getLibrary();

      expect(result).toEqual(mockResponse);
      expect(mockHttpClient.get).toHaveBeenCalledWith('/api/v1/library', expect.any(Object));
    });

    it('should apply pagination parameters', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue({
        items: [],
        page: 2,
        pageSize: 10,
        totalCount: 15,
        totalPages: 2,
        hasNextPage: false,
        hasPreviousPage: true,
      });

      const client = createLibraryClient({ httpClient: mockHttpClient });
      await client.getLibrary({ page: 2, pageSize: 10 });

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        expect.stringContaining('page=2'),
        expect.any(Object)
      );
      expect(mockHttpClient.get).toHaveBeenCalledWith(
        expect.stringContaining('pageSize=10'),
        expect.any(Object)
      );
    });

    it('should filter by favorites only', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue({
        items: [],
        page: 1,
        pageSize: 20,
        totalCount: 0,
        totalPages: 0,
        hasNextPage: false,
        hasPreviousPage: false,
      });

      const client = createLibraryClient({ httpClient: mockHttpClient });
      await client.getLibrary({ favoritesOnly: true });

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        expect.stringContaining('favoritesOnly=true'),
        expect.any(Object)
      );
    });

    it('should filter by state', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue({
        items: [],
        page: 1,
        pageSize: 20,
        totalCount: 0,
        totalPages: 0,
        hasNextPage: false,
        hasPreviousPage: false,
      });

      const client = createLibraryClient({ httpClient: mockHttpClient });
      await client.getLibrary({ stateFilter: ['Owned', 'Wishlist'] });

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        expect.stringContaining('stateFilter=Owned'),
        expect.any(Object)
      );
      expect(mockHttpClient.get).toHaveBeenCalledWith(
        expect.stringContaining('stateFilter=Wishlist'),
        expect.any(Object)
      );
    });

    it('should apply sorting', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue({
        items: [],
        page: 1,
        pageSize: 20,
        totalCount: 0,
        totalPages: 0,
        hasNextPage: false,
        hasPreviousPage: false,
      });

      const client = createLibraryClient({ httpClient: mockHttpClient });
      await client.getLibrary({ sortBy: 'addedAt', sortDescending: true });

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        expect.stringContaining('sortBy=addedAt'),
        expect.any(Object)
      );
      expect(mockHttpClient.get).toHaveBeenCalledWith(
        expect.stringContaining('sortDescending=true'),
        expect.any(Object)
      );
    });

    it('should return empty response when API returns null', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue(null);

      const client = createLibraryClient({ httpClient: mockHttpClient });
      const result = await client.getLibrary();

      expect(result.items).toEqual([]);
      expect(result.totalCount).toBe(0);
    });
  });

  describe('getStats', () => {
    it('should fetch library statistics', async () => {
      const mockStats = {
        totalGames: 25,
        favoriteGames: 5,
        oldestAddedAt: '2023-01-01T00:00:00Z',
        newestAddedAt: '2024-01-15T00:00:00Z',
      };
      vi.mocked(mockHttpClient.get).mockResolvedValue(mockStats);

      const client = createLibraryClient({ httpClient: mockHttpClient });
      const result = await client.getStats();

      expect(result).toEqual(mockStats);
      expect(mockHttpClient.get).toHaveBeenCalledWith('/api/v1/library/stats', expect.any(Object));
    });

    it('should return default stats when API returns null', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue(null);

      const client = createLibraryClient({ httpClient: mockHttpClient });
      const result = await client.getStats();

      expect(result.totalGames).toBe(0);
      expect(result.favoriteGames).toBe(0);
    });
  });

  describe('getQuota', () => {
    it('should fetch library quota', async () => {
      const mockQuota = {
        currentCount: 10,
        maxAllowed: 50,
        userTier: 'premium',
        remainingSlots: 40,
        percentageUsed: 20,
      };
      vi.mocked(mockHttpClient.get).mockResolvedValue(mockQuota);

      const client = createLibraryClient({ httpClient: mockHttpClient });
      const result = await client.getQuota();

      expect(result).toEqual(mockQuota);
      expect(mockHttpClient.get).toHaveBeenCalledWith('/api/v1/library/quota', expect.any(Object));
    });

    it('should return free tier quota when API returns null', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue(null);

      const client = createLibraryClient({ httpClient: mockHttpClient });
      const result = await client.getQuota();

      expect(result.maxAllowed).toBe(5);
      expect(result.userTier).toBe('free');
    });
  });

  describe('addGame', () => {
    it('should add game to library', async () => {
      vi.mocked(mockHttpClient.post).mockResolvedValue(mockLibraryEntry);

      const client = createLibraryClient({ httpClient: mockHttpClient });
      const result = await client.addGame('game-456');

      expect(result).toEqual(mockLibraryEntry);
      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/library/games/game-456',
        {},
        expect.any(Object)
      );
    });

    it('should add game with notes and favorite status', async () => {
      vi.mocked(mockHttpClient.post).mockResolvedValue(mockLibraryEntry);

      const client = createLibraryClient({ httpClient: mockHttpClient });
      await client.addGame('game-456', { notes: 'Gift from friend', isFavorite: true });

      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/library/games/game-456',
        { notes: 'Gift from friend', isFavorite: true },
        expect.any(Object)
      );
    });

    it('should throw error when add fails', async () => {
      vi.mocked(mockHttpClient.post).mockResolvedValue(null);

      const client = createLibraryClient({ httpClient: mockHttpClient });

      await expect(client.addGame('game-456')).rejects.toThrow('Failed to add game to library');
    });
  });

  describe('removeGame', () => {
    it('should remove game from library', async () => {
      vi.mocked(mockHttpClient.delete).mockResolvedValue(undefined);

      const client = createLibraryClient({ httpClient: mockHttpClient });
      await client.removeGame('game-456');

      expect(mockHttpClient.delete).toHaveBeenCalledWith('/api/v1/library/games/game-456');
    });
  });

  describe('updateEntry', () => {
    it('should update library entry', async () => {
      const updatedEntry = { ...mockLibraryEntry, notes: 'Updated notes' };
      vi.mocked(mockHttpClient.patch).mockResolvedValue(updatedEntry);

      const client = createLibraryClient({ httpClient: mockHttpClient });
      const result = await client.updateEntry('game-456', { notes: 'Updated notes' });

      expect(result.notes).toBe('Updated notes');
      expect(mockHttpClient.patch).toHaveBeenCalledWith(
        '/api/v1/library/games/game-456',
        { notes: 'Updated notes' },
        expect.any(Object)
      );
    });

    it('should throw error when update fails', async () => {
      vi.mocked(mockHttpClient.patch).mockResolvedValue(null);

      const client = createLibraryClient({ httpClient: mockHttpClient });

      await expect(client.updateEntry('game-456', { isFavorite: true })).rejects.toThrow(
        'Failed to update library entry'
      );
    });
  });

  describe('getGameStatus', () => {
    it('should get game library status', async () => {
      const mockStatus = { inLibrary: true, isFavorite: true };
      vi.mocked(mockHttpClient.get).mockResolvedValue(mockStatus);

      const client = createLibraryClient({ httpClient: mockHttpClient });
      const result = await client.getGameStatus('game-456');

      expect(result).toEqual(mockStatus);
      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/library/games/game-456/status',
        expect.any(Object)
      );
    });

    it('should return default status when game not in library', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue(null);

      const client = createLibraryClient({ httpClient: mockHttpClient });
      const result = await client.getGameStatus('game-456');

      expect(result.inLibrary).toBe(false);
      expect(result.isFavorite).toBe(false);
    });
  });

  describe('updateGameState', () => {
    it('should update game state', async () => {
      vi.mocked(mockHttpClient.put).mockResolvedValue(undefined);

      const client = createLibraryClient({ httpClient: mockHttpClient });
      await client.updateGameState('game-456', { state: 'Wishlist', notes: 'Birthday gift idea' });

      expect(mockHttpClient.put).toHaveBeenCalledWith('/api/v1/library/games/game-456/state', {
        state: 'Wishlist',
        notes: 'Birthday gift idea',
      });
    });
  });

  describe('Agent Configuration', () => {
    const mockAgentConfig = {
      id: 'config-123',
      gameId: 'game-456',
      agentDefinitionId: 'strategy',
      modelName: 'gpt-4',
      costEstimate: 0.05,
    };

    describe('getAgentConfig', () => {
      it('should get agent configuration', async () => {
        vi.mocked(mockHttpClient.get).mockResolvedValue(mockAgentConfig);

        const client = createLibraryClient({ httpClient: mockHttpClient });
        const result = await client.getAgentConfig('game-456');

        expect(result).toEqual(mockAgentConfig);
        expect(mockHttpClient.get).toHaveBeenCalledWith(
          '/api/v1/library/games/game-456/agent-config',
          expect.any(Object)
        );
      });

      it('should return null when no config exists', async () => {
        vi.mocked(mockHttpClient.get).mockResolvedValue(null);

        const client = createLibraryClient({ httpClient: mockHttpClient });
        const result = await client.getAgentConfig('game-456');

        expect(result).toBeNull();
      });
    });

    describe('updateAgentConfig', () => {
      it('should update agent configuration', async () => {
        const updatedConfig = { ...mockAgentConfig, modelName: 'gpt-4-turbo' };
        vi.mocked(mockHttpClient.put).mockResolvedValue(updatedConfig);

        const client = createLibraryClient({ httpClient: mockHttpClient });
        const result = await client.updateAgentConfig('game-456', { modelName: 'gpt-4-turbo' });

        expect(result.modelName).toBe('gpt-4-turbo');
      });

      it('should throw error when update fails', async () => {
        vi.mocked(mockHttpClient.put).mockResolvedValue(null);

        const client = createLibraryClient({ httpClient: mockHttpClient });

        await expect(
          client.updateAgentConfig('game-456', { modelName: 'invalid' })
        ).rejects.toThrow('Failed to update agent configuration');
      });
    });

    describe('saveAgentConfig', () => {
      it('should save agent configuration', async () => {
        const mockResponse = { success: true, configId: 'config-123', message: 'Saved' };
        vi.mocked(mockHttpClient.post).mockResolvedValue(mockResponse);

        const client = createLibraryClient({ httpClient: mockHttpClient });
        const result = await client.saveAgentConfig('game-456', {
          agentDefinitionId: 'strategy',
          modelName: 'gpt-4',
          costEstimate: 0.05,
        });

        expect(result.success).toBe(true);
        expect(mockHttpClient.post).toHaveBeenCalledWith(
          '/api/v1/library/games/game-456/agent-config',
          expect.objectContaining({ agentDefinitionId: 'strategy' }),
          expect.any(Object)
        );
      });

      it('should throw error when save fails', async () => {
        vi.mocked(mockHttpClient.post).mockResolvedValue(null);

        const client = createLibraryClient({ httpClient: mockHttpClient });

        await expect(
          client.saveAgentConfig('game-456', {
            agentDefinitionId: 'strategy',
            modelName: 'gpt-4',
            costEstimate: 0.05,
          })
        ).rejects.toThrow('Failed to save agent configuration');
      });
    });
  });

  describe('Library Sharing', () => {
    const mockShareLink = {
      shareToken: 'abc123',
      createdAt: '2024-01-15T10:00:00Z',
      expiresAt: '2024-02-15T10:00:00Z',
      isActive: true,
      accessCount: 5,
    };

    describe('getShareLink', () => {
      it('should get active share link', async () => {
        vi.mocked(mockHttpClient.get).mockResolvedValue(mockShareLink);

        const client = createLibraryClient({ httpClient: mockHttpClient });
        const result = await client.getShareLink();

        expect(result).toEqual(mockShareLink);
        expect(mockHttpClient.get).toHaveBeenCalledWith(
          '/api/v1/library/share',
          expect.any(Object)
        );
      });

      it('should return null when no share link exists', async () => {
        vi.mocked(mockHttpClient.get).mockResolvedValue(null);

        const client = createLibraryClient({ httpClient: mockHttpClient });
        const result = await client.getShareLink();

        expect(result).toBeNull();
      });
    });

    describe('createShareLink', () => {
      it('should create new share link', async () => {
        vi.mocked(mockHttpClient.post).mockResolvedValue(mockShareLink);

        const client = createLibraryClient({ httpClient: mockHttpClient });
        const result = await client.createShareLink({ expiresInDays: 30 });

        expect(result).toEqual(mockShareLink);
        expect(mockHttpClient.post).toHaveBeenCalledWith(
          '/api/v1/library/share',
          { expiresInDays: 30 },
          expect.any(Object)
        );
      });

      it('should throw error when creation fails', async () => {
        vi.mocked(mockHttpClient.post).mockResolvedValue(null);

        const client = createLibraryClient({ httpClient: mockHttpClient });

        await expect(client.createShareLink({ expiresInDays: 30 })).rejects.toThrow(
          'Failed to create share link'
        );
      });
    });

    describe('updateShareLink', () => {
      it('should update share link settings', async () => {
        const updatedLink = { ...mockShareLink, expiresAt: '2024-03-15T10:00:00Z' };
        vi.mocked(mockHttpClient.patch).mockResolvedValue(updatedLink);

        const client = createLibraryClient({ httpClient: mockHttpClient });
        const result = await client.updateShareLink('abc123', { expiresInDays: 60 });

        expect(result.expiresAt).toBe('2024-03-15T10:00:00Z');
      });

      it('should throw error when update fails', async () => {
        vi.mocked(mockHttpClient.patch).mockResolvedValue(null);

        const client = createLibraryClient({ httpClient: mockHttpClient });

        await expect(client.updateShareLink('abc123', { expiresInDays: 60 })).rejects.toThrow(
          'Failed to update share link'
        );
      });
    });

    describe('revokeShareLink', () => {
      it('should revoke share link', async () => {
        vi.mocked(mockHttpClient.delete).mockResolvedValue(undefined);

        const client = createLibraryClient({ httpClient: mockHttpClient });
        await client.revokeShareLink('abc123');

        expect(mockHttpClient.delete).toHaveBeenCalledWith('/api/v1/library/share/abc123');
      });
    });

    describe('getSharedLibrary', () => {
      it('should get shared library by token', async () => {
        const mockSharedLibrary = {
          ownerName: 'John',
          games: [mockLibraryEntry],
          shareDate: '2024-01-15T10:00:00Z',
        };
        vi.mocked(mockHttpClient.get).mockResolvedValue(mockSharedLibrary);

        const client = createLibraryClient({ httpClient: mockHttpClient });
        const result = await client.getSharedLibrary('abc123');

        expect(result).toEqual(mockSharedLibrary);
        expect(mockHttpClient.get).toHaveBeenCalledWith(
          '/api/v1/library/shared/abc123',
          expect.any(Object)
        );
      });

      it('should return null for invalid token', async () => {
        vi.mocked(mockHttpClient.get).mockResolvedValue(null);

        const client = createLibraryClient({ httpClient: mockHttpClient });
        const result = await client.getSharedLibrary('invalid');

        expect(result).toBeNull();
      });
    });
  });

  describe('Game Labels (Issue #3512)', () => {
    const mockLabel = {
      id: 'label-123',
      name: 'Strategy',
      color: '#3b82f6',
      isPredefined: true,
      createdAt: '2024-01-15T10:00:00Z',
    };

    const mockCustomLabel = {
      id: 'label-456',
      name: 'My Custom',
      color: '#22c55e',
      isPredefined: false,
      createdAt: '2024-01-16T10:00:00Z',
    };

    describe('getLabels', () => {
      it('should fetch all available labels', async () => {
        const mockLabels = [mockLabel, mockCustomLabel];
        vi.mocked(mockHttpClient.get).mockResolvedValue(mockLabels);

        const client = createLibraryClient({ httpClient: mockHttpClient });
        const result = await client.getLabels();

        expect(result).toEqual(mockLabels);
        expect(mockHttpClient.get).toHaveBeenCalledWith(
          '/api/v1/library/labels',
          expect.any(Object)
        );
      });

      it('should return empty array when no labels', async () => {
        vi.mocked(mockHttpClient.get).mockResolvedValue(null);

        const client = createLibraryClient({ httpClient: mockHttpClient });
        const result = await client.getLabels();

        expect(result).toEqual([]);
      });
    });

    describe('getGameLabels', () => {
      it('should fetch labels for specific game', async () => {
        const mockLabels = [mockLabel];
        vi.mocked(mockHttpClient.get).mockResolvedValue(mockLabels);

        const client = createLibraryClient({ httpClient: mockHttpClient });
        const result = await client.getGameLabels('game-456');

        expect(result).toEqual(mockLabels);
        expect(mockHttpClient.get).toHaveBeenCalledWith(
          '/api/v1/library/games/game-456/labels',
          expect.any(Object)
        );
      });

      it('should return empty array when game has no labels', async () => {
        vi.mocked(mockHttpClient.get).mockResolvedValue(null);

        const client = createLibraryClient({ httpClient: mockHttpClient });
        const result = await client.getGameLabels('game-456');

        expect(result).toEqual([]);
      });
    });

    describe('addLabelToGame', () => {
      it('should add label to game', async () => {
        vi.mocked(mockHttpClient.post).mockResolvedValue(mockLabel);

        const client = createLibraryClient({ httpClient: mockHttpClient });
        const result = await client.addLabelToGame('game-456', 'label-123');

        expect(result).toEqual(mockLabel);
        expect(mockHttpClient.post).toHaveBeenCalledWith(
          '/api/v1/library/games/game-456/labels/label-123',
          {},
          expect.any(Object)
        );
      });

      it('should throw error when add fails', async () => {
        vi.mocked(mockHttpClient.post).mockResolvedValue(null);

        const client = createLibraryClient({ httpClient: mockHttpClient });

        await expect(client.addLabelToGame('game-456', 'label-123')).rejects.toThrow(
          'Failed to add label to game'
        );
      });
    });

    describe('removeLabelFromGame', () => {
      it('should remove label from game', async () => {
        vi.mocked(mockHttpClient.delete).mockResolvedValue(undefined);

        const client = createLibraryClient({ httpClient: mockHttpClient });
        await client.removeLabelFromGame('game-456', 'label-123');

        expect(mockHttpClient.delete).toHaveBeenCalledWith(
          '/api/v1/library/games/game-456/labels/label-123'
        );
      });
    });

    describe('createCustomLabel', () => {
      it('should create custom label', async () => {
        vi.mocked(mockHttpClient.post).mockResolvedValue(mockCustomLabel);

        const client = createLibraryClient({ httpClient: mockHttpClient });
        const result = await client.createCustomLabel({
          name: 'My Custom',
          color: '#22c55e',
        });

        expect(result).toEqual(mockCustomLabel);
        expect(mockHttpClient.post).toHaveBeenCalledWith(
          '/api/v1/library/labels',
          { name: 'My Custom', color: '#22c55e' },
          expect.any(Object)
        );
      });

      it('should throw error when creation fails', async () => {
        vi.mocked(mockHttpClient.post).mockResolvedValue(null);

        const client = createLibraryClient({ httpClient: mockHttpClient });

        await expect(client.createCustomLabel({ name: 'Test', color: '#ff0000' })).rejects.toThrow(
          'Failed to create custom label'
        );
      });
    });

    describe('deleteCustomLabel', () => {
      it('should delete custom label', async () => {
        vi.mocked(mockHttpClient.delete).mockResolvedValue(undefined);

        const client = createLibraryClient({ httpClient: mockHttpClient });
        await client.deleteCustomLabel('label-456');

        expect(mockHttpClient.delete).toHaveBeenCalledWith('/api/v1/library/labels/label-456');
      });
    });
  });
});
