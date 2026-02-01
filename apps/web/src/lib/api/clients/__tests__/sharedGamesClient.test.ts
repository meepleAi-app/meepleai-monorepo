/**
 * Shared Games Client Tests - Issue #3026 (Frontend 85% Coverage)
 *
 * Coverage: SharedGameCatalog bounded context API client tests
 * - Public: search, getById, getCategories, getMechanics
 * - Admin CRUD: getAll, create, update, publish, archive, delete, bulkImport
 * - Approval workflow: submitForApproval, approvePublication, rejectPublication, getPendingApprovals
 * - Delete workflow: requestDelete, getPendingDeletes, approveDelete, rejectDelete
 * - FAQ: addFaq, updateFaq, deleteFaq
 * - Errata: addErrata, updateErrata, deleteErrata
 * - Documents: getDocuments, getActiveDocuments, addDocument, setActiveDocument, removeDocument
 * - BGG: searchBgg, checkBggDuplicate, importFromBgg, updateFromBgg
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';

import { createSharedGamesClient } from '../sharedGamesClient';
import type { HttpClient } from '../../core/httpClient';

const mockHttpClient: HttpClient = {
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
  delete: vi.fn(),
  patch: vi.fn(),
} as HttpClient;

const mockGame = {
  id: 'game-123',
  title: 'Catan',
  description: 'Classic trading game',
  status: 2, // Published
  minPlayers: 3,
  maxPlayers: 4,
  playingTime: 90,
  imageUrl: '/games/catan.jpg',
};

describe('SharedGamesClient - Issue #3026', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Public Endpoints', () => {
    describe('search', () => {
      it('should search games without filters', async () => {
        const mockResponse = { items: [mockGame], total: 1, page: 1, pageSize: 20 };
        vi.mocked(mockHttpClient.get).mockResolvedValue(mockResponse);

        const client = createSharedGamesClient({ httpClient: mockHttpClient });
        const result = await client.search();

        expect(result).toEqual(mockResponse);
        expect(mockHttpClient.get).toHaveBeenCalledWith('/api/v1/shared-games', expect.any(Object));
      });

      it('should search with all filters', async () => {
        vi.mocked(mockHttpClient.get).mockResolvedValue({ items: [], total: 0, page: 1, pageSize: 20 });

        const client = createSharedGamesClient({ httpClient: mockHttpClient });
        await client.search({
          searchTerm: 'catan',
          categoryIds: 'cat-1,cat-2',
          mechanicIds: 'mech-1',
          minPlayers: 2,
          maxPlayers: 6,
          maxPlayingTime: 120,
          status: 2,
          page: 2,
          pageSize: 10,
          sortBy: 'title',
          sortDescending: false,
        });

        expect(mockHttpClient.get).toHaveBeenCalledWith(
          expect.stringContaining('searchTerm=catan'),
          expect.any(Object)
        );
        expect(mockHttpClient.get).toHaveBeenCalledWith(
          expect.stringContaining('minPlayers=2'),
          expect.any(Object)
        );
      });

      it('should return empty result when API returns null', async () => {
        vi.mocked(mockHttpClient.get).mockResolvedValue(null);

        const client = createSharedGamesClient({ httpClient: mockHttpClient });
        const result = await client.search();

        expect(result.items).toEqual([]);
        expect(result.total).toBe(0);
      });
    });

    describe('getById', () => {
      it('should get game by ID', async () => {
        vi.mocked(mockHttpClient.get).mockResolvedValue(mockGame);

        const client = createSharedGamesClient({ httpClient: mockHttpClient });
        const result = await client.getById('game-123');

        expect(result).toEqual(mockGame);
        expect(mockHttpClient.get).toHaveBeenCalledWith(
          '/api/v1/shared-games/game-123',
          expect.any(Object)
        );
      });

      it('should return null for non-existent game', async () => {
        vi.mocked(mockHttpClient.get).mockResolvedValue(null);

        const client = createSharedGamesClient({ httpClient: mockHttpClient });
        const result = await client.getById('nonexistent');

        expect(result).toBeNull();
      });
    });

    describe('getCategories', () => {
      it('should get all categories', async () => {
        const mockCategories = [
          { id: 'cat-1', name: 'Strategy' },
          { id: 'cat-2', name: 'Family' },
        ];
        vi.mocked(mockHttpClient.get).mockResolvedValue(mockCategories);

        const client = createSharedGamesClient({ httpClient: mockHttpClient });
        const result = await client.getCategories();

        expect(result).toEqual(mockCategories);
      });

      it('should return empty array when no categories', async () => {
        vi.mocked(mockHttpClient.get).mockResolvedValue(null);

        const client = createSharedGamesClient({ httpClient: mockHttpClient });
        const result = await client.getCategories();

        expect(result).toEqual([]);
      });
    });

    describe('getMechanics', () => {
      it('should get all mechanics', async () => {
        const mockMechanics = [
          { id: 'mech-1', name: 'Dice Rolling' },
          { id: 'mech-2', name: 'Hand Management' },
        ];
        vi.mocked(mockHttpClient.get).mockResolvedValue(mockMechanics);

        const client = createSharedGamesClient({ httpClient: mockHttpClient });
        const result = await client.getMechanics();

        expect(result).toEqual(mockMechanics);
      });
    });
  });

  describe('Admin CRUD Endpoints', () => {
    describe('getAll', () => {
      it('should get all games for admin', async () => {
        const mockResponse = { items: [mockGame], total: 1, page: 1, pageSize: 20 };
        vi.mocked(mockHttpClient.get).mockResolvedValue(mockResponse);

        const client = createSharedGamesClient({ httpClient: mockHttpClient });
        const result = await client.getAll();

        expect(result).toEqual(mockResponse);
        expect(mockHttpClient.get).toHaveBeenCalledWith(
          '/api/v1/admin/shared-games',
          expect.any(Object)
        );
      });

      it('should filter by status', async () => {
        vi.mocked(mockHttpClient.get).mockResolvedValue({ items: [], total: 0, page: 1, pageSize: 20 });

        const client = createSharedGamesClient({ httpClient: mockHttpClient });
        await client.getAll({ status: 0, page: 1, pageSize: 50 });

        expect(mockHttpClient.get).toHaveBeenCalledWith(
          expect.stringContaining('status=0'),
          expect.any(Object)
        );
      });
    });

    describe('create', () => {
      it('should create a new game', async () => {
        vi.mocked(mockHttpClient.post).mockResolvedValue({ id: 'new-game-123' });

        const client = createSharedGamesClient({ httpClient: mockHttpClient });
        const result = await client.create({
          title: 'New Game',
          description: 'A new game',
          minPlayers: 2,
          maxPlayers: 4,
        });

        expect(result).toBe('new-game-123');
        expect(mockHttpClient.post).toHaveBeenCalledWith(
          '/api/v1/admin/shared-games',
          expect.objectContaining({ title: 'New Game' }),
          expect.any(Object)
        );
      });
    });

    describe('update', () => {
      it('should update an existing game', async () => {
        vi.mocked(mockHttpClient.put).mockResolvedValue(undefined);

        const client = createSharedGamesClient({ httpClient: mockHttpClient });
        await client.update('game-123', { title: 'Updated Title' });

        expect(mockHttpClient.put).toHaveBeenCalledWith(
          '/api/v1/admin/shared-games/game-123',
          { title: 'Updated Title' }
        );
      });
    });

    describe('publish', () => {
      it('should publish a draft game', async () => {
        vi.mocked(mockHttpClient.post).mockResolvedValue(undefined);

        const client = createSharedGamesClient({ httpClient: mockHttpClient });
        await client.publish('game-123');

        expect(mockHttpClient.post).toHaveBeenCalledWith(
          '/api/v1/admin/shared-games/game-123/publish',
          {}
        );
      });
    });

    describe('archive', () => {
      it('should archive a game', async () => {
        vi.mocked(mockHttpClient.post).mockResolvedValue(undefined);

        const client = createSharedGamesClient({ httpClient: mockHttpClient });
        await client.archive('game-123');

        expect(mockHttpClient.post).toHaveBeenCalledWith(
          '/api/v1/admin/shared-games/game-123/archive',
          {}
        );
      });
    });

    describe('delete', () => {
      it('should delete a game immediately', async () => {
        vi.mocked(mockHttpClient.delete).mockResolvedValue(undefined);

        const client = createSharedGamesClient({ httpClient: mockHttpClient });
        await client.delete('game-123');

        expect(mockHttpClient.delete).toHaveBeenCalledWith('/api/v1/admin/shared-games/game-123');
      });
    });

    describe('bulkImport', () => {
      it('should bulk import games', async () => {
        const mockResult = { successCount: 5, failureCount: 0, importedIds: ['g1', 'g2', 'g3', 'g4', 'g5'] };
        vi.mocked(mockHttpClient.post).mockResolvedValue(mockResult);

        const client = createSharedGamesClient({ httpClient: mockHttpClient });
        const result = await client.bulkImport([
          { title: 'Game 1', minPlayers: 2, maxPlayers: 4 },
          { title: 'Game 2', minPlayers: 1, maxPlayers: 6 },
        ]);

        expect(result.successCount).toBe(5);
        expect(mockHttpClient.post).toHaveBeenCalledWith(
          '/api/v1/admin/shared-games/bulk-import',
          expect.objectContaining({ games: expect.any(Array) }),
          expect.any(Object)
        );
      });
    });
  });

  describe('Approval Workflow', () => {
    describe('submitForApproval', () => {
      it('should submit game for approval', async () => {
        vi.mocked(mockHttpClient.post).mockResolvedValue(undefined);

        const client = createSharedGamesClient({ httpClient: mockHttpClient });
        await client.submitForApproval('game-123');

        expect(mockHttpClient.post).toHaveBeenCalledWith(
          '/api/v1/admin/shared-games/game-123/submit-for-approval',
          {}
        );
      });
    });

    describe('approvePublication', () => {
      it('should approve game publication', async () => {
        vi.mocked(mockHttpClient.post).mockResolvedValue(undefined);

        const client = createSharedGamesClient({ httpClient: mockHttpClient });
        await client.approvePublication('game-123');

        expect(mockHttpClient.post).toHaveBeenCalledWith(
          '/api/v1/admin/shared-games/game-123/approve-publication',
          {}
        );
      });
    });

    describe('rejectPublication', () => {
      it('should reject game with reason', async () => {
        vi.mocked(mockHttpClient.post).mockResolvedValue(undefined);

        const client = createSharedGamesClient({ httpClient: mockHttpClient });
        await client.rejectPublication('game-123', 'Missing description');

        expect(mockHttpClient.post).toHaveBeenCalledWith(
          '/api/v1/admin/shared-games/game-123/reject-publication',
          { reason: 'Missing description' }
        );
      });
    });

    describe('getPendingApprovals', () => {
      it('should get games pending approval', async () => {
        const mockResponse = { items: [{ ...mockGame, status: 1 }], total: 1, page: 1, pageSize: 20 };
        vi.mocked(mockHttpClient.get).mockResolvedValue(mockResponse);

        const client = createSharedGamesClient({ httpClient: mockHttpClient });
        const result = await client.getPendingApprovals();

        expect(result.items[0].status).toBe(1);
      });
    });
  });

  describe('Delete Request Workflow', () => {
    describe('requestDelete', () => {
      it('should request game deletion', async () => {
        vi.mocked(mockHttpClient.post).mockResolvedValue({ requestId: 'req-123', message: 'Request submitted' });

        const client = createSharedGamesClient({ httpClient: mockHttpClient });
        const result = await client.requestDelete('game-123', { reason: 'Duplicate entry' });

        expect(result.requestId).toBe('req-123');
      });
    });

    describe('getPendingDeletes', () => {
      it('should get pending delete requests', async () => {
        const mockResponse = { items: [{ id: 'req-1', gameId: 'game-123' }], total: 1, page: 1, pageSize: 20 };
        vi.mocked(mockHttpClient.get).mockResolvedValue(mockResponse);

        const client = createSharedGamesClient({ httpClient: mockHttpClient });
        const result = await client.getPendingDeletes();

        expect(result.items).toHaveLength(1);
      });
    });

    describe('approveDelete', () => {
      it('should approve delete request', async () => {
        vi.mocked(mockHttpClient.post).mockResolvedValue(undefined);

        const client = createSharedGamesClient({ httpClient: mockHttpClient });
        await client.approveDelete('req-123', { comment: 'Approved' });

        expect(mockHttpClient.post).toHaveBeenCalledWith(
          '/api/v1/admin/shared-games/approve-delete/req-123',
          { comment: 'Approved' }
        );
      });
    });

    describe('rejectDelete', () => {
      it('should reject delete request with reason', async () => {
        vi.mocked(mockHttpClient.post).mockResolvedValue(undefined);

        const client = createSharedGamesClient({ httpClient: mockHttpClient });
        await client.rejectDelete('req-123', { reason: 'Game is still needed' });

        expect(mockHttpClient.post).toHaveBeenCalledWith(
          '/api/v1/admin/shared-games/reject-delete/req-123',
          { reason: 'Game is still needed' }
        );
      });
    });
  });

  describe('FAQ Operations', () => {
    describe('addFaq', () => {
      it('should add FAQ to game', async () => {
        vi.mocked(mockHttpClient.post).mockResolvedValue({ id: 'faq-123' });

        const client = createSharedGamesClient({ httpClient: mockHttpClient });
        const result = await client.addFaq('game-123', {
          question: 'How many players?',
          answer: '2-4 players',
        });

        expect(result).toBe('faq-123');
      });
    });

    describe('updateFaq', () => {
      it('should update FAQ', async () => {
        vi.mocked(mockHttpClient.put).mockResolvedValue(undefined);

        const client = createSharedGamesClient({ httpClient: mockHttpClient });
        await client.updateFaq('game-123', 'faq-123', { answer: 'Updated answer' });

        expect(mockHttpClient.put).toHaveBeenCalledWith(
          '/api/v1/admin/shared-games/game-123/faq/faq-123',
          { answer: 'Updated answer' }
        );
      });
    });

    describe('deleteFaq', () => {
      it('should delete FAQ', async () => {
        vi.mocked(mockHttpClient.delete).mockResolvedValue(undefined);

        const client = createSharedGamesClient({ httpClient: mockHttpClient });
        await client.deleteFaq('game-123', 'faq-123');

        expect(mockHttpClient.delete).toHaveBeenCalledWith(
          '/api/v1/admin/shared-games/game-123/faq/faq-123'
        );
      });
    });
  });

  describe('Errata Operations', () => {
    describe('addErrata', () => {
      it('should add errata to game', async () => {
        vi.mocked(mockHttpClient.post).mockResolvedValue({ id: 'errata-123' });

        const client = createSharedGamesClient({ httpClient: mockHttpClient });
        const result = await client.addErrata('game-123', {
          title: 'Rule clarification',
          content: 'The rule should read...',
        });

        expect(result).toBe('errata-123');
      });
    });

    describe('updateErrata', () => {
      it('should update errata', async () => {
        vi.mocked(mockHttpClient.put).mockResolvedValue(undefined);

        const client = createSharedGamesClient({ httpClient: mockHttpClient });
        await client.updateErrata('game-123', 'errata-123', { content: 'Updated content' });

        expect(mockHttpClient.put).toHaveBeenCalledWith(
          '/api/v1/admin/shared-games/game-123/errata/errata-123',
          { content: 'Updated content' }
        );
      });
    });

    describe('deleteErrata', () => {
      it('should delete errata', async () => {
        vi.mocked(mockHttpClient.delete).mockResolvedValue(undefined);

        const client = createSharedGamesClient({ httpClient: mockHttpClient });
        await client.deleteErrata('game-123', 'errata-123');

        expect(mockHttpClient.delete).toHaveBeenCalledWith(
          '/api/v1/admin/shared-games/game-123/errata/errata-123'
        );
      });
    });
  });

  describe('Document Management', () => {
    describe('getDocuments', () => {
      it('should get all documents for game', async () => {
        const mockDocs = [{ id: 'doc-1', name: 'Rulebook' }];
        vi.mocked(mockHttpClient.get).mockResolvedValue(mockDocs);

        const client = createSharedGamesClient({ httpClient: mockHttpClient });
        const result = await client.getDocuments('game-123');

        expect(result).toEqual(mockDocs);
      });

      it('should filter by document type', async () => {
        vi.mocked(mockHttpClient.get).mockResolvedValue([]);

        const client = createSharedGamesClient({ httpClient: mockHttpClient });
        await client.getDocuments('game-123', 1);

        expect(mockHttpClient.get).toHaveBeenCalledWith(
          '/api/v1/admin/shared-games/game-123/documents?type=1',
          expect.any(Object)
        );
      });
    });

    describe('getActiveDocuments', () => {
      it('should get active documents', async () => {
        vi.mocked(mockHttpClient.get).mockResolvedValue([{ id: 'doc-1', isActive: true }]);

        const client = createSharedGamesClient({ httpClient: mockHttpClient });
        await client.getActiveDocuments('game-123');

        expect(mockHttpClient.get).toHaveBeenCalledWith(
          '/api/v1/admin/shared-games/game-123/documents/active',
          expect.any(Object)
        );
      });
    });

    describe('addDocument', () => {
      it('should add document to game', async () => {
        vi.mocked(mockHttpClient.post).mockResolvedValue({ id: 'doc-123' });

        const client = createSharedGamesClient({ httpClient: mockHttpClient });
        const result = await client.addDocument('game-123', {
          name: 'Quick Start Guide',
          type: 2,
          url: 'https://example.com/guide.pdf',
        });

        expect(result.id).toBe('doc-123');
      });
    });

    describe('setActiveDocument', () => {
      it('should set document as active', async () => {
        vi.mocked(mockHttpClient.post).mockResolvedValue(undefined);

        const client = createSharedGamesClient({ httpClient: mockHttpClient });
        await client.setActiveDocument('game-123', 'doc-123');

        expect(mockHttpClient.post).toHaveBeenCalledWith(
          '/api/v1/admin/shared-games/game-123/documents/doc-123/set-active',
          {}
        );
      });
    });

    describe('removeDocument', () => {
      it('should remove document', async () => {
        vi.mocked(mockHttpClient.delete).mockResolvedValue(undefined);

        const client = createSharedGamesClient({ httpClient: mockHttpClient });
        await client.removeDocument('game-123', 'doc-123');

        expect(mockHttpClient.delete).toHaveBeenCalledWith(
          '/api/v1/admin/shared-games/game-123/documents/doc-123'
        );
      });
    });
  });

  describe('BGG Integration', () => {
    describe('searchBgg', () => {
      it('should search BGG', async () => {
        const mockResults = [{ bggId: 13, name: 'Catan', yearPublished: 1995 }];
        vi.mocked(mockHttpClient.get).mockResolvedValue(mockResults);

        const client = createSharedGamesClient({ httpClient: mockHttpClient });
        const result = await client.searchBgg('catan');

        expect(result).toEqual(mockResults);
        expect(mockHttpClient.get).toHaveBeenCalledWith(
          expect.stringContaining('query=catan'),
          expect.any(Object)
        );
      });

      it('should search with exact match', async () => {
        vi.mocked(mockHttpClient.get).mockResolvedValue([]);

        const client = createSharedGamesClient({ httpClient: mockHttpClient });
        await client.searchBgg('Catan', true);

        expect(mockHttpClient.get).toHaveBeenCalledWith(
          expect.stringContaining('exact=true'),
          expect.any(Object)
        );
      });
    });

    describe('checkBggDuplicate', () => {
      it('should check for BGG duplicate', async () => {
        const mockResult = {
          isDuplicate: true,
          existingGameId: 'game-123',
          existingGame: mockGame,
          bggData: { bggId: 13, name: 'Catan' },
        };
        vi.mocked(mockHttpClient.get).mockResolvedValue(mockResult);

        const client = createSharedGamesClient({ httpClient: mockHttpClient });
        const result = await client.checkBggDuplicate(13);

        expect(result.isDuplicate).toBe(true);
        expect(result.existingGameId).toBe('game-123');
      });

      it('should return no duplicate when game not found', async () => {
        vi.mocked(mockHttpClient.get).mockResolvedValue(null);

        const client = createSharedGamesClient({ httpClient: mockHttpClient });
        const result = await client.checkBggDuplicate(999999);

        expect(result.isDuplicate).toBe(false);
      });
    });

    describe('importFromBgg', () => {
      it('should import game from BGG', async () => {
        vi.mocked(mockHttpClient.post).mockResolvedValue({ id: 'imported-game-123' });

        const client = createSharedGamesClient({ httpClient: mockHttpClient });
        const result = await client.importFromBgg(13);

        expect(result).toBe('imported-game-123');
        expect(mockHttpClient.post).toHaveBeenCalledWith(
          '/api/v1/admin/shared-games/import-bgg',
          { bggId: 13 },
          expect.any(Object)
        );
      });
    });

    describe('updateFromBgg', () => {
      it('should update game from BGG', async () => {
        vi.mocked(mockHttpClient.put).mockResolvedValue('game-123');

        const client = createSharedGamesClient({ httpClient: mockHttpClient });
        const result = await client.updateFromBgg('game-123', {
          bggId: 13,
          fieldsToUpdate: ['description', 'playingTime'],
        });

        expect(result).toBe('game-123');
      });
    });
  });
});
