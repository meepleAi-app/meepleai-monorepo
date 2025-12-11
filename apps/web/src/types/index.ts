/**
 * Centralized Type Definitions
 * Single import point for all application types
 *
 * Usage:
 *   import { AuthUser, Game, RuleSpec, ApiError } from '@/types'
 */

// Authentication & Session Types
export type { AuthUser, AuthResponse, SessionStatusResponse, UserRole } from './auth';

export { hasRole, canEdit } from './auth';

// Core Domain Types
// Issue #1977: RuleAtom, RuleSpec, SetupStep, SetupGuideResponse moved to @/lib/api/schemas
export type {
  Game,
  Agent,
  Chat,
  ChatMessage,
  ChatThread,
  ChatThreadMessage,
  ChatWithHistory,
  RuleSpecComment,
  Snippet,
  Citation,
  Message,
  QaResponse,
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
  PaginatedResponse,
} from './api';

export { ApiError, createApiError } from './api';

// PDF Processing Types
export { ProcessingStep, isProcessingComplete, getStepLabel, getStepOrder } from './pdf';

export type { ProcessingProgress } from './pdf';

// Search Types
export type {
  SearchFilters,
  SearchResult,
  SearchResultType,
  MessageSearchResult,
  ChatSearchResult,
  GameSearchResult,
  AgentSearchResult,
  PdfSearchResult,
  SearchOptions,
  RecentSearch,
  SearchState,
} from './search';

// API Key Filter Types
export type {
  ApiKeyFilters,
  ApiKeyStatus,
  ApiKeyScope,
  ScopeOption,
  StatusOption,
} from './api-key-filters';

export { AVAILABLE_SCOPES, AVAILABLE_STATUSES } from './api-key-filters';
