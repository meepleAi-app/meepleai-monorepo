/**
 * ShareLinks API Schemas (ISSUE-2052)
 *
 * Zod schemas for validating shareable chat thread links responses.
 * Covers: Create link, revoke link, get shared thread, add comment
 */

import { z } from 'zod';

// ========== Chat Message DTO for Shared Threads ==========

/**
 * Schema for chat message in shared thread (backend ChatMessageDto)
 */
export const ChatMessageDtoSchema = z.object({
  id: z.string().uuid(),
  content: z.string(),
  role: z.string(),
  timestamp: z.string().datetime(),
  sequenceNumber: z.number().int().nonnegative(),
  updatedAt: z.string().datetime().nullable(),
  isDeleted: z.boolean(),
  deletedAt: z.string().datetime().nullable(),
  deletedByUserId: z.string().uuid().nullable(),
  isInvalidated: z.boolean(),
});

export type ChatMessageDto = z.infer<typeof ChatMessageDtoSchema>;

// ========== ShareLink Operations ==========

/**
 * Schema for create share link response
 */
export const CreateShareLinkResponseSchema = z.object({
  shareLinkId: z.string().uuid(),
  token: z.string().min(1),
  shareableUrl: z.string().url(),
  expiresAt: z.string().datetime(),
  role: z.enum(['view', 'comment']),
});

export type CreateShareLinkResponse = z.infer<typeof CreateShareLinkResponseSchema>;

/**
 * Schema for revoke share link response
 */
export const RevokeShareLinkResponseSchema = z.object({
  success: z.boolean(),
});

export type RevokeShareLinkResponse = z.infer<typeof RevokeShareLinkResponseSchema>;

// ========== Public Shared Thread Access ==========

/**
 * Schema for get shared thread response
 */
export const GetSharedThreadResponseSchema = z.object({
  threadId: z.string().uuid(),
  title: z.string().nullable(),
  messages: z.array(ChatMessageDtoSchema),
  role: z.enum(['view', 'comment']),
  gameId: z.string().uuid().nullable(),
  createdAt: z.string().datetime(),
  lastMessageAt: z.string().datetime().nullable(),
});

export type GetSharedThreadResponse = z.infer<typeof GetSharedThreadResponseSchema>;

/**
 * Schema for add comment response
 */
export const AddCommentToSharedThreadResponseSchema = z.object({
  messageId: z.string().uuid(),
  timestamp: z.string().datetime(),
});

export type AddCommentToSharedThreadResponse = z.infer<
  typeof AddCommentToSharedThreadResponseSchema
>;

// ========== Error Responses ==========

/**
 * Share link specific errors
 */
export const ShareLinkErrorSchema = z.object({
  error: z.string(),
});

export type ShareLinkError = z.infer<typeof ShareLinkErrorSchema>;
