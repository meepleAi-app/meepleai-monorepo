/**
 * Core Domain Types
 * Centralized type definitions for core business entities
 */

import type { FeedbackOutcome } from '@/lib/constants/feedback';

/**
 * Game entity
 */
export interface Game {
  id: string;
  title: string;
  createdAt?: string;
}

/**
 * Agent entity (AI assistant for a specific game)
 * Issue #1977: Aligned with backend AgentDto (removed gameId)
 */
export interface Agent {
  id: string;
  name: string;
  type: string;
  strategyName: string;
  strategyParameters: Record<string, unknown>;
  isActive: boolean;
  createdAt: string;
  lastInvokedAt: string | null;
  invocationCount: number;
  isRecentlyUsed: boolean;
  isIdle: boolean;
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
  feedback?: FeedbackOutcome | null;
}

/**
 * Chat with full message history
 */
export interface ChatWithHistory extends Chat {
  messages: ChatMessage[];
}

/**
 * NOTE (Issue #1977): RuleAtom and RuleSpec are now defined
 * in games.schemas.ts and should be imported from there, not from this file.
 * Use: import { RuleAtom, RuleSpec } from '@/lib/api/schemas'
 */

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
 * Issue #1977: Removed fen (use analysis.fenPosition from ChessAgentResponse)
 */
export interface Message {
  id: string;
  role: 'user' | 'assistant';
  content: string;
  snippets?: Snippet[];
  citations?: Citation[]; // Issue #859: RAG citations with relevance scoring
  followUpQuestions?: string[];
  suggestedMoves?: string[]; // Chess agent suggested moves
  analysis?: {
    fenPosition?: string | null;
    evaluationSummary?: string | null;
    keyConsiderations?: string[];
  }; // Chess analysis data
  feedback?: FeedbackOutcome | null;
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
 * Q&A response from agent (Issue #1002: BGAI-062)
 * Aligned with backend QaResponseDto
 */
export interface QaResponse {
  answer: string;
  snippets?: Snippet[];
  followUpQuestions?: string[];
  messageId?: string;
  /** Search/retrieval confidence score (0-1) */
  searchConfidence?: number;
  /** LLM generation confidence score (0-1) */
  llmConfidence?: number;
  /** Overall confidence score (0-1) - Primary metric for UI display */
  overallConfidence?: number;
  /** Flag indicating if response quality is below threshold */
  isLowQuality?: boolean;
  /** RAG citations with page numbers and snippets */
  citations?: Citation[];
}

/**
 * NOTE (Issue #1977): SetupGuideResponseStep and SetupGuideResponse are now defined
 * in agents.schemas.ts and should be imported from there, not from this file.
 * Use: import { SetupGuideResponse, SetupGuideResponseStep } from '@/lib/api/schemas'
 */
