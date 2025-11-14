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

// PDF Processing Types
export {
  ProcessingStep,
  isProcessingComplete,
  getStepLabel,
  getStepOrder
} from './pdf';

export type {
  ProcessingProgress
} from './pdf';
