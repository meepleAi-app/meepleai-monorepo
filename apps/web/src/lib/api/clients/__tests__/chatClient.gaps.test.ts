/**
 * ChatClient - Coverage Gaps Tests (Issue #2309)
 *
 * Coverage gap: 95.2% → 100%
 * Tests: deleteThread, updateThreadTitle (lines 176-177, 180-185)
 */

import { createChatClient } from '../chatClient';
import type { HttpClient } from '../../core/httpClient';

describe('chatClient - Gaps (Issue #2309)', () => {
  let mockHttpClient: Mocked<HttpClient>;
  let chatClient: ReturnType<typeof createChatClient>;

  beforeEach(() => {
    mockHttpClient = {
      get: vi.fn(),
      post: vi.fn(),
      put: vi.fn(),
      delete: vi.fn(),
      patch: vi.fn(),
      postFile: vi.fn(),
    } as any;

    chatClient = createChatClient({ httpClient: mockHttpClient });
  });

  describe('deleteThread', () => {
    it('should delete thread successfully', async () => {
      mockHttpClient.delete.mockResolvedValueOnce(undefined);

      const result = await chatClient.deleteThread('thread-123');

      expect(mockHttpClient.delete).toHaveBeenCalledWith('/api/v1/chat-threads/thread-123');
      expect(result).toBeUndefined();
    });

    it('should handle delete error', async () => {
      mockHttpClient.delete.mockRejectedValueOnce(new Error('Thread not found'));

      await expect(chatClient.deleteThread('invalid')).rejects.toThrow('Thread not found');
    });
  });

  describe('updateThreadTitle', () => {
    it('should update thread title successfully', async () => {
      const mockUpdatedThread = {
        id: 'thread-123',
        title: 'New Title',
        gameId: 'game-1',
        createdAt: '2024-01-01',
        status: 'Active' as const,
      };

      mockHttpClient.patch.mockResolvedValueOnce(mockUpdatedThread);

      const result = await chatClient.updateThreadTitle('thread-123', 'New Title');

      expect(mockHttpClient.patch).toHaveBeenCalledWith(
        '/api/v1/chat-threads/thread-123',
        { title: 'New Title' },
        expect.anything()
      );
      expect(result.title).toBe('New Title');
    });

    it('should encode thread ID in URL', async () => {
      mockHttpClient.patch.mockResolvedValueOnce({
        id: 'thread',
        title: 'Title',
        gameId: 'g1',
        createdAt: '2024-01-01',
        status: 'Active',
      });

      await chatClient.updateThreadTitle('thread-with/special', 'New Title');

      expect(mockHttpClient.patch).toHaveBeenCalledWith(
        expect.stringContaining('thread-with%2Fspecial'),
        { title: 'New Title' },
        expect.anything()
      );
    });

    it('should handle empty title', async () => {
      mockHttpClient.patch.mockResolvedValueOnce({
        id: 'thread',
        title: '',
        gameId: 'g1',
        createdAt: '2024-01-01',
        status: 'Active',
      });

      await chatClient.updateThreadTitle('thread-123', '');

      expect(mockHttpClient.patch).toHaveBeenCalledWith(
        '/api/v1/chat-threads/thread-123',
        { title: '' },
        expect.anything()
      );
    });

    it('should handle update error', async () => {
      mockHttpClient.patch.mockRejectedValueOnce(new Error('Update failed'));

      await expect(chatClient.updateThreadTitle('thread-123', 'New')).rejects.toThrow(
        'Update failed'
      );
    });
  });
});
