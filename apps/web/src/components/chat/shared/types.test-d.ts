/**
 * Type-level assertions for `chat/shared/types.ts`.
 *
 * Run via: `pnpm typecheck:types-test` (dedicated tsconfig.test-types.json).
 *
 * These tests use `@ts-expect-error` to assert that certain assignments
 * MUST fail. If the expected error disappears (e.g., a type widens
 * incorrectly) TypeScript fails the build because the directive suppresses
 * a non-existent error.
 */

import type { Citation, ChatMessageItem, ChatMessageRole, StreamStateForMessages } from './types';

// ────────────────────────────────────────────────────────────────────────
// ChatMessageRole: exactly 'user' | 'assistant'
// ────────────────────────────────────────────────────────────────────────

const _roleUser: ChatMessageRole = 'user';
const _roleAssistant: ChatMessageRole = 'assistant';

// @ts-expect-error — 'system' is not a valid ChatMessageRole
const _roleSystem: ChatMessageRole = 'system';

// @ts-expect-error — empty string is not a valid ChatMessageRole
const _roleEmpty: ChatMessageRole = '';

// ────────────────────────────────────────────────────────────────────────
// Citation: all required fields enforced, copyrightTier is a union
// ────────────────────────────────────────────────────────────────────────

const _validCitation: Citation = {
  documentId: 'doc-1',
  pageNumber: 12,
  snippet: 'text',
  relevanceScore: 0.9,
  copyrightTier: 'full',
};

const _validCitationWithOptional: Citation = {
  documentId: 'doc-1',
  pageNumber: 12,
  snippet: 'text',
  relevanceScore: 0.9,
  copyrightTier: 'protected',
  paraphrasedSnippet: 'paraphrase',
  isPublic: true,
};

// prettier-ignore
// @ts-expect-error — missing required field `documentId`
const _invalidCitationMissingId: Citation = { pageNumber: 12, snippet: 'text', relevanceScore: 0.9, copyrightTier: 'full' };

// prettier-ignore
// @ts-expect-error — copyrightTier must be 'full' | 'protected'
const _invalidCitationTier: Citation = { documentId: 'doc-1', pageNumber: 12, snippet: 'text', relevanceScore: 0.9, copyrightTier: 'unknown' };

// ────────────────────────────────────────────────────────────────────────
// ChatMessageItem: id, role, content are the only required fields
// ────────────────────────────────────────────────────────────────────────

const _minimalMessage: ChatMessageItem = {
  id: 'msg-1',
  role: 'user',
  content: 'hello',
};

// prettier-ignore
// @ts-expect-error — missing required `id`
const _messageMissingId: ChatMessageItem = { role: 'user', content: 'hello' };

// prettier-ignore
// @ts-expect-error — content must be a string, not a number
const _messageWrongContent: ChatMessageItem = { id: 'msg-1', role: 'user', content: 42 };

// ────────────────────────────────────────────────────────────────────────
// StreamStateForMessages: subset contract — unrelated keys must NOT leak
// ────────────────────────────────────────────────────────────────────────

declare const _fullStreamState: import('@/hooks/useAgentChatStream').AgentChatStreamState;

// A full stream state is assignable to the subset (Pick widens nothing).
const _subset: StreamStateForMessages = _fullStreamState;

// @ts-expect-error — `error` is not part of the subset
type _LeakError = StreamStateForMessages['error'];

// @ts-expect-error — `connectionStatus` is not part of the subset
type _LeakConnection = StreamStateForMessages['connectionStatus'];

// @ts-expect-error — `retryCount` is not part of the subset
type _LeakRetry = StreamStateForMessages['retryCount'];

// Silence unused-var noise at module boundary
export type __TypeTestMarker = typeof _roleUser & typeof _roleAssistant;
