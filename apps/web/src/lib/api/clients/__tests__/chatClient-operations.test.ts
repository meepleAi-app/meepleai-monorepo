/**
 * Chat Client Tests - Operations & Management
 *
 * Tests for RuleSpec comments, bulk operations, cache management, and agent feedback
 */

import { createChatClient } from '../chatClient';
import { downloadFile } from '../../core/httpClient';
import {
  createMockHttpClient,
  createMockComment,
  createMockCacheStats,
  commonExpectations,
} from './chatClient.test-helpers';

describe('ChatClient - Operations & Management', () => {
  let mockHttpClient: ReturnType<typeof createMockHttpClient>;
  let chatClient: ReturnType<typeof createChatClient>;

  beforeEach(() => {
    mockHttpClient = createMockHttpClient();
    chatClient = createChatClient({ httpClient: mockHttpClient });
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

        commonExpectations.expectNullReturn(result);
      });
    });

    describe('createRuleSpecComment', () => {
      it('should create comment with all fields', async () => {
        const request = {
          atomId: 'atom-123',
          lineNumber: 42,
          commentText: 'This needs clarification',
        };

        const mockResponse = createMockComment(request);
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

        const mockResponse = createMockComment(request);
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

        commonExpectations.expectVoidReturn(result);
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

        commonExpectations.expectVoidReturn(result);
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
        const mockStats = createMockCacheStats();
        mockHttpClient.get.mockResolvedValueOnce(mockStats);

        const result = await chatClient.getCacheStats();

        expect(mockHttpClient.get).toHaveBeenCalledWith(
          '/api/v1/admin/cache/stats',
          expect.anything()
        );
        expect(result).toEqual(mockStats);
      });

      it('should fetch cache stats for specific game', async () => {
        const mockStats = createMockCacheStats({
          totalEntries: 50,
          hitRate: 0.90,
          memoryUsage: 256000,
        });

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

        commonExpectations.expectNullReturn(result);
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

        commonExpectations.expectVoidReturn(result);
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

        commonExpectations.expectVoidReturn(result);
      });
    });
  });

  describe('Error Handling', () => {
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
  });
});
