/**
 * Canonical chat domain types — shared between chat-unified/ and chat/panel/.
 *
 * Phase 0 (Strangler Fig migration): this module is the single source of truth
 * for chat primitive types. Both `components/chat-unified/*` and
 * `components/chat/panel/*` consume from here.
 *
 * Reconciliation notes:
 *   - `Citation` is re-exported from `@/types` (global domain type at
 *     `src/types/domain.ts:134`). It is canonical and pre-existing.
 *   - `CitationData` (previously defined in `chat-unified/CitationSheet.tsx`)
 *     is a deprecated alias of `Citation`. The shapes were structurally
 *     equivalent except for the optional `isPublic?: boolean` field on
 *     `Citation` — safely assignable both directions.
 *   - `ChatCitation` (view-model in `chat/panel/ChatCitationCard.tsx`) is
 *     intentionally NOT moved here. It is a UI projection, not a domain type.
 *   - `DebugStep` and `AgentChatStreamState` remain owned by
 *     `@/hooks/useAgentChatStream` (stream state is the hook's contract).
 *
 * Modification policy: until Phase 0 is fully consumed by chat/panel, this
 * file MUST remain structurally identical to the legacy declarations. Any
 * widening / narrowing requires a coordinated migration PR.
 */

import type { AgentChatStreamState } from '@/hooks/useAgentChatStream';
import type { InlineCitationMatch } from '@/lib/api/clients/chatClient';
import type { Citation as DomainCitation } from '@/types';

/**
 * Canonical citation from a RAG response with relevance scoring.
 * Re-exported from `@/types` as the single source of truth.
 */
export type Citation = DomainCitation;

/**
 * @deprecated Use {@link Citation} directly. Retained as a structural alias
 * for backward compatibility with existing `chat-unified/CitationSheet`
 * imports. Scheduled for removal after chat/panel fully consumes Phase 0.
 */
export type CitationData = Citation;

/**
 * Role of a chat message (user-authored or assistant-authored).
 * Used by ChatMessageList (chat-unified) and ChatMessageBubble (chat/panel).
 */
export type ChatMessageRole = 'user' | 'assistant';

/**
 * Message item rendered in both chat-unified and chat/panel message lists.
 *
 * Historical home: `components/chat-unified/ChatMessageList.tsx`.
 * This definition MUST remain identical to the legacy export until a post-
 * Phase-0 cleanup removes the legacy module.
 */
export interface ChatMessageItem {
  id: string;
  role: ChatMessageRole;
  content: string;
  timestamp?: string;
  citations?: Citation[];
  followUpQuestions?: string[];
  inlineCitations?: InlineCitationMatch[];
  snippets?: Array<{
    text: string;
    source: string;
    page: number;
    line: number;
    score: number;
  }>;
  continuationToken?: string;
}

/**
 * Subset of `AgentChatStreamState` required by message-rendering components.
 * Declared as a structural `Pick<>` so changes to the full stream state flow
 * through automatically without re-syncing this file.
 */
export type StreamStateForMessages = Pick<
  AgentChatStreamState,
  | 'isStreaming'
  | 'currentAnswer'
  | 'statusMessage'
  | 'strategyTier'
  | 'executionId'
  | 'debugSteps'
  | 'modelDowngrade'
>;
