/**
 * TEST-625: Extended test suite for lib/api.ts (Part 2)
 * Coverage for uncovered areas (Lines 415-806)
 *
 * This file extends api.test.ts to cover:
 * - EDIT-05: createReply, resolveComment, unresolveComment
 * - PERF-03: Cache management API
 * - PDF-08: PDF processing progress API
 * - CHAT-05/06: Chat export and message management
 * - AI-13: BoardGameGeek integration
 * - CONFIG-06: Dynamic configuration system
 * - EDIT-07: Bulk RuleSpec operations
 * - AUTH-07: Two-Factor Authentication
 */

import { api, ApiError } from '../api';

// Mock global fetch and DOM APIs
global.fetch = jest.fn();

// Mock DOM APIs for file download tests
document.body.appendChild = jest.fn();
document.body.removeChild = jest.fn();
document.createElement = jest.fn(() => ({
  click: jest.fn(),
  href: '',
  download: ''
})) as any;
URL.createObjectURL = jest.fn(() => 'blob:mock-url');
URL.revokeObjectURL = jest.fn();

describe('api.ts - Extended Coverage (TEST-625)', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  const setFetchResponse = (status: number, payload?: unknown, headers?: Record<string, string>) => {
    const mockHeaders = new Map(Object.entries(headers || {}));
    (fetch as jest.Mock).mockResolvedValue({
      ok: status >= 200 && status < 300,
      status,
      json: async () => payload,
      blob: async () => new Blob([JSON.stringify(payload)]),
      headers: {
        get: (name: string) => mockHeaders.get(name) || null
      }
    } as Response);
  };

  // ==================================================================
  // EDIT-05: Threaded Reply Support (Lines 415-427)
  // ==================================================================
  describe('api.ruleSpecComments - Extended (EDIT-05)', () => {
    const commentId = 'comment-123';

    describe('createReply', () => {
      it('should create a reply to a parent comment', async () => {
        const request = { commentText: 'This is a reply' };
        const mockReply = {
          id: 'reply-1',
          parentCommentId: commentId,
          commentText: 'This is a reply',
          userId: 'user-1',
          userDisplayName: 'Jane Doe',
          createdAt: '2025-11-02T10:00:00Z'
        };

        setFetchResponse(201, mockReply);

        const result = await api.ruleSpecComments.createReply(commentId, request);

        expect(fetch).toHaveBeenCalledWith(
          `http://localhost:8080/api/v1/rulespec/comments/${commentId}/replies`,
          {
            method: 'POST',
            credentials: 'include',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(request)
          }
        );
        expect(result).toEqual(mockReply);
      });

      it('should throw on 401 Unauthorized', async () => {
        setFetchResponse(401, null, { 'X-Correlation-Id': 'corr-123' });

        await expect(
          api.ruleSpecComments.createReply(commentId, { commentText: 'Reply' })
        ).rejects.toThrow('Unauthorized');
      });

      it('should throw on 404 Not Found', async () => {
        setFetchResponse(404, { error: 'Parent comment not found' });

        await expect(
          api.ruleSpecComments.createReply(commentId, { commentText: 'Reply' })
        ).rejects.toThrow(ApiError);
      });
    });

    describe('resolveComment', () => {
      it('should resolve a comment', async () => {
        setFetchResponse(200, {});

        await api.ruleSpecComments.resolveComment(commentId);

        expect(fetch).toHaveBeenCalledWith(
          `http://localhost:8080/api/v1/rulespec/comments/${commentId}/resolve`,
          {
            method: 'POST',
            credentials: 'include',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({})
          }
        );
      });

      it('should throw on 401 Unauthorized', async () => {
        setFetchResponse(401, null, { 'X-Correlation-Id': 'corr-456' });

        await expect(api.ruleSpecComments.resolveComment(commentId)).rejects.toThrow(
          'Unauthorized'
        );
      });

      it('should throw on 403 Forbidden', async () => {
        setFetchResponse(403, { error: 'Cannot resolve other users comments' });

        await expect(api.ruleSpecComments.resolveComment(commentId)).rejects.toThrow(
          ApiError
        );
      });
    });

    describe('unresolveComment', () => {
      it('should unresolve a comment', async () => {
        setFetchResponse(200, {});

        await api.ruleSpecComments.unresolveComment(commentId);

        expect(fetch).toHaveBeenCalledWith(
          `http://localhost:8080/api/v1/rulespec/comments/${commentId}/unresolve`,
          {
            method: 'POST',
            credentials: 'include',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({})
          }
        );
      });

      it('should throw on 401 Unauthorized', async () => {
        setFetchResponse(401, null, { 'X-Correlation-Id': 'corr-789' });

        await expect(api.ruleSpecComments.unresolveComment(commentId)).rejects.toThrow(
          'Unauthorized'
        );
      });
    });
  });

  // ==================================================================
  // PERF-03: Cache Management API (Lines 432-447)
  // ==================================================================
  describe('api.cache (PERF-03)', () => {
    describe('getStats', () => {
      it('should get cache stats without gameId', async () => {
        const mockStats = {
          totalHits: 1000,
          totalMisses: 200,
          hitRate: 0.83,
          totalKeys: 150,
          cacheSizeBytes: 1048576,
          topQuestions: [
            {
              questionHash: 'hash1',
              hitCount: 50,
              missCount: 5,
              lastHitAt: '2025-11-02T10:00:00Z'
            }
          ]
        };

        setFetchResponse(200, mockStats);

        const result = await api.cache.getStats();

        expect(fetch).toHaveBeenCalledWith(
          'http://localhost:8080/api/v1/admin/cache/stats',
          {
            method: 'GET',
            credentials: 'include'
          }
        );
        expect(result).toEqual(mockStats);
      });

      it('should get cache stats with gameId filter', async () => {
        const mockStats = {
          totalHits: 500,
          totalMisses: 100,
          hitRate: 0.83,
          totalKeys: 75,
          cacheSizeBytes: 524288,
          topQuestions: []
        };

        setFetchResponse(200, mockStats);

        const result = await api.cache.getStats('chess');

        expect(fetch).toHaveBeenCalledWith(
          'http://localhost:8080/api/v1/admin/cache/stats?gameId=chess',
          {
            method: 'GET',
            credentials: 'include'
          }
        );
        expect(result).toEqual(mockStats);
      });

      it('should encode gameId parameter', async () => {
        setFetchResponse(200, {});

        await api.cache.getStats('game with spaces');

        expect(fetch).toHaveBeenCalledWith(
          expect.stringContaining('gameId=game%20with%20spaces'),
          expect.any(Object)
        );
      });

      it('should return null on 401', async () => {
        setFetchResponse(401);

        const result = await api.cache.getStats();

        expect(result).toBeNull();
      });
    });

    describe('invalidateGameCache', () => {
      it('should invalidate game cache', async () => {
        setFetchResponse(204);

        await api.cache.invalidateGameCache('chess');

        expect(fetch).toHaveBeenCalledWith(
          'http://localhost:8080/api/v1/admin/cache/games/chess',
          {
            method: 'DELETE',
            credentials: 'include'
          }
        );
      });

      it('should encode gameId in URL', async () => {
        setFetchResponse(204);

        await api.cache.invalidateGameCache('game/with/slashes');

        expect(fetch).toHaveBeenCalledWith(
          expect.stringContaining('games/game%2Fwith%2Fslashes'),
          expect.any(Object)
        );
      });

      it('should throw on 401 Unauthorized', async () => {
        setFetchResponse(401, null, { 'X-Correlation-Id': 'corr-cache-1' });

        await expect(api.cache.invalidateGameCache('chess')).rejects.toThrow(
          'Unauthorized'
        );
      });
    });

    describe('invalidateByTag', () => {
      it('should invalidate cache by tag', async () => {
        setFetchResponse(204);

        await api.cache.invalidateByTag('rag-responses');

        expect(fetch).toHaveBeenCalledWith(
          'http://localhost:8080/api/v1/admin/cache/tags/rag-responses',
          {
            method: 'DELETE',
            credentials: 'include'
          }
        );
      });

      it('should encode tag in URL', async () => {
        setFetchResponse(204);

        await api.cache.invalidateByTag('tag:with:colons');

        expect(fetch).toHaveBeenCalledWith(
          expect.stringContaining('tags/tag%3Awith%3Acolons'),
          expect.any(Object)
        );
      });

      it('should throw on 401 Unauthorized', async () => {
        setFetchResponse(401, null, { 'X-Correlation-Id': 'corr-cache-2' });

        await expect(api.cache.invalidateByTag('tag')).rejects.toThrow('Unauthorized');
      });
    });
  });

  // ==================================================================
  // PDF-08: PDF Processing Progress API (Lines 450-460)
  // ==================================================================
  describe('api.pdf (PDF-08)', () => {
    describe('getProcessingProgress', () => {
      it('should get PDF processing progress', async () => {
        const mockProgress = {
          pdfId: 'pdf-123',
          status: 'processing',
          currentStep: 'text_extraction',
          progress: 0.65,
          totalPages: 150,
          processedPages: 97
        };

        setFetchResponse(200, mockProgress);

        const result = await api.pdf.getProcessingProgress('pdf-123');

        expect(fetch).toHaveBeenCalledWith(
          'http://localhost:8080/api/v1/pdfs/pdf-123/progress',
          {
            method: 'GET',
            credentials: 'include'
          }
        );
        expect(result).toEqual(mockProgress);
      });

      it('should encode pdfId in URL', async () => {
        setFetchResponse(200, {});

        await api.pdf.getProcessingProgress('pdf with spaces.pdf');

        expect(fetch).toHaveBeenCalledWith(
          expect.stringContaining('pdfs/pdf%20with%20spaces.pdf/progress'),
          expect.any(Object)
        );
      });

      it('should return null on 401', async () => {
        setFetchResponse(401);

        const result = await api.pdf.getProcessingProgress('pdf-123');

        expect(result).toBeNull();
      });

      it('should throw on 404 Not Found', async () => {
        setFetchResponse(404, { error: 'PDF not found' });

        await expect(api.pdf.getProcessingProgress('pdf-999')).rejects.toThrow(
          ApiError
        );
      });
    });

    describe('cancelProcessing', () => {
      it('should cancel PDF processing', async () => {
        setFetchResponse(204);

        await api.pdf.cancelProcessing('pdf-123');

        expect(fetch).toHaveBeenCalledWith(
          'http://localhost:8080/api/v1/pdfs/pdf-123/processing',
          {
            method: 'DELETE',
            credentials: 'include'
          }
        );
      });

      it('should encode pdfId in URL', async () => {
        setFetchResponse(204);

        await api.pdf.cancelProcessing('pdf/special');

        expect(fetch).toHaveBeenCalledWith(
          expect.stringContaining('pdfs/pdf%2Fspecial/processing'),
          expect.any(Object)
        );
      });

      it('should throw on 401 Unauthorized', async () => {
        setFetchResponse(401, null, { 'X-Correlation-Id': 'corr-pdf-1' });

        await expect(api.pdf.cancelProcessing('pdf-123')).rejects.toThrow(
          'Unauthorized'
        );
      });

      it('should throw on 404 Not Found', async () => {
        setFetchResponse(404, { error: 'PDF not found' });

        await expect(api.pdf.cancelProcessing('pdf-999')).rejects.toThrow(ApiError);
      });
    });
  });

  // ==================================================================
  // CHAT-05 + CHAT-06: Chat API (Lines 463-524)
  // ==================================================================
  describe('api.chat (CHAT-05 + CHAT-06)', () => {
    const chatId = 'chat-123';

    describe('exportChat (CHAT-05)', () => {
      beforeEach(() => {
        // Reset DOM mocks
        (document.createElement as jest.Mock).mockReturnValue({
          click: jest.fn(),
          href: '',
          download: ''
        });
      });

      it('should export chat as PDF', async () => {
        const request = { format: 'pdf' as const };
        setFetchResponse(200, {}, { 'Content-Disposition': 'attachment; filename="chat-export.pdf"' });

        await api.chat.exportChat(chatId, request);

        expect(fetch).toHaveBeenCalledWith(
          `http://localhost:8080/api/v1/chats/${chatId}/export`,
          {
            method: 'POST',
            credentials: 'include',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(request)
          }
        );
        expect(URL.createObjectURL).toHaveBeenCalled();
      });

      it('should export chat with date range', async () => {
        const request = {
          format: 'txt' as const,
          dateFrom: '2025-10-01',
          dateTo: '2025-10-31'
        };
        setFetchResponse(200, {}, { 'Content-Disposition': 'attachment; filename="chat-export.txt"' });

        await api.chat.exportChat(chatId, request);

        expect(fetch).toHaveBeenCalledWith(
          expect.any(String),
          expect.objectContaining({
            body: JSON.stringify(request)
          })
        );
      });

      it('should use fallback filename when Content-Disposition missing', async () => {
        const request = { format: 'md' as const };
        setFetchResponse(200, {}, {});

        await api.chat.exportChat(chatId, request);

        expect(document.createElement).toHaveBeenCalledWith('a');
      });

      it('should throw on 401 Unauthorized', async () => {
        const request = { format: 'pdf' as const };
        setFetchResponse(401, null, { 'X-Correlation-Id': 'corr-chat-1' });

        await expect(api.chat.exportChat(chatId, request)).rejects.toThrow(
          'Unauthorized'
        );
      });

      it('should throw on 403 Forbidden', async () => {
        const request = { format: 'pdf' as const };
        setFetchResponse(403, { error: 'Forbidden' }, { 'X-Correlation-Id': 'corr-chat-2' });

        await expect(api.chat.exportChat(chatId, request)).rejects.toThrow(
          'Forbidden'
        );
      });

      it('should throw on 404 Not Found', async () => {
        const request = { format: 'pdf' as const };
        setFetchResponse(404, { error: 'Chat not found' });

        await expect(api.chat.exportChat(chatId, request)).rejects.toThrow(ApiError);
      });

      it('should parse filename from Content-Disposition with quotes', async () => {
        const request = { format: 'pdf' as const };
        setFetchResponse(200, {}, { 'Content-Disposition': 'attachment; filename="my-chat.pdf"' });

        const mockElement = {
          click: jest.fn(),
          href: '',
          download: ''
        };
        (document.createElement as jest.Mock).mockReturnValue(mockElement);

        await api.chat.exportChat(chatId, request);

        expect(mockElement.download).toBe('my-chat.pdf');
      });

      it('should parse filename from Content-Disposition without quotes', async () => {
        const request = { format: 'txt' as const };
        setFetchResponse(200, {}, { 'Content-Disposition': 'attachment; filename=chat.txt' });

        const mockElement = {
          click: jest.fn(),
          href: '',
          download: ''
        };
        (document.createElement as jest.Mock).mockReturnValue(mockElement);

        await api.chat.exportChat(chatId, request);

        expect(mockElement.download).toBe('chat.txt');
      });
    });

    describe('updateMessage (CHAT-06)', () => {
      const messageId = 'msg-456';

      it('should update a chat message', async () => {
        const mockUpdated = {
          id: messageId,
          chatId,
          userId: 'user-1',
          level: 'user',
          content: 'Updated content',
          sequenceNumber: 1,
          createdAt: '2025-11-02T10:00:00Z',
          updatedAt: '2025-11-02T11:00:00Z',
          isDeleted: false,
          deletedAt: null,
          deletedByUserId: null,
          isInvalidated: false,
          metadataJson: null
        };

        setFetchResponse(200, mockUpdated);

        const result = await api.chat.updateMessage(chatId, messageId, 'Updated content');

        expect(fetch).toHaveBeenCalledWith(
          `http://localhost:8080/api/v1/chats/${chatId}/messages/${messageId}`,
          {
            method: 'PUT',
            credentials: 'include',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ content: 'Updated content' })
          }
        );
        expect(result).toEqual(mockUpdated);
      });

      it('should throw on 401 Unauthorized', async () => {
        setFetchResponse(401, null, { 'X-Correlation-Id': 'corr-msg-1' });

        await expect(
          api.chat.updateMessage(chatId, messageId, 'Content')
        ).rejects.toThrow('Unauthorized');
      });

      it('should throw on 403 Forbidden (not owner)', async () => {
        setFetchResponse(403, { error: 'Cannot update other users messages' });

        await expect(
          api.chat.updateMessage(chatId, messageId, 'Content')
        ).rejects.toThrow(ApiError);
      });

      it('should throw on 404 Not Found', async () => {
        setFetchResponse(404, { error: 'Message not found' });

        await expect(
          api.chat.updateMessage(chatId, messageId, 'Content')
        ).rejects.toThrow(ApiError);
      });
    });

    describe('deleteMessage (CHAT-06)', () => {
      const messageId = 'msg-789';

      it('should delete a chat message', async () => {
        setFetchResponse(204);

        await api.chat.deleteMessage(chatId, messageId);

        expect(fetch).toHaveBeenCalledWith(
          `http://localhost:8080/api/v1/chats/${chatId}/messages/${messageId}`,
          {
            method: 'DELETE',
            credentials: 'include'
          }
        );
      });

      it('should throw on 401 Unauthorized', async () => {
        setFetchResponse(401, null, { 'X-Correlation-Id': 'corr-msg-2' });

        await expect(api.chat.deleteMessage(chatId, messageId)).rejects.toThrow(
          'Unauthorized'
        );
      });

      it('should throw on 403 Forbidden', async () => {
        setFetchResponse(403, { error: 'Cannot delete other users messages' });

        await expect(api.chat.deleteMessage(chatId, messageId)).rejects.toThrow(
          ApiError
        );
      });

      it('should throw on 404 Not Found', async () => {
        setFetchResponse(404, { error: 'Message not found' });

        await expect(api.chat.deleteMessage(chatId, messageId)).rejects.toThrow(
          ApiError
        );
      });
    });
  });

  // TO BE CONTINUED IN PART 3:
  // - BGG API (AI-13)
  // - Configuration API (CONFIG-06) - Lines 549-727
  // - Bulk Operations (EDIT-07) - Lines 730-783
  // - Two-Factor Authentication (AUTH-07) - Lines 785-806
});
