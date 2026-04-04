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

  describe('chat (SSE)', () => {
    const agentId = 'agent-sse-1';
    const threadId = '11111111-1111-1111-1111-111111111111';

    function makeSseResponse(events: object[]): Response {
      const lines = events.map(e => `data: ${JSON.stringify(e)}\n\n`).join('');
      const stream = new ReadableStream({
        start(controller) {
          controller.enqueue(new TextEncoder().encode(lines));
          controller.close();
        },
      });
      return new Response(stream, {
        status: 200,
        headers: { 'Content-Type': 'text/event-stream' },
      });
    }

    beforeEach(() => {
      vi.stubGlobal('fetch', vi.fn());
    });

    afterEach(() => {
      vi.unstubAllGlobals();
    });

    it('should send message without chatThreadId', async () => {
      const events = [
        {
          type: 'stateUpdate',
          data: { state: 'Processing...' },
          timestamp: new Date().toISOString(),
        },
        { type: 'token', data: { token: 'Hello' }, timestamp: new Date().toISOString() },
        { type: 'complete', data: { totalTokens: 10 }, timestamp: new Date().toISOString() },
      ];
      vi.mocked(fetch).mockResolvedValueOnce(makeSseResponse(events));

      const results: object[] = [];
      for await (const event of agentsClient.chat(agentId, 'What are the rules?')) {
        results.push(event);
      }

      expect(fetch).toHaveBeenCalledWith(
        `/api/v1/agents/${agentId}/chat`,
        expect.objectContaining({
          method: 'POST',
          body: JSON.stringify({ message: 'What are the rules?' }),
        })
      );
      expect(results).toHaveLength(3);
      expect(results[0]).toMatchObject({ type: 'stateUpdate' });
      expect(results[2]).toMatchObject({ type: 'complete' });
    });

    it('should send chatThreadId for multi-turn conversations', async () => {
      const events = [
        { type: 'token', data: { token: 'Yes' }, timestamp: new Date().toISOString() },
        { type: 'complete', data: { totalTokens: 5 }, timestamp: new Date().toISOString() },
      ];
      vi.mocked(fetch).mockResolvedValueOnce(makeSseResponse(events));

      const results: object[] = [];
      for await (const event of agentsClient.chat(agentId, 'Follow-up question', {
        chatThreadId: threadId,
      })) {
        results.push(event);
      }

      expect(fetch).toHaveBeenCalledWith(
        `/api/v1/agents/${agentId}/chat`,
        expect.objectContaining({
          body: JSON.stringify({ message: 'Follow-up question', chatThreadId: threadId }),
        })
      );
      expect(results).toHaveLength(2);
    });

    it('should not include chatThreadId in body when not provided', async () => {
      vi.mocked(fetch).mockResolvedValueOnce(
        makeSseResponse([
          { type: 'complete', data: { totalTokens: 1 }, timestamp: new Date().toISOString() },
        ])
      );

      for await (const _ of agentsClient.chat(agentId, 'test')) {
        /* drain */
      }

      const body = JSON.parse((vi.mocked(fetch).mock.calls[0][1] as RequestInit).body as string);
      expect(body).not.toHaveProperty('chatThreadId');
    });

    it('should throw on non-ok HTTP response', async () => {
      vi.mocked(fetch).mockResolvedValueOnce(
        new Response(null, { status: 503, statusText: 'Service Unavailable' })
      );

      await expect(async () => {
        for await (const _ of agentsClient.chat(agentId, 'test')) {
          /* drain */
        }
      }).rejects.toThrow('Chat failed: Service Unavailable');
    });

    it('should abort stream when signal fires', async () => {
      const controller = new AbortController();
      const slowStream = new ReadableStream({
        start() {
          // Never enqueues — simulates a hanging connection
        },
        cancel() {
          /* no-op */
        },
      });
      vi.mocked(fetch).mockResolvedValueOnce(
        new Response(slowStream, { status: 200, headers: { 'Content-Type': 'text/event-stream' } })
      );

      controller.abort();

      const results: object[] = [];
      for await (const event of agentsClient.chat(agentId, 'test', { signal: controller.signal })) {
        results.push(event);
      }

      // Aborted immediately — no events yielded
      expect(results).toHaveLength(0);
    });
  });
});
