/**
 * Comprehensive Tests for Games Client (Issue #2309)
 *
 * Coverage target: 90%+ (current: 11.76%)
 * Tests: All 22 client methods with success/error scenarios
 */

import { createGamesClient, type CreateGameRequest } from '../gamesClient';
import type { HttpClient } from '../../core/httpClient';
import type {
  Game,
  PaginatedGamesResponse,
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
  // Issue #4158: search, page e pageSize vanno al server. Il client non filtra, non ordina e non
  // pagina più una pagina già ricevuta: così vedeva solo i primi 20 giochi del catalogo.
  describe('getAll', () => {
    const serverPage: PaginatedGamesResponse = {
      games: [
        {
          id: 'game-1',
          title: 'Dominion',
          publisher: 'Rio Grande',
          yearPublished: 2008,
          minPlayers: 2,
          maxPlayers: 4,
          minPlayTimeMinutes: 30,
          maxPlayTimeMinutes: 30,
          bggId: 36218,
          iconUrl: null,
          imageUrl: null,
          createdAt: '2024-01-01',
        },
      ],
      total: 165,
      page: 2,
      pageSize: 24,
      totalPages: 7,
    };

    it('sends search, page and pageSize to the server', async () => {
      mockHttpClient.get.mockResolvedValueOnce(serverPage);

      await gamesClient.getAll({ search: 'Dominion' }, 2, 24);

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/games?search=Dominion&page=2&pageSize=24',
        expect.anything()
      );
    });

    it('sends the default page and pageSize when none are given', async () => {
      mockHttpClient.get.mockResolvedValueOnce(serverPage);

      await gamesClient.getAll();

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/games?page=1&pageSize=20',
        expect.anything()
      );
    });

    it('omits a blank search and encodes a multi-word one', async () => {
      mockHttpClient.get.mockResolvedValue(serverPage);

      await gamesClient.getAll({ search: '   ' });
      await gamesClient.getAll({ search: 'Ticket to Ride' });

      expect(mockHttpClient.get).toHaveBeenNthCalledWith(
        1,
        '/api/v1/games?page=1&pageSize=20',
        expect.anything()
      );
      expect(mockHttpClient.get).toHaveBeenNthCalledWith(
        2,
        '/api/v1/games?search=Ticket+to+Ride&page=1&pageSize=20',
        expect.anything()
      );
    });

    it('returns the server page as is, total included', async () => {
      mockHttpClient.get.mockResolvedValueOnce(serverPage);

      const result = await gamesClient.getAll({ search: 'Dominion' }, 2, 24);

      // Nessun ri-filtraggio lato client: il totale è quello del catalogo, non della pagina.
      expect(result).toEqual(serverPage);
    });

    it('returns an empty page when the server answers null', async () => {
      mockHttpClient.get.mockResolvedValueOnce(null);

      const result = await gamesClient.getAll(undefined, 3, 10);

      expect(result).toEqual({ games: [], total: 0, page: 3, pageSize: 10, totalPages: 0 });
    });
  });

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
