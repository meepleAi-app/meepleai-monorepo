/**
 * Core Domain Types
 * Centralized type definitions for core business entities
 */

/**
 * Game entity
 */
export interface Game {
  id: string;
  name: string;
  createdAt?: string;
}

/**
 * Agent entity (AI assistant for a specific game)
 */
export interface Agent {
  id: string;
  gameId: string;
  name: string;
  kind: string;
  createdAt: string;
}

/**
 * Chat session entity
 */
export interface Chat {
  id: string;
  gameId: string;
  gameName: string;
  agentId: string;
  agentName: string;
  startedAt: string;
  lastMessageAt: string | null;
}

/**
 * Chat message entity (CHAT-06: with edit/delete support)
 */
export interface ChatMessage {
  id: string;
  level: string;
  message: string;
  metadataJson: string | null;
  createdAt: string;
  updatedAt: string | null;
  isDeleted: boolean;
  isInvalidated: boolean;
}

/**
 * ChatThread entity (SPRINT-3: DDD KnowledgeBase)
 * Represents a conversation thread with messages
 */
export interface ChatThread {
  id: string;
  userId?: string; // Added for Issue #858
  gameId: string | null;
  title: string | null;
  status?: string; // Added for Issue #858: "Active" | "Closed"
  createdAt: string;
  lastMessageAt: string | null;
  messageCount: number;
  messages: ChatThreadMessage[];
}

/**
 * ChatThread message entity (SPRINT-3: DDD KnowledgeBase)
 */
export interface ChatThreadMessage {
  content: string;
  role: string;
  timestamp: string;
  // Optional metadata fields for ChatProvider compatibility (SPRINT-3 #858)
  backendMessageId?: string;
  endpoint?: string;
  gameId?: string;
  feedback?: 'helpful' | 'not-helpful' | null;
}

/**
 * Chat with full message history
 */
export interface ChatWithHistory extends Chat {
  messages: ChatMessage[];
}

/**
 * Rule atom - individual rule from a rulebook
 */
export interface RuleAtom {
  id: string;
  text: string;
  section?: string | null;
  page?: string | null;
  line?: string | null;
}

/**
 * RuleSpec - complete game rules specification
 */
export interface RuleSpec {
  gameId: string;
  version: string;
  createdAt: string;
  rules: RuleAtom[];
}

/**
 * RuleSpec comment (for collaborative editing)
 */
export interface RuleSpecComment {
  id: string;
  gameId: string;
  version: string;
  atomId: string | null;
  userId: string;
  userDisplayName: string;
  commentText: string;
  createdAt: string;
  updatedAt: string | null;
}

/**
 * Source snippet from RAG retrieval
 */
export interface Snippet {
  text: string;
  source: string;
  page?: number | null;
  line?: number | null;
}

/**
 * Citation from RAG response with relevance scoring (Issue #859, BGAI-074)
 */
export interface Citation {
  documentId: string;
  pageNumber: number;
  snippet: string;
  relevanceScore: number;
}

/**
 * Message in chat UI (combines user/assistant messages)
 */
export interface Message {
  id: string;
  role: 'user' | 'assistant';
  content: string;
  snippets?: Snippet[];
  citations?: Citation[]; // Issue #859: RAG citations with relevance scoring
  followUpQuestions?: string[];
  feedback?: 'helpful' | 'not-helpful' | null;
  endpoint?: string;
  gameId?: string;
  timestamp: Date;
  backendMessageId?: string;
  updatedAt?: string | null;
  isDeleted?: boolean;
  isInvalidated?: boolean;
  isOptimistic?: boolean; // Flag for optimistically added messages awaiting confirmation
}

/**
 * Q&A response from agent
 */
export interface QaResponse {
  answer: string;
  snippets?: Snippet[];
  followUpQuestions?: string[];
  messageId?: string;
}

/**
 * Setup guide step
 */
export interface SetupStep {
  stepNumber: number;
  description: string;
  isOptional: boolean;
  estimatedTimeMinutes?: number;
  citations?: Snippet[];
}

/**
 * Setup guide response
 */
export interface SetupGuideResponse {
  steps: SetupStep[];
  totalSteps: number;
  estimatedTimeMinutes: number;
  confidence?: number;
}
