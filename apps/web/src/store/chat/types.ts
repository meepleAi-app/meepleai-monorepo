/**
 * Zustand Chat Store Type Definitions (Issue #1083)
 *
 * Modular store slices for chat state management with:
 * - Session management (user selections, UI state)
 * - Game catalog
 * - Chat threads per game
 * - Messages per thread
 * - UI state (loading, errors)
 */

import { ChatThread, Message, Game, Agent } from '@/types';
import { AgentDto } from '@/lib/api/schemas/agents.schemas';
import type { FeedbackOutcome } from '@/lib/constants/feedback';

// ============================================================================
// Loading State
// ============================================================================

export interface LoadingState {
  chats: boolean;
  messages: boolean;
  sending: boolean;
  creating: boolean;
  updating: boolean;
  deleting: boolean;
  games: boolean;
  agents: boolean;
}

// ============================================================================
// Session Slice - User selections and UI state
// ============================================================================

export interface SessionState {
  selectedGameId: string | null;
  selectedAgentId: string | null;
  sidebarCollapsed: boolean;
  selectedDocumentIds: string[] | null; // Issue #2051: Document source filtering (null = all documents)
}

export interface SessionActions {
  selectGame: (gameId: string | null) => void;
  selectAgent: (agentId: string | null) => void;
  toggleSidebar: () => void;
  setSidebarCollapsed: (collapsed: boolean) => void;
  setSelectedDocuments: (documentIds: string[] | null) => void; // Issue #2051
}

export type SessionSlice = SessionState & SessionActions;

// ============================================================================
// Game Slice - Games and agents catalog
// ============================================================================

export interface GameState {
  games: Game[];
  agents: AgentDto[];
}

export interface GameActions {
  setGames: (games: Game[]) => void;
  setAgents: (agents: AgentDto[]) => void;
  loadGames: () => Promise<void>;
  loadAgents: () => Promise<void>; // Issue #868: Agents are global, not per-game
}

export type GameSlice = GameState & GameActions;

// ============================================================================
// Chat Slice - Thread management per game
// ============================================================================

export interface ChatState {
  chatsByGame: Record<string, ChatThread[]>;
  activeChatIds: Record<string, string | null>;
}

export interface ChatActions {
  loadChats: (gameId: string) => Promise<void>;
  createChat: () => Promise<void>;
  deleteChat: (chatId: string) => Promise<void>;
  selectChat: (chatId: string) => Promise<void>;
  updateChatTitle: (chatId: string, title: string) => void;
}

export type ChatSlice = ChatState & ChatActions;

// ============================================================================
// Messages Slice - Messages per thread with optimistic updates
// ============================================================================

export interface MessagesState {
  messagesByChat: Record<string, Message[]>;
}

export interface MessagesActions {
  loadMessages: (threadId: string) => Promise<void>;
  sendMessage: (content: string) => Promise<void>;
  editMessage: (messageId: string, content: string) => Promise<void>;
  deleteMessage: (messageId: string) => Promise<void>;
  setMessageFeedback: (messageId: string, feedback: FeedbackOutcome) => Promise<void>;
  addOptimisticMessage: (message: Message, threadId: string) => void;
  removeOptimisticMessage: (messageId: string, threadId: string) => void;
  updateMessageInThread: (threadId: string, messageId: string, updates: Partial<Message>) => void;
}

export type MessagesSlice = MessagesState & MessagesActions;

// ============================================================================
// UI Slice - Loading, errors, input, editing, search mode
// ============================================================================

export interface UIState {
  loading: LoadingState;
  error: string | null;
  // Message input
  inputValue: string;
  // Message editing
  editingMessageId: string | null;
  editContent: string;
  // Search mode
  searchMode: string;
}

export interface UIActions {
  setLoading: (key: keyof LoadingState, value: boolean) => void;
  setError: (error: string | null) => void;
  clearError: () => void;
  // Input actions
  setInputValue: (value: string) => void;
  // Editing actions
  startEdit: (messageId: string, content: string) => void;
  cancelEdit: () => void;
  saveEdit: (editMessageFn: (messageId: string, content: string) => Promise<void>) => Promise<void>;
  setEditContent: (content: string) => void;
  // Search mode
  setSearchMode: (mode: string) => void;
}

export type UISlice = UIState & UIActions;

// ============================================================================
// Combined Store
// ============================================================================

export type ChatStore = SessionSlice & GameSlice & ChatSlice & MessagesSlice & UISlice;

// ============================================================================
// Undo/Redo Config
// ============================================================================

export interface UndoableAction {
  type: 'message_sent' | 'message_edited' | 'message_deleted';
  messageId: string;
  threadId: string;
  previousState?: Message;
  newState?: Message;
}

// ============================================================================
// Store Selectors (for performance optimization)
// ============================================================================

export interface StoreSelectors {
  // Session selectors
  useSelectedGameId: () => string | null;
  useSelectedAgentId: () => string | null;
  useSidebarCollapsed: () => boolean;

  // Game selectors
  useGames: () => Game[];
  useAgents: () => Agent[];
  useSelectedGame: () => Game | undefined;
  useSelectedAgent: () => Agent | undefined;

  // Chat selectors
  useChats: () => ChatThread[];
  useActiveChatId: () => string | null;
  useActiveChat: () => ChatThread | null;

  // Message selectors
  useMessages: () => Message[];
  useMessagesForChat: (chatId: string) => Message[];

  // UI selectors
  useLoading: () => LoadingState;
  useError: () => string | null;
  useIsCreating: () => boolean;
  useIsSending: () => boolean;
}
