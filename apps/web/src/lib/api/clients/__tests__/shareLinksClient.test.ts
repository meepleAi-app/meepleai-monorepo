/**
 * ShareLinks Client Tests - Issue #2340
 * Coverage: 9 tests for share link creation, revocation, access, and comments
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { createShareLinksClient } from '../shareLinksClient';
import type { HttpClient } from '../../core/httpClient';

const mockHttpClient: HttpClient = {
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
  delete: vi.fn(),
  patch: vi.fn(),
} as any;

describe('ShareLinksClient - Issue #2340', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('createShareLink', () => {
    it('should create shareable link for chat thread', async () => {
      const mockResponse = {
        token: 'share_token_123',
        threadId: 'thread_abc',
        role: 'view',
        expiryDate: '2024-12-31',
      };
      vi.mocked(mockHttpClient.post).mockResolvedValue(mockResponse);

      const client = createShareLinksClient({ httpClient: mockHttpClient });
      const result = await client.createShareLink({
        threadId: 'thread_abc',
        role: 'view',
        expiryDays: 30,
      });

      expect(result).toEqual(mockResponse);
      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/share-links',
        expect.objectContaining({ threadId: 'thread_abc', role: 'view' }),
        expect.any(Object)
      );
    });

    it('should create share link with comment role', async () => {
      const mockResponse = {
        token: 'share_token_456',
        threadId: 'thread_xyz',
        role: 'comment',
        expiryDate: '2024-06-30',
      };
      vi.mocked(mockHttpClient.post).mockResolvedValue(mockResponse);

      const client = createShareLinksClient({ httpClient: mockHttpClient });
      const result = await client.createShareLink({
        threadId: 'thread_xyz',
        role: 'comment',
        label: 'Review Feedback',
      });

      expect(result.role).toBe('comment');
      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/share-links',
        expect.objectContaining({ role: 'comment' }),
        expect.any(Object)
      );
    });

    it('should pass optional parameters to API', async () => {
      vi.mocked(mockHttpClient.post).mockResolvedValue({
        token: 'share_token_789',
        threadId: 'thread_123',
        role: 'view',
      });

      const client = createShareLinksClient({ httpClient: mockHttpClient });
      await client.createShareLink({
        threadId: 'thread_123',
        role: 'view',
        expiryDays: 7,
        label: 'Week Review',
      });

      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/share-links',
        expect.objectContaining({
          threadId: 'thread_123',
          expiryDays: 7,
          label: 'Week Review',
        }),
        expect.any(Object)
      );
    });
  });

  describe('revokeShareLink', () => {
    it('should revoke share link by ID', async () => {
      vi.mocked(mockHttpClient.delete).mockResolvedValue(undefined);

      const client = createShareLinksClient({ httpClient: mockHttpClient });
      const result = await client.revokeShareLink('share_link_123');

      expect(result).toEqual({ success: true });
      expect(mockHttpClient.delete).toHaveBeenCalledWith('/api/v1/share-links/share_link_123');
    });

    it('should handle revocation of non-existent link', async () => {
      vi.mocked(mockHttpClient.delete).mockResolvedValue(undefined);

      const client = createShareLinksClient({ httpClient: mockHttpClient });
      const result = await client.revokeShareLink('nonexistent_id');

      expect(result.success).toBe(true);
      expect(mockHttpClient.delete).toHaveBeenCalledWith('/api/v1/share-links/nonexistent_id');
    });

    it('should revoke share link with special characters in ID', async () => {
      vi.mocked(mockHttpClient.delete).mockResolvedValue(undefined);

      const client = createShareLinksClient({ httpClient: mockHttpClient });
      await client.revokeShareLink('share_link_123/456');

      expect(mockHttpClient.delete).toHaveBeenCalledWith(expect.stringContaining('share_link_123'));
    });
  });

  describe('getSharedThread', () => {
    it('should fetch shared thread by token', async () => {
      const mockThread = {
        threadId: 'thread_abc',
        title: 'Game Rules Review',
        messages: [{ id: '1', content: 'Question about rules' }],
        role: 'view',
      };
      vi.mocked(mockHttpClient.get).mockResolvedValue(mockThread);

      const client = createShareLinksClient({ httpClient: mockHttpClient });
      const result = await client.getSharedThread('valid_token_123');

      expect(result).toEqual(mockThread);
      expect(mockHttpClient.get).toHaveBeenCalledWith(
        expect.stringContaining('token=valid_token_123'),
        expect.any(Object)
      );
    });

    it('should throw error for invalid token', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue(null);

      const client = createShareLinksClient({ httpClient: mockHttpClient });

      await expect(client.getSharedThread('invalid_token')).rejects.toThrow(
        'Thread not found or access denied'
      );
    });

    it('should encode special characters in token', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue({
        threadId: 'thread_123',
        title: 'Review',
      });

      const client = createShareLinksClient({ httpClient: mockHttpClient });
      await client.getSharedThread('token+with/special=chars');

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        expect.stringContaining('token%2Bwith%2Fspecial%3Dchars'),
        expect.any(Object)
      );
    });

    it('should return thread with empty messages', async () => {
      const mockThread = {
        threadId: 'thread_empty',
        title: 'Empty Thread',
        messages: [],
        role: 'view',
      };
      vi.mocked(mockHttpClient.get).mockResolvedValue(mockThread);

      const client = createShareLinksClient({ httpClient: mockHttpClient });
      const result = await client.getSharedThread('token_empty');

      expect(result.messages).toEqual([]);
    });
  });

  describe('addCommentToSharedThread', () => {
    it('should add comment to shared thread', async () => {
      const mockResponse = {
        commentId: 'comment_123',
        token: 'token_abc',
        content: 'Great rules!',
        createdAt: '2024-01-15',
      };
      vi.mocked(mockHttpClient.post).mockResolvedValue(mockResponse);

      const client = createShareLinksClient({ httpClient: mockHttpClient });
      const result = await client.addCommentToSharedThread({
        token: 'token_abc',
        content: 'Great rules!',
      });

      expect(result).toEqual(mockResponse);
      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/shared/thread/comment',
        expect.objectContaining({ token: 'token_abc', content: 'Great rules!' }),
        expect.any(Object)
      );
    });

    it('should add comment with special characters', async () => {
      const mockResponse = {
        commentId: 'comment_456',
        token: 'token_xyz',
        content: 'Question: How do you play? @user',
      };
      vi.mocked(mockHttpClient.post).mockResolvedValue(mockResponse);

      const client = createShareLinksClient({ httpClient: mockHttpClient });
      const result = await client.addCommentToSharedThread({
        token: 'token_xyz',
        content: 'Question: How do you play? @user',
      });

      expect(result.content).toContain('How do you play?');
      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/shared/thread/comment',
        expect.objectContaining({
          content: 'Question: How do you play? @user',
        }),
        expect.any(Object)
      );
    });

    it('should handle long comment content', async () => {
      const longContent = 'A'.repeat(1000);
      const mockResponse = {
        commentId: 'comment_long',
        token: 'token_long',
        content: longContent,
      };
      vi.mocked(mockHttpClient.post).mockResolvedValue(mockResponse);

      const client = createShareLinksClient({ httpClient: mockHttpClient });
      const result = await client.addCommentToSharedThread({
        token: 'token_long',
        content: longContent,
      });

      expect(result.content.length).toBe(1000);
    });

    it('should fail to add comment without comment role', async () => {
      const mockError = new Error('Share link does not have comment role');
      vi.mocked(mockHttpClient.post).mockRejectedValue(mockError);

      const client = createShareLinksClient({ httpClient: mockHttpClient });

      await expect(
        client.addCommentToSharedThread({
          token: 'view_only_token',
          content: 'This comment should fail',
        })
      ).rejects.toThrow();
    });

    it('should respect rate limiting on comment submission', async () => {
      const mockError = new Error('Rate limit exceeded: 10 comments/hour');
      vi.mocked(mockHttpClient.post).mockRejectedValue(mockError);

      const client = createShareLinksClient({ httpClient: mockHttpClient });

      await expect(
        client.addCommentToSharedThread({
          token: 'rate_limited_token',
          content: 'Comment after limit',
        })
      ).rejects.toThrow('Rate limit exceeded');
    });
  });
});
