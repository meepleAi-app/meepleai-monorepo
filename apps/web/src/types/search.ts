/**
 * Search Types
 * Type definitions for advanced search functionality (Issue #1101)
 */

import type { Game, Agent, Message, ChatThread } from './domain';

/**
 * Search result type discriminator
 */
export type SearchResultType = 'message' | 'chat' | 'game' | 'agent' | 'pdf';

/**
 * Base search result interface
 */
export interface BaseSearchResult {
  id: string;
  type: SearchResultType;
  title: string;
  subtitle?: string;
  timestamp?: Date;
  relevanceScore: number;
}

/**
 * Message search result
 */
export interface MessageSearchResult extends BaseSearchResult {
  type: 'message';
  message: Message;
  chatId: string;
  gameId?: string;
  gameName?: string;
  agentName?: string;
}

/**
 * Chat search result
 */
export interface ChatSearchResult extends BaseSearchResult {
  type: 'chat';
  chat: ChatThread;
  gameId?: string;
  gameName?: string;
  messageCount: number;
}

/**
 * Game search result
 */
export interface GameSearchResult extends BaseSearchResult {
  type: 'game';
  game: Game;
}

/**
 * Agent search result
 */
export interface AgentSearchResult extends BaseSearchResult {
  type: 'agent';
  agent: Agent;
  gameId: string;
  gameName?: string;
}

/**
 * PDF search result
 * TODO: Add PDF document type when available
 */
export interface PdfSearchResult extends BaseSearchResult {
  type: 'pdf';
  documentId: string;
  fileName: string;
  gameId?: string;
  gameName?: string;
  language?: string; // TODO: Add when PDF language metadata is available
}

/**
 * Union type for all search results
 */
export type SearchResult =
  | MessageSearchResult
  | ChatSearchResult
  | GameSearchResult
  | AgentSearchResult
  | PdfSearchResult;

/**
 * Search filters
 */
export interface SearchFilters {
  gameId?: string;
  agentId?: string;
  dateFrom?: Date;
  dateTo?: Date;
  types?: SearchResultType[];
  language?: string; // TODO: Implement when PDF language is available
}

/**
 * Search options for Fuse.js
 */
export interface SearchOptions {
  query: string;
  filters?: SearchFilters;
  limit?: number;
  threshold?: number; // Fuzzy matching threshold (0.0 = perfect match, 1.0 = match anything)
}

/**
 * Recent search entry (for history)
 */
export interface RecentSearch {
  id: string;
  query: string;
  filters?: SearchFilters;
  timestamp: Date;
  resultCount: number;
}

/**
 * Search state for UI
 */
export interface SearchState {
  query: string;
  filters: SearchFilters;
  results: SearchResult[];
  isSearching: boolean;
  recentSearches: RecentSearch[];
}
