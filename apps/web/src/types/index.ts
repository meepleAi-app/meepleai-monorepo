/**
 * Centralized Type Definitions
 * Single import point for all application types
 *
 * Usage:
 *   import { AuthUser, Game, RuleSpec, ApiError } from '@/types'
 */

// Authentication & Session Types
export type {
  AuthUser,
  AuthResponse,
  SessionStatusResponse,
  UserRole
} from './auth';

export {
  hasRole,
  canEdit
} from './auth';

// Core Domain Types
export type {
  Game,
  Agent,
  Chat,
  ChatMessage,
  ChatThread,
  ChatThreadMessage,
  ChatWithHistory,
  RuleAtom,
  RuleSpec,
  RuleSpecComment,
  Snippet,
  Citation, // #859: PDF citation type
  Message,
  QaResponse,
  SetupStep,
  SetupGuideResponse
} from './domain';

// API Contract Types
export type {
  RuleSpecCommentsResponse,
  CreateRuleSpecCommentRequest,
  UpdateRuleSpecCommentRequest,
  ChatMessageResponse,
  ExportFormat,
  ExportChatRequest,
  TopQuestion,
  CacheStats,
  ValidationResult,
  PdfValidationError,
  ApiResponse,
  PaginatedResponse
} from './api';

export {
  ApiError,
  createApiError
} from './api';

// PDF Processing Types (FE-IMP-005: Simplified, using API schemas)
export {
  isProcessingComplete,
} from './pdf';

export type {
  ProcessingProgress
} from './pdf';

// Search Types (#1101)
export type {
  SearchResultType,
  BaseSearchResult,
  MessageSearchResult,
  ChatSearchResult,
  GameSearchResult,
  AgentSearchResult,
  PdfSearchResult,
  SearchResult,
  SearchFilters,
  SearchOptions,
  RecentSearch,
  SearchState
} from './search';
