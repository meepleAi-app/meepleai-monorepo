/**
 * Comprehensive Tests for Games Client (Issue #2309)
 *
 * Coverage target: 90%+ (current: 11.76%)
 * Tests: All 22 client methods with success/error scenarios
 */

import {
  createGamesClient,
  type CreateGameRequest,
  type GameFilters,
  type GameSortOptions,
} from '../gamesClient';
import type { HttpClient } from '../../core/httpClient';
import type {
  Game,
  AgentDto,
  RuleSpec,
  GameSessionDto,
  PdfDocumentDto,
  GameFAQ,
} from '../../schemas';

describe('gamesClient - Comprehensive (Issue #2309)', () => {
  let mockHttpClient: Mocked<HttpClient>;
  let gamesClient: ReturnType<typeof createGamesClient>;

  beforeEach(() => {
    mockHttpClient = {
      get: vi.fn(),
      post: vi.fn(),
      put: vi.fn(),
      delete: vi.fn(),
      postFile: vi.fn(),
      baseUrl: 'http://localhost:8080',
    } as any;

    gamesClient = createGamesClient({ httpClient: mockHttpClient });
  });

  // ========== getAll Tests ==========
  describe('getAll', () => {
    const mockGames: Game[] = [
      {
        id: 'game-1',
        title: 'Chess',
        publisher: 'Classic Games',
        yearPublished: 1475,
        minPlayers: 2,
        maxPlayers: 2,
        minPlayTimeMinutes: 30,
        maxPlayTimeMinutes: 60,
        bggId: 171,
        iconUrl: null,
        imageUrl: null,
        createdAt: '2024-01-01',
      },
      {
        id: 'game-2',
        title: 'Catan',
        publisher: 'Kosmos',
        yearPublished: 1995,
        minPlayers: 3,
        maxPlayers: 4,
        minPlayTimeMinutes: 60,
        maxPlayTimeMinutes: 120,
        bggId: 13,
        iconUrl: null,
        imageUrl: null,
        createdAt: '2024-01-02',
      },
      {
        id: 'game-3',
        title: 'Monopoly',
        publisher: 'Hasbro',
        yearPublished: 1935,
        minPlayers: 2,
        maxPlayers: 6,
        minPlayTimeMinutes: 60,
        maxPlayTimeMinutes: 180,
        bggId: 1406,
        iconUrl: null,
        imageUrl: null,
        createdAt: '2024-01-03',
      },
    ];

    // Helper to create mock paginated response
    const createMockPaginatedResponse = (games: Game[]) => ({
      games,
      total: games.length,
      page: 1,
      pageSize: 20,
      totalPages: Math.ceil(games.length / 20),
    });

    it('should fetch all games without filters', async () => {
      mockHttpClient.get.mockResolvedValueOnce(createMockPaginatedResponse(mockGames));

      const result = await gamesClient.getAll();

      expect(mockHttpClient.get).toHaveBeenCalledWith('/api/v1/games', expect.anything());
      expect(result.games).toHaveLength(3);
      expect(result.total).toBe(3);
      expect(result.page).toBe(1);
      expect(result.pageSize).toBe(20);
      expect(result.totalPages).toBe(1);
    });

    it('should filter by search term (title)', async () => {
      mockHttpClient.get.mockResolvedValueOnce(createMockPaginatedResponse(mockGames));

      const result = await gamesClient.getAll({ search: 'chess' });

      expect(result.games).toHaveLength(1);
      expect(result.games[0].title).toBe('Chess');
    });

    it('should filter by search term (publisher)', async () => {
      mockHttpClient.get.mockResolvedValueOnce(createMockPaginatedResponse(mockGames));

      const result = await gamesClient.getAll({ search: 'kosmos' });

      expect(result.games).toHaveLength(1);
      expect(result.games[0].title).toBe('Catan');
    });

    it('should filter by minPlayers', async () => {
      mockHttpClient.get.mockResolvedValueOnce(createMockPaginatedResponse(mockGames));

      const result = await gamesClient.getAll({ minPlayers: 3 });

      // Should return games with maxPlayers >= 3
      expect(result.games).toHaveLength(2); // Catan (3-4), Monopoly (2-6)
    });

    it('should filter by maxPlayers', async () => {
      mockHttpClient.get.mockResolvedValueOnce(createMockPaginatedResponse(mockGames));

      const result = await gamesClient.getAll({ maxPlayers: 3 });

      // Should return games with minPlayers <= 3
      expect(result.games).toHaveLength(3); // All games
    });

    it('should filter by minPlayTime', async () => {
      mockHttpClient.get.mockResolvedValueOnce(createMockPaginatedResponse(mockGames));

      const result = await gamesClient.getAll({ minPlayTime: 60 });

      // Games with maxPlayTime >= 60
      expect(result.games).toHaveLength(3); // All have maxPlayTime >= 60
    });

    it('should filter by maxPlayTime', async () => {
      mockHttpClient.get.mockResolvedValueOnce(createMockPaginatedResponse(mockGames));

      const result = await gamesClient.getAll({ maxPlayTime: 90 });

      // Games with minPlayTime <= 90
      expect(result.games).toHaveLength(3);
    });

    it('should filter by yearFrom', async () => {
      mockHttpClient.get.mockResolvedValueOnce(createMockPaginatedResponse(mockGames));

      const result = await gamesClient.getAll({ yearFrom: 1990 });

      expect(result.games).toHaveLength(1); // Only Catan (1995)
      expect(result.games[0].title).toBe('Catan');
    });

    it('should filter by yearTo', async () => {
      mockHttpClient.get.mockResolvedValueOnce(createMockPaginatedResponse(mockGames));

      const result = await gamesClient.getAll({ yearTo: 1950 });

      expect(result.games).toHaveLength(2); // Chess (1475), Monopoly (1935)
    });

    it('should filter by bggOnly', async () => {
      const gamesWithNoBgg = [...mockGames, { ...mockGames[0], id: 'game-4', bggId: null }];
      mockHttpClient.get.mockResolvedValueOnce(createMockPaginatedResponse(gamesWithNoBgg));

      const result = await gamesClient.getAll({ bggOnly: true });

      expect(result.games).toHaveLength(3); // All with bggId
      expect(result.games.every(g => g.bggId !== null)).toBe(true);
    });

    it('should apply multiple filters simultaneously', async () => {
      mockHttpClient.get.mockResolvedValueOnce(createMockPaginatedResponse(mockGames));

      const result = await gamesClient.getAll({
        minPlayers: 2,
        maxPlayers: 4,
        yearFrom: 1990,
      });

      expect(result.games).toHaveLength(1); // Only Catan matches all filters
      expect(result.games[0].title).toBe('Catan');
    });

    it('should sort by title ascending', async () => {
      mockHttpClient.get.mockResolvedValueOnce(createMockPaginatedResponse(mockGames));

      const result = await gamesClient.getAll(undefined, { field: 'title', direction: 'asc' });

      expect(result.games[0].title).toBe('Catan');
      expect(result.games[1].title).toBe('Chess');
      expect(result.games[2].title).toBe('Monopoly');
    });

    it('should sort by title descending', async () => {
      mockHttpClient.get.mockResolvedValueOnce(createMockPaginatedResponse(mockGames));

      const result = await gamesClient.getAll(undefined, { field: 'title', direction: 'desc' });

      expect(result.games[0].title).toBe('Monopoly');
      expect(result.games[1].title).toBe('Chess');
      expect(result.games[2].title).toBe('Catan');
    });

    it('should sort by yearPublished ascending', async () => {
      mockHttpClient.get.mockResolvedValueOnce(createMockPaginatedResponse(mockGames));

      const result = await gamesClient.getAll(undefined, {
        field: 'yearPublished',
        direction: 'asc',
      });

      expect(result.games[0].yearPublished).toBe(1475); // Chess
      expect(result.games[1].yearPublished).toBe(1935); // Monopoly
      expect(result.games[2].yearPublished).toBe(1995); // Catan
    });

    it('should handle null values in sorting', async () => {
      const gamesWithNulls = [...mockGames, { ...mockGames[0], id: 'game-4', yearPublished: null }];
      mockHttpClient.get.mockResolvedValueOnce(createMockPaginatedResponse(gamesWithNulls));

      const result = await gamesClient.getAll(undefined, {
        field: 'yearPublished',
        direction: 'asc',
      });

      // Null should be pushed to end
      expect(result.games[result.games.length - 1].yearPublished).toBeNull();
    });

    it('should paginate results correctly', async () => {
      mockHttpClient.get.mockResolvedValueOnce(createMockPaginatedResponse(mockGames));

      const result = await gamesClient.getAll(undefined, undefined, 1, 2);

      expect(result.games).toHaveLength(2);
      expect(result.page).toBe(1);
      expect(result.pageSize).toBe(2);
      expect(result.total).toBe(3);
      expect(result.totalPages).toBe(2);
    });

    it('should handle page 2 pagination', async () => {
      mockHttpClient.get.mockResolvedValueOnce(createMockPaginatedResponse(mockGames));

      const result = await gamesClient.getAll(undefined, undefined, 2, 2);

      expect(result.games).toHaveLength(1);
      expect(result.games[0].title).toBe('Monopoly');
      expect(result.page).toBe(2);
    });

    it('should handle empty results', async () => {
      mockHttpClient.get.mockResolvedValueOnce(createMockPaginatedResponse([]));

      const result = await gamesClient.getAll();

      expect(result.games).toEqual([]);
      expect(result.total).toBe(0);
      expect(result.totalPages).toBe(0);
    });

    it('should handle null response', async () => {
      mockHttpClient.get.mockResolvedValueOnce(null);

      const result = await gamesClient.getAll();

      expect(result.games).toEqual([]);
      expect(result.total).toBe(0);
    });
  });

  // ========== getById Tests ==========
  describe('getById', () => {
    it('should fetch game by ID successfully', async () => {
      const mockGame: Game = {
        id: 'game-1',
        title: 'Chess',
        publisher: 'Classic',
        yearPublished: 1475,
        minPlayers: 2,
        maxPlayers: 2,
        minPlayTimeMinutes: 30,
        maxPlayTimeMinutes: 60,
        bggId: 171,
        iconUrl: null,
        imageUrl: null,
        createdAt: '2024-01-01',
      };

      mockHttpClient.get.mockResolvedValueOnce(mockGame);

      const result = await gamesClient.getById('game-1');

      expect(mockHttpClient.get).toHaveBeenCalledWith('/api/v1/games/game-1', expect.anything());
      expect(result).toEqual(mockGame);
    });

    it('should return null when game not found', async () => {
      mockHttpClient.get.mockResolvedValueOnce(null);

      const result = await gamesClient.getById('non-existent');

      expect(result).toBeNull();
    });

    it('should encode special characters in ID', async () => {
      mockHttpClient.get.mockResolvedValueOnce(null);

      await gamesClient.getById('game-with-special/chars');

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/games/game-with-special%2Fchars',
        expect.anything()
      );
    });
  });

  // ========== getSessions Tests ==========
  describe('getSessions', () => {
    it('should fetch sessions without pagination', async () => {
      const mockSessions: GameSessionDto[] = [
        { id: 'session-1', gameId: 'game-1', createdAt: '2024-01-01', playerCount: 4 },
      ];

      mockHttpClient.get.mockResolvedValueOnce(mockSessions);

      const result = await gamesClient.getSessions('game-1');

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/games/game-1/sessions',
        expect.anything()
      );
      expect(result).toEqual(mockSessions);
    });

    it('should fetch sessions with pagination params', async () => {
      mockHttpClient.get.mockResolvedValueOnce([]);

      await gamesClient.getSessions('game-1', 2, 10);

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/games/game-1/sessions?pageNumber=2&pageSize=10',
        expect.anything()
      );
    });

    it('should return empty array when no sessions found', async () => {
      mockHttpClient.get.mockResolvedValueOnce(null);

      const result = await gamesClient.getSessions('game-1');

      expect(result).toEqual([]);
    });
  });

  // ========== getDocuments Tests ==========
  describe('getDocuments', () => {
    it('should fetch PDF documents for game', async () => {
      const mockDocs: PdfDocumentDto[] = [
        {
          id: 'doc-1',
          gameId: 'game-1',
          fileName: 'rules.pdf',
          uploadedAt: '2024-01-01',
          status: 'Completed',
          fileSizeBytes: 1024000,
        },
      ];

      mockHttpClient.get.mockResolvedValueOnce(mockDocs);

      const result = await gamesClient.getDocuments('game-1');

      expect(mockHttpClient.get).toHaveBeenCalledWith('/api/v1/games/game-1/pdfs');
      expect(result).toEqual(mockDocs);
    });

    it('should return empty array when no documents', async () => {
      mockHttpClient.get.mockResolvedValueOnce(null);

      const result = await gamesClient.getDocuments('game-1');

      expect(result).toEqual([]);
    });
  });

  // ========== create Tests ==========
  describe('create', () => {
    it('should create game with full request object', async () => {
      const request: CreateGameRequest = {
        name: 'New Game',
        publisher: 'Publisher',
        yearPublished: 2024,
        minPlayers: 2,
        maxPlayers: 4,
        minPlayTimeMinutes: 30,
        maxPlayTimeMinutes: 60,
        bggId: 12345,
        iconUrl: 'https://example.com/icon.png',
        imageUrl: 'https://example.com/image.jpg',
      };

      const mockResponse = { id: 'new-game-id', title: 'New Game', createdAt: '2024-01-01' };
      mockHttpClient.post.mockResolvedValueOnce(mockResponse);

      const result = await gamesClient.create(request);

      expect(mockHttpClient.post).toHaveBeenCalledWith('/api/v1/games', request);
      expect(result).toEqual(mockResponse);
    });

    it('should create game with legacy string signature', async () => {
      const mockResponse = { id: 'new-id', title: 'Simple Game', createdAt: '2024-01-01' };
      mockHttpClient.post.mockResolvedValueOnce(mockResponse);

      const result = await gamesClient.create('Simple Game');

      expect(mockHttpClient.post).toHaveBeenCalledWith('/api/v1/games', { title: 'Simple Game' });
      expect(result).toEqual(mockResponse);
    });

    it('should create game with minimal fields', async () => {
      const request: CreateGameRequest = { name: 'Minimal Game' };
      const mockResponse = { id: 'id', title: 'Minimal Game', createdAt: '2024-01-01' };
      mockHttpClient.post.mockResolvedValueOnce(mockResponse);

      const result = await gamesClient.create(request);

      expect(result.id).toBeDefined();
      expect(result.title).toBe('Minimal Game');
    });
  });

  // ========== update Tests ==========
  describe('update', () => {
    it('should update game with full updates', async () => {
      const updates: Partial<CreateGameRequest> = {
        name: 'Updated Name',
        publisher: 'New Publisher',
        yearPublished: 2025,
      };

      const mockUpdatedGame: Game = {
        id: 'game-1',
        title: 'Updated Name',
        publisher: 'New Publisher',
        yearPublished: 2025,
        minPlayers: 2,
        maxPlayers: 4,
        minPlayTimeMinutes: 30,
        maxPlayTimeMinutes: 60,
        bggId: null,
        iconUrl: null,
        imageUrl: null,
        createdAt: '2024-01-01',
      };

      mockHttpClient.put.mockResolvedValueOnce(mockUpdatedGame);

      const result = await gamesClient.update('game-1', updates);

      expect(mockHttpClient.put).toHaveBeenCalledWith(
        '/api/v1/games/game-1',
        updates,
        expect.anything()
      );
      expect(result.title).toBe('Updated Name');
    });

    it('should update game with partial updates', async () => {
      const mockGame: Game = {
        id: 'game-1',
        title: 'Game',
        publisher: null,
        yearPublished: null,
        minPlayers: 2,
        maxPlayers: 4,
        minPlayTimeMinutes: null,
        maxPlayTimeMinutes: null,
        bggId: null,
        iconUrl: null,
        imageUrl: null,
        createdAt: '2024-01-01',
      };

      mockHttpClient.put.mockResolvedValueOnce(mockGame);

      const result = await gamesClient.update('game-1', { name: 'Game' });

      expect(result).toBeDefined();
    });
  });

  // ========== uploadImage Tests ==========
  describe('uploadImage', () => {
    beforeEach(() => {
      global.fetch = vi.fn();
    });

    it('should upload icon image successfully', async () => {
      const mockFile = new File(['icon'], 'icon.png', { type: 'image/png' });
      const mockResponse = {
        success: true,
        fileId: 'file-id',
        fileUrl: 'https://cdn.example.com/icon.png',
        fileSizeBytes: 1024,
      };

      (global.fetch as any).mockResolvedValueOnce({
        ok: true,
        json: async () => mockResponse,
      });

      const result = await gamesClient.uploadImage(mockFile, 'game-1', 'icon');

      expect(global.fetch).toHaveBeenCalledWith(
        'http://localhost:8080/api/v1/games/upload-image',
        expect.objectContaining({
          method: 'POST',
          credentials: 'include',
        })
      );
      expect(result.success).toBe(true);
      expect(result.fileUrl).toBe('https://cdn.example.com/icon.png');
    });

    it('should handle upload failure', async () => {
      const mockFile = new File(['data'], 'test.png', { type: 'image/png' });

      (global.fetch as any).mockResolvedValueOnce({
        ok: false,
        status: 400,
        json: async () => ({ message: 'Invalid file format' }),
      });

      const result = await gamesClient.uploadImage(mockFile, 'game-1', 'image');

      expect(result.success).toBe(false);
      expect(result.error).toContain('Invalid file format');
    });

    it('should handle network error during upload', async () => {
      const mockFile = new File(['data'], 'test.png', { type: 'image/png' });

      (global.fetch as any).mockResolvedValueOnce({
        ok: false,
        status: 500,
        json: async () => {
          throw new Error('JSON parse error');
        },
      });

      const result = await gamesClient.uploadImage(mockFile, 'game-1', 'icon');

      expect(result.success).toBe(false);
      expect(result.error).toContain('Upload failed');
    });
  });

  // ========== RuleSpec Methods Tests ==========
  describe('getRuleSpec', () => {
    it('should fetch RuleSpec successfully', async () => {
      const mockRuleSpec: RuleSpec = {
        id: 'rule-1',
        gameId: 'game-1',
        version: '1.0',
        createdAt: '2024-01-01',
        createdByUserId: null,
        parentVersionId: null,
        atoms: [],
      };

      mockHttpClient.get.mockResolvedValueOnce(mockRuleSpec);

      const result = await gamesClient.getRuleSpec('game-1');

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/games/game-1/rulespec',
        expect.anything()
      );
      expect(result).toEqual(mockRuleSpec);
    });

    it('should return null when no RuleSpec exists', async () => {
      mockHttpClient.get.mockResolvedValueOnce(null);

      const result = await gamesClient.getRuleSpec('game-1');

      expect(result).toBeNull();
    });
  });

  describe('updateRuleSpec', () => {
    it('should update RuleSpec successfully', async () => {
      const updates = { version: '2.0', atoms: [] };
      const mockUpdated: RuleSpec = {
        id: 'rule-1',
        gameId: 'game-1',
        version: '2.0',
        createdAt: '2024-01-01',
        createdByUserId: null,
        parentVersionId: null,
        atoms: [],
      };

      mockHttpClient.put.mockResolvedValueOnce(mockUpdated);

      const result = await gamesClient.updateRuleSpec('game-1', updates);

      expect(result.version).toBe('2.0');
    });

    it('should throw error when update fails with no response', async () => {
      mockHttpClient.put.mockResolvedValueOnce(null);

      await expect(gamesClient.updateRuleSpec('game-1', {})).rejects.toThrow(
        'Failed to update RuleSpec: no response from server'
      );
    });
  });

  describe('getAgents', () => {
    it('should fetch agents for game', async () => {
      const mockAgents: AgentDto[] = [
        {
          id: 'agent-1',
          name: 'Rules Expert',
          description: 'Expert',
          modelProvider: 'openai',
          modelName: 'gpt-4',
          temperature: 0.7,
          maxTokens: 1000,
          systemPrompt: 'Prompt',
          isActive: true,
        },
      ];

      mockHttpClient.get.mockResolvedValueOnce(mockAgents);

      const result = await gamesClient.getAgents('game-1');

      expect(result).toEqual(mockAgents);
      expect(result).toHaveLength(1);
    });

    it('should return empty array when no agents', async () => {
      mockHttpClient.get.mockResolvedValueOnce(null);

      const result = await gamesClient.getAgents('game-1');

      expect(result).toEqual([]);
    });
  });

  // ========== FAQ Methods Tests ==========
  describe('getFAQs', () => {
    it('should fetch FAQs with default pagination', async () => {
      const mockResult = {
        faqs: [
          {
            id: 'faq-1',
            question: 'Q1',
            answer: 'A1',
            upvotes: 5,
            gameId: 'game-1',
            createdAt: '2024-01-01',
          },
        ],
        totalCount: 1,
      };

      mockHttpClient.get.mockResolvedValueOnce(mockResult);

      const result = await gamesClient.getFAQs('game-1');

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/games/game-1/faqs?limit=10&offset=0',
        expect.anything()
      );
      expect(result.faqs).toHaveLength(1);
      expect(result.totalCount).toBe(1);
    });

    it('should fetch FAQs with custom pagination', async () => {
      mockHttpClient.get.mockResolvedValueOnce({ faqs: [], totalCount: 0 });

      await gamesClient.getFAQs('game-1', 20, 40);

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/games/game-1/faqs?limit=20&offset=40',
        expect.anything()
      );
    });

    it('should return empty result when null response', async () => {
      mockHttpClient.get.mockResolvedValueOnce(null);

      const result = await gamesClient.getFAQs('game-1');

      expect(result).toEqual({ faqs: [], totalCount: 0 });
    });
  });

  describe('upvoteFAQ', () => {
    it('should upvote FAQ successfully', async () => {
      const mockFAQ: GameFAQ = {
        id: 'faq-1',
        question: 'Question',
        answer: 'Answer',
        upvotes: 6,
        gameId: 'game-1',
        createdAt: '2024-01-01',
      };

      mockHttpClient.post.mockResolvedValueOnce(mockFAQ);

      const result = await gamesClient.upvoteFAQ('faq-1');

      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/faqs/faq-1/upvote',
        {},
        expect.anything()
      );
      expect(result.upvotes).toBe(6);
    });

    it('should throw error when upvote fails with no response', async () => {
      mockHttpClient.post.mockResolvedValueOnce(null);

      await expect(gamesClient.upvoteFAQ('faq-1')).rejects.toThrow(
        'Failed to upvote FAQ: no response from server'
      );
    });
  });

  // ========== Editor Lock Tests ==========
  describe('acquireEditorLock', () => {
    it('should acquire lock successfully', async () => {
      const mockResult = {
        success: true,
        lock: {
          gameId: 'game-1',
          userId: 'user-1',
          acquiredAt: '2024-01-01',
          expiresAt: '2024-01-01',
        },
        message: 'Lock acquired',
      };

      mockHttpClient.post.mockResolvedValueOnce(mockResult);

      const result = await gamesClient.acquireEditorLock('game-1');

      expect(result.success).toBe(true);
      expect(result.lock).toBeDefined();
    });

    it('should return failure when lock unavailable', async () => {
      const mockResult = { success: false, lock: null, message: 'Lock held by another user' };

      mockHttpClient.post.mockResolvedValueOnce(mockResult);

      const result = await gamesClient.acquireEditorLock('game-1');

      expect(result.success).toBe(false);
      expect(result.message).toContain('another user');
    });

    it('should handle null response', async () => {
      mockHttpClient.post.mockResolvedValueOnce(null);

      const result = await gamesClient.acquireEditorLock('game-1');

      expect(result.success).toBe(false);
      expect(result.message).toBe('No response from server');
    });
  });

  describe('releaseEditorLock', () => {
    it('should release lock successfully', async () => {
      mockHttpClient.delete.mockResolvedValueOnce(undefined);

      await expect(gamesClient.releaseEditorLock('game-1')).resolves.not.toThrow();

      expect(mockHttpClient.delete).toHaveBeenCalledWith('/api/v1/games/game-1/rulespec/lock');
    });
  });

  describe('getEditorLockStatus', () => {
    it('should fetch lock status', async () => {
      const mockLock = {
        gameId: 'game-1',
        userId: 'user-1',
        acquiredAt: '2024-01-01',
        expiresAt: '2024-01-02',
      };

      mockHttpClient.get.mockResolvedValueOnce(mockLock);

      const result = await gamesClient.getEditorLockStatus('game-1');

      expect(result).toEqual(mockLock);
    });

    it('should return null when no lock exists', async () => {
      mockHttpClient.get.mockResolvedValueOnce(null);

      const result = await gamesClient.getEditorLockStatus('game-1');

      expect(result).toBeNull();
    });
  });

  describe('refreshEditorLock', () => {
    it('should refresh lock successfully', async () => {
      const mockResult = { success: true, lock: {}, message: 'Lock refreshed' };

      mockHttpClient.post.mockResolvedValueOnce(mockResult);

      const result = await gamesClient.refreshEditorLock('game-1');

      expect(result.success).toBe(true);
    });

    it('should handle refresh failure', async () => {
      mockHttpClient.post.mockResolvedValueOnce(null);

      const result = await gamesClient.refreshEditorLock('game-1');

      expect(result.success).toBe(false);
    });
  });

  describe('updateRuleSpecWithETag', () => {
    it('should update with ETag successfully', async () => {
      const mockRuleSpec: RuleSpec = {
        id: 'rule-1',
        gameId: 'game-1',
        version: '2.0',
        createdAt: '2024-01-01',
        createdByUserId: null,
        parentVersionId: null,
        atoms: [],
      };

      mockHttpClient.put.mockResolvedValueOnce(mockRuleSpec);

      const result = await gamesClient.updateRuleSpecWithETag(
        'game-1',
        { version: '2.0' },
        'etag-123'
      );

      expect(mockHttpClient.put).toHaveBeenCalledWith(
        '/api/v1/games/game-1/rulespec',
        { version: '2.0', expectedETag: 'etag-123' },
        expect.anything()
      );
      expect(result.version).toBe('2.0');
    });

    it('should update without ETag when not provided', async () => {
      const mockRuleSpec: RuleSpec = {
        id: 'rule-1',
        gameId: 'game-1',
        version: '1.5',
        createdAt: '2024-01-01',
        createdByUserId: null,
        parentVersionId: null,
        atoms: [],
      };

      mockHttpClient.put.mockResolvedValueOnce(mockRuleSpec);

      const result = await gamesClient.updateRuleSpecWithETag('game-1', { version: '1.5' });

      expect(mockHttpClient.put).toHaveBeenCalledWith(
        '/api/v1/games/game-1/rulespec',
        { version: '1.5' },
        expect.anything()
      );
      expect(result).toEqual(mockRuleSpec);
    });

    it('should throw error when update fails with no response', async () => {
      mockHttpClient.put.mockResolvedValueOnce(null);

      await expect(gamesClient.updateRuleSpecWithETag('game-1', {})).rejects.toThrow(
        'Failed to update RuleSpec: no response from server'
      );
    });
  });

  // ========== Additional RuleSpec Methods ==========
  describe('getRuleSpecVersion', () => {
    it('should fetch specific version', async () => {
      const mockRuleSpec: RuleSpec = {
        id: 'rule-v2',
        gameId: 'game-1',
        version: '2.0',
        createdAt: '2024-01-01',
        createdByUserId: null,
        parentVersionId: 'rule-v1',
        atoms: [],
      };

      mockHttpClient.get.mockResolvedValueOnce(mockRuleSpec);

      const result = await gamesClient.getRuleSpecVersion('game-1', 2);

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/games/game-1/rulespec/versions/2',
        expect.anything()
      );
      expect(result?.version).toBe('2.0');
    });
  });

  describe('getRuleSpecHistory', () => {
    it('should fetch version history', async () => {
      const mockHistory = {
        currentVersion: '2.0',
        versions: [
          { version: '1.0', createdAt: '2024-01-01' },
          { version: '2.0', createdAt: '2024-01-02' },
        ],
      };

      mockHttpClient.get.mockResolvedValueOnce(mockHistory);

      const result = await gamesClient.getRuleSpecHistory('game-1');

      expect(result).toBeDefined();
    });
  });

  describe('getRuleSpecTimeline', () => {
    it('should fetch timeline with authors', async () => {
      const mockTimeline = {
        versions: [{ version: '1.0', author: 'User 1', createdAt: '2024-01-01' }],
      };

      mockHttpClient.get.mockResolvedValueOnce(mockTimeline);

      const result = await gamesClient.getRuleSpecTimeline('game-1');

      expect(result).toBeDefined();
    });
  });

  describe('getRuleSpecDiff', () => {
    it('should fetch diff between versions', async () => {
      const mockDiff = {
        from: '1.0',
        to: '2.0',
        changes: [],
      };

      mockHttpClient.get.mockResolvedValueOnce(mockDiff);

      const result = await gamesClient.getRuleSpecDiff('game-1', 1, 2);

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/games/game-1/rulespec/diff?from=1&to=2',
        expect.anything()
      );
      expect(result).toBeDefined();
    });

    it('should handle string version parameters', async () => {
      mockHttpClient.get.mockResolvedValueOnce(null);

      await gamesClient.getRuleSpecDiff('game-1', 'v1', 'v2');

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/games/game-1/rulespec/diff?from=v1&to=v2',
        expect.anything()
      );
    });
  });
});
