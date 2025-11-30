/**
 * Chat Client Tests - Threads & Messages
 *
 * Tests for chat thread operations, messages, and export functionality
 */

// Mock downloadFile before importing chatClient
import { mockDownloadFile } from './chatClient.test-helpers';

vi.mock('../../core/httpClient', async () => {
  const actual = await vi.importActual('../../core/httpClient');
  return {
    ...actual,
    downloadFile: mockDownloadFile,
  };
});

import { createChatClient } from '../chatClient';
import {
  createMockHttpClient,
  createMockThread,
  createMockMessage,
  commonExpectations,
} from './chatClient.test-helpers';

describe('ChatClient - Threads & Messages', () => {
  let mockHttpClient: ReturnType<typeof createMockHttpClient>;
  let chatClient: ReturnType<typeof createChatClient>;

  beforeEach(() => {
    mockHttpClient = createMockHttpClient();
    chatClient = createChatClient({ httpClient: mockHttpClient });
  });

  describe('Chat Threads', () => {
    describe('getThreadsByGame', () => {
      it('should fetch threads for a game', async () => {
        const mockThreads = [
          { id: 'thread-1', title: 'Thread 1', gameId: 'game-1', status: 'active' },
          { id: 'thread-2', title: 'Thread 2', gameId: 'game-1', status: 'closed' },
        ];

        mockHttpClient.get.mockResolvedValueOnce(mockThreads);

        const result = await chatClient.getThreadsByGame('game-1');

        expect(mockHttpClient.get).toHaveBeenCalledWith(
          '/api/v1/knowledge-base/chat-threads?gameId=game-1'
        );
        expect(result).toEqual(mockThreads);
      });

      it('should return empty array when response is null', async () => {
        mockHttpClient.get.mockResolvedValueOnce(null);
        const result = await chatClient.getThreadsByGame('game-1');
        commonExpectations.expectEmptyArray(result);
      });

      it('should encode gameId in URL', async () => {
        mockHttpClient.get.mockResolvedValueOnce([]);
        await chatClient.getThreadsByGame('game with spaces');

        expect(mockHttpClient.get).toHaveBeenCalledWith(
          '/api/v1/knowledge-base/chat-threads?gameId=game%20with%20spaces'
        );
      });

      it('should handle special characters in gameId', async () => {
        mockHttpClient.get.mockResolvedValueOnce([]);
        await chatClient.getThreadsByGame('game&id=123');

        expect(mockHttpClient.get).toHaveBeenCalledWith(
          expect.stringContaining('gameId=game%26id%3D123')
        );
      });

      it('should handle empty gameId', async () => {
        mockHttpClient.get.mockResolvedValueOnce([]);
        await chatClient.getThreadsByGame('');

        expect(mockHttpClient.get).toHaveBeenCalledWith(
          '/api/v1/knowledge-base/chat-threads?gameId='
        );
      });
    });

    describe('getThreadById', () => {
      it('should fetch thread by ID', async () => {
        const mockThread = createMockThread();
        mockHttpClient.get.mockResolvedValueOnce(mockThread);

        const result = await chatClient.getThreadById('thread-123');

        expect(mockHttpClient.get).toHaveBeenCalledWith(
          '/api/v1/knowledge-base/chat-threads/thread-123',
          expect.anything()
        );
        expect(result).toEqual(mockThread);
      });

      it('should return null when thread not found', async () => {
        mockHttpClient.get.mockResolvedValueOnce(null);
        const result = await chatClient.getThreadById('non-existent');
        commonExpectations.expectNullReturn(result);
      });

      it('should encode threadId in URL', async () => {
        mockHttpClient.get.mockResolvedValueOnce(null);
        await chatClient.getThreadById('thread with/slash');

        expect(mockHttpClient.get).toHaveBeenCalledWith(
          expect.stringContaining('thread%20with%2Fslash'),
          expect.anything()
        );
      });
    });

    describe('createThread', () => {
      it('should create thread with all fields', async () => {
        const request = {
          gameId: 'game-1',
          title: 'New Thread',
          initialMessage: 'Hello',
        };

        const mockResponse = { id: 'thread-new', ...request };
        mockHttpClient.post.mockResolvedValueOnce(mockResponse);

        const result = await chatClient.createThread(request);

        expect(mockHttpClient.post).toHaveBeenCalledWith(
          '/api/v1/knowledge-base/chat-threads',
          request,
          expect.anything()
        );
        expect(result).toEqual(mockResponse);
      });

      it('should create thread with minimal fields', async () => {
        const request = {};
        mockHttpClient.post.mockResolvedValueOnce({ id: 'thread-min' });

        await chatClient.createThread(request);

        expect(mockHttpClient.post).toHaveBeenCalledWith(
          '/api/v1/knowledge-base/chat-threads',
          request,
          expect.anything()
        );
      });

      it('should create thread with null fields', async () => {
        const request = {
          gameId: null,
          title: null,
          initialMessage: null,
        };

        mockHttpClient.post.mockResolvedValueOnce({ id: 'thread-null' });
        await chatClient.createThread(request);

        expect(mockHttpClient.post).toHaveBeenCalledWith(
          '/api/v1/knowledge-base/chat-threads',
          request,
          expect.anything()
        );
      });
    });

    describe('addMessage', () => {
      it('should add message to thread', async () => {
        const request = {
          content: 'New message',
          role: 'user',
        };

        const mockResponse = { id: 'thread-123', messages: [request] };
        mockHttpClient.post.mockResolvedValueOnce(mockResponse);

        const result = await chatClient.addMessage('thread-123', request);

        expect(mockHttpClient.post).toHaveBeenCalledWith(
          '/api/v1/knowledge-base/chat-threads/thread-123/messages',
          request,
          expect.anything()
        );
        expect(result).toEqual(mockResponse);
      });

      it('should encode threadId in URL', async () => {
        mockHttpClient.post.mockResolvedValueOnce({ id: 'thread' });

        await chatClient.addMessage('thread/123', {
          content: 'Message',
          role: 'user',
        });

        expect(mockHttpClient.post).toHaveBeenCalledWith(
          expect.stringContaining('thread%2F123'),
          expect.anything(),
          expect.anything()
        );
      });

      it('should handle empty content', async () => {
        mockHttpClient.post.mockResolvedValueOnce({ id: 'thread' });

        await chatClient.addMessage('thread-1', {
          content: '',
          role: 'user',
        });

        expect(mockHttpClient.post).toHaveBeenCalled();
      });
    });

    describe('closeThread', () => {
      it('should close thread', async () => {
        const mockResponse = { id: 'thread-123', status: 'closed' };
        mockHttpClient.post.mockResolvedValueOnce(mockResponse);

        const result = await chatClient.closeThread('thread-123');

        expect(mockHttpClient.post).toHaveBeenCalledWith(
          '/api/v1/knowledge-base/chat-threads/thread-123/close',
          {},
          expect.anything()
        );
        expect(result).toEqual(mockResponse);
      });

      it('should encode threadId in URL', async () => {
        mockHttpClient.post.mockResolvedValueOnce({ id: 'thread' });
        await chatClient.closeThread('thread with spaces');

        expect(mockHttpClient.post).toHaveBeenCalledWith(
          expect.stringContaining('thread%20with%20spaces'),
          expect.anything(),
          expect.anything()
        );
      });
    });

    describe('reopenThread', () => {
      it('should reopen thread', async () => {
        const mockResponse = { id: 'thread-123', status: 'active' };
        mockHttpClient.post.mockResolvedValueOnce(mockResponse);

        const result = await chatClient.reopenThread('thread-123');

        expect(mockHttpClient.post).toHaveBeenCalledWith(
          '/api/v1/knowledge-base/chat-threads/thread-123/reopen',
          {},
          expect.anything()
        );
        expect(result).toEqual(mockResponse);
      });
    });
  });

  describe('Chat Messages', () => {
    describe('updateMessage', () => {
      it('should update message content', async () => {
        const mockResponse = createMockMessage({ content: 'Updated content' });
        mockHttpClient.put.mockResolvedValueOnce(mockResponse);

        const result = await chatClient.updateMessage('chat-1', 'msg-123', 'Updated content');

        expect(mockHttpClient.put).toHaveBeenCalledWith(
          '/api/v1/chats/chat-1/messages/msg-123',
          { content: 'Updated content' },
          expect.anything()
        );
        expect(result).toEqual(mockResponse);
      });

      it('should handle empty content update', async () => {
        mockHttpClient.put.mockResolvedValueOnce({ id: 'msg-123', content: '' });
        await chatClient.updateMessage('chat-1', 'msg-123', '');

        expect(mockHttpClient.put).toHaveBeenCalledWith(
          expect.any(String),
          { content: '' },
          expect.anything()
        );
      });

      it('should handle special characters in IDs', async () => {
        mockHttpClient.put.mockResolvedValueOnce({ id: 'msg' });
        await chatClient.updateMessage('chat/1', 'msg#123', 'Content');

        expect(mockHttpClient.put).toHaveBeenCalled();
      });
    });

    describe('deleteMessage', () => {
      it('should delete message', async () => {
        mockHttpClient.delete.mockResolvedValueOnce(undefined);
        await chatClient.deleteMessage('chat-1', 'msg-123');

        expect(mockHttpClient.delete).toHaveBeenCalledWith('/api/v1/chats/chat-1/messages/msg-123');
      });

      it('should return void', async () => {
        mockHttpClient.delete.mockResolvedValueOnce(undefined);
        const result = await chatClient.deleteMessage('chat-1', 'msg-123');

        commonExpectations.expectVoidReturn(result);
      });
    });
  });

  describe('Chat Export', () => {
    beforeEach(() => {
      mockDownloadFile.mockClear();
    });

    it('should export chat as PDF', async () => {
      const blob = new Blob(['content'], { type: 'application/pdf' });
      mockHttpClient.postFile.mockResolvedValueOnce({
        blob,
        filename: 'chat-export.pdf',
      });

      await chatClient.exportChat('chat-1', { format: 'pdf' });

      expect(mockHttpClient.postFile).toHaveBeenCalledWith('/api/v1/chats/chat-1/export', {
        format: 'pdf',
      });
      expect(mockDownloadFile).toHaveBeenCalledWith(blob, 'chat-export.pdf');
    });

    it('should export chat as TXT', async () => {
      const blob = new Blob(['content'], { type: 'text/plain' });
      mockHttpClient.postFile.mockResolvedValueOnce({
        blob,
        filename: 'chat-export.txt',
      });

      await chatClient.exportChat('chat-1', { format: 'txt' });

      expect(mockHttpClient.postFile).toHaveBeenCalledWith('/api/v1/chats/chat-1/export', {
        format: 'txt',
      });
    });

    it('should export chat as Markdown', async () => {
      const blob = new Blob(['content'], { type: 'text/markdown' });
      mockHttpClient.postFile.mockResolvedValueOnce({
        blob,
        filename: 'chat-export.md',
      });

      await chatClient.exportChat('chat-1', { format: 'md' });

      expect(mockHttpClient.postFile).toHaveBeenCalledWith('/api/v1/chats/chat-1/export', {
        format: 'md',
      });
    });

    it('should include date filters when provided', async () => {
      const blob = new Blob(['content']);
      mockHttpClient.postFile.mockResolvedValueOnce({ blob, filename: 'export.pdf' });

      await chatClient.exportChat('chat-1', {
        format: 'pdf',
        dateFrom: '2025-01-01',
        dateTo: '2025-01-31',
      });

      expect(mockHttpClient.postFile).toHaveBeenCalledWith(
        expect.any(String),
        expect.objectContaining({
          format: 'pdf',
          dateFrom: '2025-01-01',
          dateTo: '2025-01-31',
        })
      );
    });

    it('should encode chatId in URL', async () => {
      const blob = new Blob(['content']);
      mockHttpClient.postFile.mockResolvedValueOnce({ blob, filename: 'export.pdf' });

      await chatClient.exportChat('chat with/spaces', { format: 'pdf' });

      expect(mockHttpClient.postFile).toHaveBeenCalledWith(
        expect.stringContaining('chat%20with%2Fspaces'),
        expect.anything()
      );
    });
  });

  describe('Error Handling', () => {
    it('should propagate httpClient errors for getThreadsByGame', async () => {
      const error = new Error('Network error');
      mockHttpClient.get.mockRejectedValueOnce(error);

      await expect(chatClient.getThreadsByGame('game-1')).rejects.toThrow('Network error');
    });

    it('should propagate httpClient errors for createThread', async () => {
      const error = new Error('Validation error');
      mockHttpClient.post.mockRejectedValueOnce(error);

      await expect(chatClient.createThread({})).rejects.toThrow('Validation error');
    });

    it('should propagate httpClient errors for exportChat', async () => {
      const error = new Error('Export failed');
      mockHttpClient.postFile.mockRejectedValueOnce(error);

      await expect(chatClient.exportChat('chat-1', { format: 'pdf' })).rejects.toThrow(
        'Export failed'
      );
    });
  });

  describe('Edge Cases', () => {
    it('should handle Unicode in gameId', async () => {
      mockHttpClient.get.mockResolvedValueOnce([]);
      await chatClient.getThreadsByGame('游戏-中文');

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        expect.stringContaining(encodeURIComponent('游戏-中文'))
      );
    });

    it('should handle concurrent operations on same thread', async () => {
      mockHttpClient.post.mockResolvedValue({ id: 'thread' });

      const operations = [
        chatClient.addMessage('thread-1', { content: 'msg1', role: 'user' }),
        chatClient.addMessage('thread-1', { content: 'msg2', role: 'user' }),
        chatClient.closeThread('thread-1'),
      ];

      await Promise.all(operations);

      expect(mockHttpClient.post).toHaveBeenCalledTimes(3);
    });

    it('should handle numeric IDs as strings', async () => {
      mockHttpClient.get.mockResolvedValueOnce(null);
      await chatClient.getThreadById('12345');

      expect(mockHttpClient.get).toHaveBeenCalled();
    });
  });
});
