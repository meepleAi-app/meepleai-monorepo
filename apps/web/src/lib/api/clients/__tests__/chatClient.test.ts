/**
 * Comprehensive Tests for Chat Client (Issue #1661 - Fase 1.2)
 *
 * Coverage target: 95%+
 * Tests: All client methods, validation, error handling, edge cases
 */

import { createChatClient, ChatClient, ExportFormat } from '../chatClient';
import { HttpClient } from '../../core/httpClient';

// Mock downloadFile at module level
vi.mock('../../core/httpClient', () => ({
  ...jest.requireActual('../../core/httpClient'),
  downloadFile: vi.fn(),
}));

import { downloadFile } from '../../core/httpClient';

describe('createChatClient', () => {
  let mockHttpClient: Mocked<HttpClient>;
  let chatClient: ChatClient;

  beforeEach(() => {
    mockHttpClient = {
      get: vi.fn(),
      post: vi.fn(),
      put: vi.fn(),
      delete: vi.fn(),
      postFile: vi.fn(),
    } as any;

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

        expect(result).toEqual([]);
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
        const mockThread = { id: 'thread-123', title: 'Test Thread' };

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

        expect(result).toBeNull();
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
        const mockResponse = {
          id: 'msg-123',
          content: 'Updated content',
          role: 'user',
        };

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

        expect(result).toBeUndefined();
      });
    });
  });

  describe('Chat Export', () => {
    beforeEach(() => {
      (downloadFile as Mock).mockClear();
    });

    it('should export chat as PDF', async () => {
      const blob = new Blob(['content'], { type: 'application/pdf' });
      mockHttpClient.postFile.mockResolvedValueOnce({
        blob,
        filename: 'chat-export.pdf',
      });

      await chatClient.exportChat('chat-1', { format: 'pdf' });

      expect(mockHttpClient.postFile).toHaveBeenCalledWith(
        '/api/v1/chats/chat-1/export',
        { format: 'pdf' }
      );
      expect(downloadFile).toHaveBeenCalledWith(blob, 'chat-export.pdf');
    });

    it('should export chat as TXT', async () => {
      const blob = new Blob(['content'], { type: 'text/plain' });
      mockHttpClient.postFile.mockResolvedValueOnce({
        blob,
        filename: 'chat-export.txt',
      });

      await chatClient.exportChat('chat-1', { format: 'txt' });

      expect(mockHttpClient.postFile).toHaveBeenCalledWith(
        '/api/v1/chats/chat-1/export',
        { format: 'txt' }
      );
    });

    it('should export chat as Markdown', async () => {
      const blob = new Blob(['content'], { type: 'text/markdown' });
      mockHttpClient.postFile.mockResolvedValueOnce({
        blob,
        filename: 'chat-export.md',
      });

      await chatClient.exportChat('chat-1', { format: 'md' });

      expect(mockHttpClient.postFile).toHaveBeenCalledWith(
        '/api/v1/chats/chat-1/export',
        { format: 'md' }
      );
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

  describe('RuleSpec Comments', () => {
    describe('getRuleSpecComments', () => {
      it('should fetch comments for game and version', async () => {
        const mockComments = {
          comments: [
            { id: 'comment-1', text: 'Comment 1' },
            { id: 'comment-2', text: 'Comment 2' },
          ],
        };

        mockHttpClient.get.mockResolvedValueOnce(mockComments);

        const result = await chatClient.getRuleSpecComments('catan', '1.0');

        expect(mockHttpClient.get).toHaveBeenCalledWith(
          '/api/v1/games/catan/rulespec/versions/1.0/comments?includeResolved=true',
          expect.anything()
        );
        expect(result).toEqual(mockComments);
      });

      it('should include resolved comments by default', async () => {
        mockHttpClient.get.mockResolvedValueOnce(null);

        await chatClient.getRuleSpecComments('catan', '1.0');

        expect(mockHttpClient.get).toHaveBeenCalledWith(
          expect.stringContaining('includeResolved=true'),
          expect.anything()
        );
      });

      it('should exclude resolved comments when specified', async () => {
        mockHttpClient.get.mockResolvedValueOnce(null);

        await chatClient.getRuleSpecComments('catan', '1.0', false);

        expect(mockHttpClient.get).toHaveBeenCalledWith(
          expect.stringContaining('includeResolved=false'),
          expect.anything()
        );
      });

      it('should return null when no comments found', async () => {
        mockHttpClient.get.mockResolvedValueOnce(null);

        const result = await chatClient.getRuleSpecComments('catan', '1.0');

        expect(result).toBeNull();
      });
    });

    describe('createRuleSpecComment', () => {
      it('should create comment with all fields', async () => {
        const request = {
          atomId: 'atom-123',
          lineNumber: 42,
          commentText: 'This needs clarification',
        };

        const mockResponse = {
          id: 'comment-new',
          ...request,
          createdAt: new Date().toISOString(),
        };

        mockHttpClient.post.mockResolvedValueOnce(mockResponse);

        const result = await chatClient.createRuleSpecComment('catan', '1.0', request);

        expect(mockHttpClient.post).toHaveBeenCalledWith(
          '/api/v1/games/catan/rulespec/versions/1.0/comments',
          request,
          expect.anything()
        );
        expect(result).toEqual(mockResponse);
      });

      it('should create comment with null atomId', async () => {
        const request = {
          atomId: null,
          commentText: 'General comment',
        };

        mockHttpClient.post.mockResolvedValueOnce({ id: 'comment' });

        await chatClient.createRuleSpecComment('catan', '1.0', request);

        expect(mockHttpClient.post).toHaveBeenCalledWith(
          expect.any(String),
          expect.objectContaining({ atomId: null }),
          expect.anything()
        );
      });

      it('should create comment with null lineNumber', async () => {
        const request = {
          atomId: 'atom-1',
          lineNumber: null,
          commentText: 'Comment',
        };

        mockHttpClient.post.mockResolvedValueOnce({ id: 'comment' });

        await chatClient.createRuleSpecComment('catan', '1.0', request);

        expect(mockHttpClient.post).toHaveBeenCalled();
      });
    });

    describe('updateRuleSpecComment', () => {
      it('should update comment text', async () => {
        const request = {
          commentText: 'Updated comment text',
        };

        const mockResponse = {
          id: 'comment-123',
          commentText: 'Updated comment text',
        };

        mockHttpClient.put.mockResolvedValueOnce(mockResponse);

        const result = await chatClient.updateRuleSpecComment('catan', 'comment-123', request);

        expect(mockHttpClient.put).toHaveBeenCalledWith(
          '/api/v1/games/catan/rulespec/comments/comment-123',
          request,
          expect.anything()
        );
        expect(result).toEqual(mockResponse);
      });

      it('should handle empty comment text', async () => {
        mockHttpClient.put.mockResolvedValueOnce({ id: 'comment' });

        await chatClient.updateRuleSpecComment('catan', 'comment-1', {
          commentText: '',
        });

        expect(mockHttpClient.put).toHaveBeenCalled();
      });
    });

    describe('deleteRuleSpecComment', () => {
      it('should delete comment', async () => {
        mockHttpClient.delete.mockResolvedValueOnce(undefined);

        await chatClient.deleteRuleSpecComment('catan', 'comment-123');

        expect(mockHttpClient.delete).toHaveBeenCalledWith(
          '/api/v1/games/catan/rulespec/comments/comment-123'
        );
      });

      it('should return void', async () => {
        mockHttpClient.delete.mockResolvedValueOnce(undefined);

        const result = await chatClient.deleteRuleSpecComment('catan', 'comment-123');

        expect(result).toBeUndefined();
      });
    });

    describe('createCommentReply', () => {
      it('should create reply to parent comment', async () => {
        const request = {
          commentText: 'Reply text',
        };

        const mockResponse = {
          id: 'reply-new',
          parentId: 'comment-parent',
          commentText: 'Reply text',
        };

        mockHttpClient.post.mockResolvedValueOnce(mockResponse);

        const result = await chatClient.createCommentReply('comment-parent', request);

        expect(mockHttpClient.post).toHaveBeenCalledWith(
          '/api/v1/rulespec/comments/comment-parent/replies',
          request,
          expect.anything()
        );
        expect(result).toEqual(mockResponse);
      });

      it('should handle empty reply text', async () => {
        mockHttpClient.post.mockResolvedValueOnce({ id: 'reply' });

        await chatClient.createCommentReply('parent', { commentText: '' });

        expect(mockHttpClient.post).toHaveBeenCalled();
      });
    });

    describe('resolveComment', () => {
      it('should resolve comment', async () => {
        mockHttpClient.post.mockResolvedValueOnce(undefined);

        await chatClient.resolveComment('comment-123');

        expect(mockHttpClient.post).toHaveBeenCalledWith(
          '/api/v1/rulespec/comments/comment-123/resolve',
          {}
        );
      });

      it('should return void', async () => {
        mockHttpClient.post.mockResolvedValueOnce(undefined);

        const result = await chatClient.resolveComment('comment-123');

        expect(result).toBeUndefined();
      });
    });

    describe('unresolveComment', () => {
      it('should unresolve comment', async () => {
        mockHttpClient.post.mockResolvedValueOnce(undefined);

        await chatClient.unresolveComment('comment-123');

        expect(mockHttpClient.post).toHaveBeenCalledWith(
          '/api/v1/rulespec/comments/comment-123/unresolve',
          {}
        );
      });
    });
  });

  describe('Bulk Operations', () => {
    describe('bulkExportRuleSpecs', () => {
      beforeEach(() => {
        (downloadFile as Mock).mockClear();
      });

      it('should export multiple rule specs', async () => {
        const request = {
          ruleSpecIds: ['spec-1', 'spec-2', 'spec-3'],
        };

        const blob = new Blob(['content'], { type: 'application/zip' });
        mockHttpClient.postFile.mockResolvedValueOnce({
          blob,
          filename: 'rulespecs-export.zip',
        });

        await chatClient.bulkExportRuleSpecs(request);

        expect(mockHttpClient.postFile).toHaveBeenCalledWith(
          '/api/v1/rulespecs/bulk/export',
          request
        );
        expect(downloadFile).toHaveBeenCalledWith(blob, 'rulespecs-export.zip');
      });

      it('should handle single rule spec export', async () => {
        const blob = new Blob(['content']);
        mockHttpClient.postFile.mockResolvedValueOnce({ blob, filename: 'export.zip' });

        await chatClient.bulkExportRuleSpecs({ ruleSpecIds: ['spec-1'] });

        expect(mockHttpClient.postFile).toHaveBeenCalled();
      });

      it('should handle empty rule spec list', async () => {
        const blob = new Blob(['content']);
        mockHttpClient.postFile.mockResolvedValueOnce({ blob, filename: 'export.zip' });

        await chatClient.bulkExportRuleSpecs({ ruleSpecIds: [] });

        expect(mockHttpClient.postFile).toHaveBeenCalledWith(
          '/api/v1/rulespecs/bulk/export',
          { ruleSpecIds: [] }
        );
      });
    });
  });

  describe('Cache Management', () => {
    describe('getCacheStats', () => {
      it('should fetch cache stats without gameId', async () => {
        const mockStats = {
          totalEntries: 100,
          hitRate: 0.85,
          memoryUsage: 512000,
        };

        mockHttpClient.get.mockResolvedValueOnce(mockStats);

        const result = await chatClient.getCacheStats();

        expect(mockHttpClient.get).toHaveBeenCalledWith(
          '/api/v1/admin/cache/stats',
          expect.anything()
        );
        expect(result).toEqual(mockStats);
      });

      it('should fetch cache stats for specific game', async () => {
        const mockStats = {
          totalEntries: 50,
          hitRate: 0.90,
          memoryUsage: 256000,
        };

        mockHttpClient.get.mockResolvedValueOnce(mockStats);

        const result = await chatClient.getCacheStats('catan');

        expect(mockHttpClient.get).toHaveBeenCalledWith(
          '/api/v1/admin/cache/stats?gameId=catan',
          expect.anything()
        );
        expect(result).toEqual(mockStats);
      });

      it('should return null when stats not available', async () => {
        mockHttpClient.get.mockResolvedValueOnce(null);

        const result = await chatClient.getCacheStats();

        expect(result).toBeNull();
      });

      it('should encode gameId in URL', async () => {
        mockHttpClient.get.mockResolvedValueOnce(null);

        await chatClient.getCacheStats('game with spaces');

        expect(mockHttpClient.get).toHaveBeenCalledWith(
          expect.stringContaining('gameId=game%20with%20spaces'),
          expect.anything()
        );
      });
    });

    describe('invalidateGameCache', () => {
      it('should invalidate cache for game', async () => {
        mockHttpClient.delete.mockResolvedValueOnce(undefined);

        await chatClient.invalidateGameCache('catan');

        expect(mockHttpClient.delete).toHaveBeenCalledWith('/api/v1/admin/cache/games/catan');
      });

      it('should encode gameId in URL', async () => {
        mockHttpClient.delete.mockResolvedValueOnce(undefined);

        await chatClient.invalidateGameCache('game/with/slashes');

        expect(mockHttpClient.delete).toHaveBeenCalledWith(
          expect.stringContaining('game%2Fwith%2Fslashes')
        );
      });

      it('should return void', async () => {
        mockHttpClient.delete.mockResolvedValueOnce(undefined);

        const result = await chatClient.invalidateGameCache('catan');

        expect(result).toBeUndefined();
      });
    });

    describe('invalidateCacheByTag', () => {
      it('should invalidate cache by tag', async () => {
        mockHttpClient.delete.mockResolvedValueOnce(undefined);

        await chatClient.invalidateCacheByTag('game-rules');

        expect(mockHttpClient.delete).toHaveBeenCalledWith(
          '/api/v1/admin/cache/tags/game-rules'
        );
      });

      it('should encode tag in URL', async () => {
        mockHttpClient.delete.mockResolvedValueOnce(undefined);

        await chatClient.invalidateCacheByTag('tag with spaces');

        expect(mockHttpClient.delete).toHaveBeenCalledWith(
          expect.stringContaining('tag%20with%20spaces')
        );
      });

      it('should handle special characters in tag', async () => {
        mockHttpClient.delete.mockResolvedValueOnce(undefined);

        await chatClient.invalidateCacheByTag('tag&special=chars');

        expect(mockHttpClient.delete).toHaveBeenCalledWith(
          expect.stringContaining('tag%26special%3Dchars')
        );
      });
    });
  });

  describe('Agent Feedback', () => {
    describe('submitAgentFeedback', () => {
      it('should submit helpful feedback', async () => {
        const request = {
          messageId: 'msg-123',
          endpoint: '/api/v1/chat',
          gameId: 'catan',
          outcome: 'helpful' as const,
        };

        mockHttpClient.post.mockResolvedValueOnce(undefined);

        await chatClient.submitAgentFeedback(request);

        expect(mockHttpClient.post).toHaveBeenCalledWith('/api/v1/agents/feedback', request);
      });

      it('should submit not-helpful feedback', async () => {
        const request = {
          messageId: 'msg-456',
          endpoint: '/api/v1/rag',
          gameId: 'pandemic',
          outcome: 'not-helpful' as const,
        };

        mockHttpClient.post.mockResolvedValueOnce(undefined);

        await chatClient.submitAgentFeedback(request);

        expect(mockHttpClient.post).toHaveBeenCalledWith('/api/v1/agents/feedback', request);
      });

      it('should return void', async () => {
        mockHttpClient.post.mockResolvedValueOnce(undefined);

        const result = await chatClient.submitAgentFeedback({
          messageId: 'msg',
          endpoint: '/api',
          gameId: 'game',
          outcome: 'helpful',
        });

        expect(result).toBeUndefined();
      });
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

    it('should propagate httpClient errors for cache operations', async () => {
      const error = new Error('Cache error');
      mockHttpClient.delete.mockRejectedValueOnce(error);

      await expect(chatClient.invalidateGameCache('catan')).rejects.toThrow('Cache error');
    });
  });

  describe('Edge Cases', () => {
    it('should handle very long comment text', async () => {
      const longText = 'A'.repeat(10000);
      mockHttpClient.post.mockResolvedValueOnce({ id: 'comment' });

      await chatClient.createRuleSpecComment('catan', '1.0', {
        atomId: 'atom',
        commentText: longText,
      });

      expect(mockHttpClient.post).toHaveBeenCalledWith(
        expect.any(String),
        expect.objectContaining({ commentText: longText }),
        expect.anything()
      );
    });

    it('should handle Unicode in gameId', async () => {
      mockHttpClient.get.mockResolvedValueOnce([]);

      await chatClient.getThreadsByGame('游戏-中文');

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        expect.stringContaining(encodeURIComponent('游戏-中文')),
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
