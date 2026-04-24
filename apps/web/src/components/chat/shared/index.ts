/**
 * chat/shared — canonical chat primitives (Phase 0 Strangler Fig).
 * See ./types.ts for rationale and reconciliation notes.
 */
export type {
  Citation,
  CitationData,
  ChatMessageRole,
  ChatMessageItem,
  StreamStateForMessages,
} from './types';

export { collectCitations, getSuggestedQuestions } from './messages';
export { useChatScroll, type UseChatScrollResult } from './useChatScroll';
export {
  useThreadMessages,
  threadMessagesReducer,
  initialThreadMessagesState,
  type UseThreadMessagesResult,
  type UseThreadMessagesOptions,
  type ThreadSendContext,
  type SendOptions,
  type StreamStatus,
  type ThreadStreamError,
  type ThreadMessagesState,
  type ThreadMessagesAction,
} from './useThreadMessages';
