/**
 * AgentsClient - Specialized Agents Tests (Issue #2309)
 *
 * Coverage gap: lines 145-155, 166-176
 * Tests: invokeChess
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

  // Issue #4139: il describe `chat (SSE)` stava qui, con cinque test che
  // asserivano `fetch` chiamata su `/api/v1/agents/${agentId}/chat` e il corpo
  // `{ message, chatThreadId }`. Quella rotta il backend non l'ha mai montata:
  // i test confermavano che il client coincideva con se stesso, non col server,
  // ed e' il motivo per cui il difetto e' sopravvissuto a una suite verde.
  // Il metodo `chat()` e' stato rimosso; chi streamma e' `useAgentChatStream`,
  // e i suoi test ora asseriscono il percorso richiesto contro l'elenco delle
  // rotte realmente montate.
});
