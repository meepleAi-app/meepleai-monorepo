/**
 * AgentsClient - Specialized Agents Tests (Issue #2309)
 *
 * Coverage gap: lines 145-155, 166-176
 * Tests: invokeChess, generateSetupGuide
 */

import { createAgentsClient } from '../agentsClient';
import type { HttpClient } from '../../core/httpClient';

describe('agentsClient - Specialized (Issue #2309)', () => {
  let mockHttpClient: Mocked<HttpClient>;
  let agentsClient: ReturnType<typeof createAgentsClient>;

  beforeEach(() => {
    mockHttpClient = {
      get: vi.fn(),
      post: vi.fn(),
      put: vi.fn(),
      delete: vi.fn(),
      patch: vi.fn(),
      postFile: vi.fn(),
    } as any;

    agentsClient = createAgentsClient({ httpClient: mockHttpClient });
  });

  describe('invokeChess', () => {
    it('should invoke chess agent with question only', async () => {
      const mockResponse = {
        answer: 'The best move is e4',
        suggestedMoves: ['e4', 'd4'],
        confidence: 0.95,
      };

      mockHttpClient.post.mockResolvedValueOnce(mockResponse);

      const result = await agentsClient.invokeChess({ question: 'What is the best opening move?' });

      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/agents/chess',
        { question: 'What is the best opening move?' },
        expect.anything()
      );
      expect(result.answer).toBe('The best move is e4');
    });

    it('should invoke chess agent with FEN position', async () => {
      const mockResponse = {
        answer: 'Checkmate in 2 moves',
        suggestedMoves: ['Qh7+'],
        confidence: 1.0,
      };

      mockHttpClient.post.mockResolvedValueOnce(mockResponse);

      const result = await agentsClient.invokeChess({
        question: 'How to checkmate?',
        fenPosition: 'rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1',
      });

      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/agents/chess',
        {
          question: 'How to checkmate?',
          fenPosition: 'rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1',
        },
        expect.anything()
      );
      expect(result.answer).toBe('Checkmate in 2 moves');
    });

    it('should throw error when response is null', async () => {
      mockHttpClient.post.mockResolvedValueOnce(null);

      await expect(agentsClient.invokeChess({ question: 'Test' })).rejects.toThrow(
        'Failed to invoke chess agent: no response from server'
      );
    });

    it('should handle API errors', async () => {
      mockHttpClient.post.mockRejectedValueOnce(new Error('Agent unavailable'));

      await expect(agentsClient.invokeChess({ question: 'Test' })).rejects.toThrow(
        'Agent unavailable'
      );
    });
  });

  describe('generateSetupGuide', () => {
    it('should generate setup guide', async () => {
      const mockResponse = {
        guide: 'Setup instructions for the game...',
        steps: ['Step 1', 'Step 2'],
        estimatedTime: '15 minutes',
      };

      mockHttpClient.post.mockResolvedValueOnce(mockResponse);

      const result = await agentsClient.generateSetupGuide({ gameId: 'game-1', chatId: 'chat-1' });

      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/agents/setup',
        { gameId: 'game-1', chatId: 'chat-1' },
        expect.anything()
      );
      expect(result.guide).toBeDefined();
    });

    it('should generate setup guide without chatId', async () => {
      const mockResponse = {
        guide: 'Basic setup',
        steps: [],
        estimatedTime: '10 minutes',
      };

      mockHttpClient.post.mockResolvedValueOnce(mockResponse);

      const result = await agentsClient.generateSetupGuide({ gameId: 'game-2', chatId: null });

      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/agents/setup',
        { gameId: 'game-2', chatId: null },
        expect.anything()
      );
      expect(result).toEqual(mockResponse);
    });

    it('should throw error when response is null', async () => {
      mockHttpClient.post.mockResolvedValueOnce(null);

      await expect(
        agentsClient.generateSetupGuide({ gameId: 'game-1', chatId: null })
      ).rejects.toThrow('Failed to generate setup guide: no response from server');
    });

    it('should handle API errors', async () => {
      mockHttpClient.post.mockRejectedValueOnce(new Error('Game not found'));

      await expect(
        agentsClient.generateSetupGuide({ gameId: 'invalid', chatId: null })
      ).rejects.toThrow('Game not found');
    });
  });
});
