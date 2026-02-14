/**
 * Comments API helper
 * Issue #4368: Extracted from deleted chatClient.ts
 *
 * Provides RuleSpec comment CRUD operations.
 */

import { getApiBase } from '@/lib/api';

import type {
  RuleSpecCommentsResponse,
  CreateRuleSpecCommentRequest,
  UpdateRuleSpecCommentRequest,
  CreateReplyRequest,
} from './types';

const BASE = () => getApiBase();

async function fetchJson<T>(url: string, options?: RequestInit): Promise<T> {
  const res = await fetch(url, {
    credentials: 'include',
    ...options,
    headers: {
      'Content-Type': 'application/json',
      ...options?.headers,
    },
  });
  if (!res.ok) {
    throw new Error(`HTTP ${res.status}: ${res.statusText}`);
  }
  return res.json();
}

export const commentsApi = {
  getRuleSpecComments: (gameId: string, version: string, includeResolved: boolean) =>
    fetchJson<RuleSpecCommentsResponse>(
      `${BASE()}/api/v1/chat/games/${gameId}/rulespec/${version}/comments?includeResolved=${includeResolved}`
    ),

  createRuleSpecComment: (gameId: string, version: string, data: CreateRuleSpecCommentRequest) =>
    fetchJson<void>(`${BASE()}/api/v1/chat/games/${gameId}/rulespec/${version}/comments`, {
      method: 'POST',
      body: JSON.stringify(data),
    }),

  updateRuleSpecComment: (gameId: string, commentId: string, data: UpdateRuleSpecCommentRequest) =>
    fetchJson<void>(`${BASE()}/api/v1/chat/comments/${commentId}`, {
      method: 'PUT',
      body: JSON.stringify(data),
    }),

  deleteRuleSpecComment: (gameId: string, commentId: string) =>
    fetchJson<void>(`${BASE()}/api/v1/chat/comments/${commentId}`, {
      method: 'DELETE',
    }),

  createCommentReply: (parentCommentId: string, data: CreateReplyRequest) =>
    fetchJson<void>(`${BASE()}/api/v1/chat/comments/${parentCommentId}/replies`, {
      method: 'POST',
      body: JSON.stringify(data),
    }),

  resolveComment: (commentId: string) =>
    fetchJson<void>(`${BASE()}/api/v1/chat/comments/${commentId}/resolve`, {
      method: 'POST',
    }),

  unresolveComment: (commentId: string) =>
    fetchJson<void>(`${BASE()}/api/v1/chat/comments/${commentId}/unresolve`, {
      method: 'POST',
    }),
};
