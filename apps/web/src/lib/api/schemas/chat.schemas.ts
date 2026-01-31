/**
 * Chat & Knowledge Base API Schemas (FE-IMP-005)
 *
 * Zod schemas for validating KnowledgeBase bounded context responses.
 * Covers: ChatThreads, Messages, RuleSpec Comments, Cache Stats
 */

import { z } from 'zod';

// ========== Chat Messages ==========

export const ChatMessageResponseSchema = z.object({
  id: z.string().uuid(),
  chatId: z.string().uuid(),
  userId: z.string().uuid().nullable(),
  level: z.string().min(1),
  content: z.string(),
  sequenceNumber: z.number().int().nonnegative(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime().nullable(),
  isDeleted: z.boolean(),
  deletedAt: z.string().datetime().nullable(),
  deletedByUserId: z.string().uuid().nullable(),
  isInvalidated: z.boolean(),
  metadataJson: z.string().nullable(),
});

export type ChatMessageResponse = z.infer<typeof ChatMessageResponseSchema>;

// ========== Chat Threads ==========

export const ChatThreadMessageDtoSchema = z.object({
  content: z.string().min(1),
  role: z.string().min(1),
  timestamp: z.string().datetime(),
  backendMessageId: z.string().uuid().optional(),
  endpoint: z.string().optional(),
  gameId: z.string().uuid().optional(),
  // Note: Keep in sync with FEEDBACK_OUTCOMES in @/lib/constants/feedback
  feedback: z.enum(['helpful', 'not-helpful', 'incorrect']).nullable().optional(),
});

export type ChatThreadMessageDto = z.infer<typeof ChatThreadMessageDtoSchema>;

export const ChatThreadDtoSchema = z.object({
  id: z.string().uuid(),
  gameId: z.string().uuid().nullable(),
  title: z.string().nullable(),
  createdAt: z.string().datetime(),
  lastMessageAt: z.string().datetime().nullable(),
  messageCount: z.number().int().nonnegative(),
  messages: z.array(ChatThreadMessageDtoSchema),
});

export type ChatThreadDto = z.infer<typeof ChatThreadDtoSchema>;

// ========== RuleSpec Comments ==========

export const RuleSpecCommentSchema: z.ZodType<RuleSpecComment> = z.lazy(() => z.object({
  id: z.string().uuid(),
  gameId: z.string().uuid(),
  version: z.string().min(1),
  atomId: z.string().nullable(),
  lineNumber: z.number().int().nullable(),
  lineContext: z.string().nullable(),
  parentCommentId: z.string().uuid().nullable(),
  replies: z.array(RuleSpecCommentSchema),
  userId: z.string().uuid(),
  userDisplayName: z.string().min(1),
  commentText: z.string().min(1),
  isResolved: z.boolean(),
  resolvedByUserId: z.string().uuid().nullable(),
  resolvedByDisplayName: z.string().nullable(),
  resolvedAt: z.string().datetime().nullable(),
  mentionedUserIds: z.array(z.string().uuid()),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime().nullable(),
}));

export type RuleSpecComment = {
  id: string;
  gameId: string;
  version: string;
  atomId: string | null;
  lineNumber: number | null;
  lineContext: string | null;
  parentCommentId: string | null;
  replies: RuleSpecComment[];
  userId: string;
  userDisplayName: string;
  commentText: string;
  isResolved: boolean;
  resolvedByUserId: string | null;
  resolvedByDisplayName: string | null;
  resolvedAt: string | null;
  mentionedUserIds: string[];
  createdAt: string;
  updatedAt: string | null;
};

export const RuleSpecCommentsResponseSchema = z.object({
  gameId: z.string().uuid(),
  version: z.string().min(1),
  comments: z.array(RuleSpecCommentSchema),
  totalComments: z.number().int().nonnegative(),
});

export type RuleSpecCommentsResponse = z.infer<typeof RuleSpecCommentsResponseSchema>;

// ========== Cache Statistics ==========

export const TopQuestionSchema = z.object({
  questionHash: z.string().min(1),
  hitCount: z.number().int().nonnegative(),
  missCount: z.number().int().nonnegative(),
  lastHitAt: z.string().datetime().nullable(),
});

export type TopQuestion = z.infer<typeof TopQuestionSchema>;

export const CacheStatsSchema = z.object({
  totalHits: z.number().int().nonnegative(),
  totalMisses: z.number().int().nonnegative(),
  hitRate: z.number().min(0).max(1),
  totalKeys: z.number().int().nonnegative(),
  cacheSizeBytes: z.number().int().nonnegative(),
  topQuestions: z.array(TopQuestionSchema),
});

export type CacheStats = z.infer<typeof CacheStatsSchema>;
