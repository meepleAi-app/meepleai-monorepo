/**
 * Chat Sessions API Schemas (Issue #3484)
 *
 * Zod schemas for validating ChatSession persistence API responses.
 * Connects to backend API from Issue #3483.
 */

import { z } from 'zod';

// ========== Session Chat Message ==========

export const SessionChatMessageDtoSchema = z.object({
  id: z.string().uuid(),
  role: z.enum(['user', 'assistant', 'system']),
  content: z.string().min(1),
  timestamp: z.string().datetime(),
  metadata: z.record(z.string(), z.unknown()).nullable().optional(),
});

export type SessionChatMessageDto = z.infer<typeof SessionChatMessageDtoSchema>;

// ========== Chat Session ==========

export const ChatSessionDtoSchema = z.object({
  id: z.string().uuid(),
  userId: z.string().uuid(),
  gameId: z.string().uuid(),
  gameTitle: z.string().nullable().optional(),
  title: z.string().nullable(),
  messageCount: z.number().int().nonnegative(),
  messages: z.array(SessionChatMessageDtoSchema),
  createdAt: z.string().datetime(),
  lastMessageAt: z.string().datetime().nullable(),
  isArchived: z.boolean(),
});

export type ChatSessionDto = z.infer<typeof ChatSessionDtoSchema>;

// ========== Chat Session Summary (for list views) ==========

export const ChatSessionSummaryDtoSchema = z.object({
  id: z.string().uuid(),
  userId: z.string().uuid(),
  gameId: z.string().uuid(),
  gameTitle: z.string().nullable().optional(),
  agentId: z.string().uuid().nullable().optional(),
  agentType: z.string().nullable().optional(),
  agentName: z.string().nullable().optional(),
  title: z.string().nullable(),
  messageCount: z.number().int().nonnegative(),
  lastMessagePreview: z.string().nullable().optional(),
  createdAt: z.string().datetime(),
  lastMessageAt: z.string().datetime().nullable(),
  isArchived: z.boolean(),
});

export type ChatSessionSummaryDto = z.infer<typeof ChatSessionSummaryDtoSchema>;

// ========== API Responses ==========

// Used by getByUserAndGame (backend returns ChatSessionListDto with Sessions+TotalCount)
export const ChatSessionListResponseSchema = z.object({
  sessions: z.array(ChatSessionSummaryDtoSchema),
  totalCount: z.number().int().nonnegative(),
});

// Used by getRecent (backend returns IReadOnlyList<ChatSessionSummaryDto> as plain array)
export const ChatSessionArraySchema = z.array(ChatSessionSummaryDtoSchema);

export type ChatSessionListResponse = z.infer<typeof ChatSessionListResponseSchema>;

// ========== Tier Limit ==========

export const ChatSessionTierLimitSchema = z.object({
  limit: z.number().int().nonnegative(),
  used: z.number().int().nonnegative(),
  tier: z.string(),
});

export type ChatSessionTierLimit = z.infer<typeof ChatSessionTierLimitSchema>;

// ========== Add Message Response ==========

// Backend returns AddChatSessionMessageResponse(Guid MessageId) — NOT a full ChatSessionDto
export const AddChatSessionMessageResponseSchema = z.object({
  messageId: z.string().uuid(),
});

export type AddChatSessionMessageResponse = z.infer<typeof AddChatSessionMessageResponseSchema>;

// ========== Request Types ==========

export interface CreateChatSessionRequest {
  userId: string;
  gameId: string;
  title?: string | null;
}

export interface AddChatSessionMessageRequest {
  sessionId: string;
  role: 'user' | 'assistant' | 'system';
  content: string;
  metadata?: Record<string, unknown> | null;
}
