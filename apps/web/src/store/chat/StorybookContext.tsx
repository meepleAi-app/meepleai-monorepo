/**
 * Storybook ChatContext Mock (Issue #1676)
 *
 * Provides ChatContext for Storybook isolation.
 * ONLY for Storybook/testing - production uses Zustand directly.
 *
 * Will be removed in PR #3 when Storybook migrates to ChatStoreProvider.
 */

import { createContext } from 'react';
import type { ChatThread, Message, Game } from '@/types';
import type { AgentDto } from '@/lib/api/schemas/agents.schemas';
import type { FeedbackOutcome } from '@/lib/constants/feedback';
import type { Citation } from '@/lib/api/schemas/streaming.schemas';

export interface ChatContextValue {
  authUser: unknown;
  games: Game[];
  selectedGameId: string | null;
  agents: AgentDto[];
  selectedAgentId: string | null;
  selectGame: (gameId: string | null) => void;
  selectAgent: (agentId: string | null) => void;
  chats: ChatThread[];
  activeChatId: string | null;
  messages: Message[];
  createChat: () => Promise<void>;
  deleteChat: (chatId: string) => Promise<void>;
  selectChat: (chatId: string) => Promise<void>;
  sendMessage: (content: string) => Promise<void>;
  setMessageFeedback: (messageId: string, feedback: FeedbackOutcome) => Promise<void>;
  editMessage: (messageId: string, content: string) => Promise<void>;
  deleteMessage: (messageId: string) => Promise<void>;
  loading: {
    games: boolean;
    agents: boolean;
    chats: boolean;
    messages: boolean;
    sending: boolean;
    creating: boolean;
    updating: boolean;
    deleting: boolean;
  };
  errorMessage: string;
  sidebarCollapsed: boolean;
  toggleSidebar: () => void;
  editingMessageId: string | null;
  editContent: string;
  setEditContent: (content: string) => void;
  startEditMessage: (messageId: string, content: string) => void;
  cancelEdit: () => void;
  saveEdit: () => Promise<void>;
  inputValue: string;
  setInputValue: (value: string) => void;
  searchMode: string;
  setSearchMode: (mode: string) => void;
  isStreaming: boolean;
  streamingAnswer: string;
  streamingState: string;
  streamingCitations: Citation[];
  stopStreaming: () => void;
}

export type { Citation };

export const ChatContext = createContext<ChatContextValue | null>(null);
