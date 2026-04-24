/**
 * `useThreadMessages` — Phase 1 Strangler Fig extraction (SCAFFOLD).
 *
 * Facade hook that owns the message list + streaming lifecycle for a single
 * chat thread. Replaces the scattered `useState<ChatMessageItem[]>` +
 * `qaAbortRef` + `lastMessageWasVoiceRef` machinery inside
 * `chat-unified/ChatThreadView.tsx` with a single `useReducer` + action API.
 *
 * STATUS: Task 2 — reducer + stubs only. `sendMessage`/`continueStream`
 * dispatch no-ops until Tasks 4-6 wire them to `qaStream`/`api.chat.*`.
 * `replaceMessages` and `abortCurrent` are functional from day one so they
 * can be dropped into ChatThreadView without further changes.
 *
 * Plan: docs/superpowers/plans/2026-04-24-chat-thread-state-hook.md
 * Invariants: apps/web/src/components/chat-unified/__tests__/ChatThreadView.invariants.test.tsx
 *
 * Module boundary: lives under `chat/shared/**` — MUST NOT import from
 * `chat-unified/**` or `chat/panel/**` (enforced by ESLint).
 */

import { useCallback, useEffect, useReducer, useRef } from 'react';

import type { ChatMessageItem } from './types';

// ---------------------------------------------------------------------------
// Public API types
// ---------------------------------------------------------------------------

export type StreamStatus = 'idle' | 'streaming' | 'error';

export interface SendOptions {
  fromVoice?: boolean;
}

export interface ThreadSendContext {
  gameId?: string;
  threadId: string;
  agentId?: string;
  responseStyle?: 'concise' | 'detailed';
}

export interface ThreadStreamError {
  kind: 'stream' | 'persist' | 'qa';
  message: string;
}

export interface UseThreadMessagesOptions {
  onError?: (err: ThreadStreamError) => void;
  onPersist?: (msg: ChatMessageItem) => Promise<void>;
  onStreamComplete?: (answer: string) => void;
}

export interface UseThreadMessagesResult {
  messages: ReadonlyArray<ChatMessageItem>;
  streamStatus: StreamStatus;
  currentAnswer: string;
  lastMessageWasVoice: boolean;
  sendMessage: (
    content: string,
    ctx: ThreadSendContext,
    options?: SendOptions
  ) => Promise<void>;
  continueStream: (token: string, ctx: Pick<ThreadSendContext, 'gameId'>) => Promise<void>;
  abortCurrent: () => void;
  replaceMessages: (next: ReadonlyArray<ChatMessageItem>) => void;
}

// ---------------------------------------------------------------------------
// Reducer — pure, unit-testable
// ---------------------------------------------------------------------------

export interface ThreadMessagesState {
  readonly messages: ReadonlyArray<ChatMessageItem>;
  readonly streamStatus: StreamStatus;
  readonly currentAnswer: string;
  readonly lastMessageWasVoice: boolean;
}

export type ThreadMessagesAction =
  | { type: 'APPEND'; message: ChatMessageItem }
  | { type: 'PATCH_BY_ID'; id: string; patch: Partial<ChatMessageItem> }
  | { type: 'REMOVE_BY_ID'; id: string }
  | { type: 'REPLACE_ALL'; messages: ReadonlyArray<ChatMessageItem> }
  | { type: 'SET_STREAM_STATUS'; status: StreamStatus }
  | { type: 'SET_CURRENT_ANSWER'; answer: string }
  | { type: 'SET_VOICE_FLAG'; flag: boolean };

export const initialThreadMessagesState: ThreadMessagesState = {
  messages: [],
  streamStatus: 'idle',
  currentAnswer: '',
  lastMessageWasVoice: false,
};

export function threadMessagesReducer(
  state: ThreadMessagesState,
  action: ThreadMessagesAction
): ThreadMessagesState {
  switch (action.type) {
    case 'APPEND':
      return { ...state, messages: [...state.messages, action.message] };

    case 'PATCH_BY_ID': {
      // No-op fast path: bail out if the id isn't present so React's
      // referential equality check can skip downstream re-renders.
      const idx = state.messages.findIndex(m => m.id === action.id);
      if (idx < 0) return state;
      const next = state.messages.slice();
      next[idx] = { ...next[idx], ...action.patch };
      return { ...state, messages: next };
    }

    case 'REMOVE_BY_ID': {
      const next = state.messages.filter(m => m.id !== action.id);
      if (next.length === state.messages.length) return state;
      return { ...state, messages: next };
    }

    case 'REPLACE_ALL':
      return { ...state, messages: action.messages };

    case 'SET_STREAM_STATUS':
      if (state.streamStatus === action.status) return state;
      return { ...state, streamStatus: action.status };

    case 'SET_CURRENT_ANSWER':
      if (state.currentAnswer === action.answer) return state;
      return { ...state, currentAnswer: action.answer };

    case 'SET_VOICE_FLAG':
      if (state.lastMessageWasVoice === action.flag) return state;
      return { ...state, lastMessageWasVoice: action.flag };

    default: {
      // Exhaustiveness guard — TS will flag a missing case if a new action
      // variant is added without a branch.
      const _exhaustive: never = action;
      void _exhaustive;
      return state;
    }
  }
}

// ---------------------------------------------------------------------------
// Hook — owns the reducer and the AbortController lifecycle
// ---------------------------------------------------------------------------

export function useThreadMessages(
  options: UseThreadMessagesOptions = {}
): UseThreadMessagesResult {
  const [state, dispatch] = useReducer(threadMessagesReducer, initialThreadMessagesState);
  const abortRef = useRef<AbortController | null>(null);

  // Stable callback refs so consumers that pass inline lambdas (e.g. the
  // transient renders during hydration) don't force `sendMessage` / `continueStream`
  // identity churn. Wired in Tasks 4-6.
  const optionsRef = useRef(options);
  useEffect(() => {
    optionsRef.current = options;
  }, [options]);

  const abortCurrent = useCallback(() => {
    abortRef.current?.abort();
    abortRef.current = null;
  }, []);

  const replaceMessages = useCallback((next: ReadonlyArray<ChatMessageItem>) => {
    dispatch({ type: 'REPLACE_ALL', messages: next });
  }, []);

  // Auto-abort in-flight stream on unmount. This is the single source of
  // truth for stream lifecycle; ChatThreadView's existing empty-deps cleanup
  // effect will be removed in Task 7.
  useEffect(() => {
    return () => {
      abortRef.current?.abort();
      abortRef.current = null;
    };
  }, []);

  // TASK 4-6 WIRING TARGETS -------------------------------------------------
  // These stubs intentionally only flip streamStatus so consumers can type
  // against the final shape without the reducer being in a half-wired state.

  const sendMessage = useCallback<UseThreadMessagesResult['sendMessage']>(
    async (_content, _ctx, _opts) => {
      // TODO(task-4/6): append user bubble, abort prior stream, open
      // qaStream, patch assistant placeholder, dispatch COMPLETE/ERROR.
      // Intentionally unimplemented in Task 2 so consumers cannot depend
      // on it prematurely.
      return Promise.resolve();
    },
    []
  );

  const continueStream = useCallback<UseThreadMessagesResult['continueStream']>(
    async (_token, _ctx) => {
      // TODO(task-5): resume qaStream with continuation token and patch
      // the last assistant message captured at call time.
      return Promise.resolve();
    },
    []
  );

  return {
    messages: state.messages,
    streamStatus: state.streamStatus,
    currentAnswer: state.currentAnswer,
    lastMessageWasVoice: state.lastMessageWasVoice,
    sendMessage,
    continueStream,
    abortCurrent,
    replaceMessages,
  };
}
