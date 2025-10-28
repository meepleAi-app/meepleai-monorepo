/**
 * API Contract Types
 * Request/response types for API endpoints
 */

import type { RuleSpecComment } from './domain';

/**
 * RuleSpec comments response
 */
export interface RuleSpecCommentsResponse {
  gameId: string;
  version: string;
  comments: RuleSpecComment[];
  totalComments: number;
}

/**
 * Create RuleSpec comment request
 */
export interface CreateRuleSpecCommentRequest {
  atomId: string | null;
  commentText: string;
}

/**
 * Update RuleSpec comment request
 */
export interface UpdateRuleSpecCommentRequest {
  commentText: string;
}

/**
 * Chat message response (from API)
 */
export interface ChatMessageResponse {
  id: string;
  chatId: string;
  userId: string | null;
  level: string;
  content: string;
  sequenceNumber: number;
  createdAt: string;
  updatedAt: string | null;
  isDeleted: boolean;
  deletedAt: string | null;
  deletedByUserId: string | null;
  isInvalidated: boolean;
  metadataJson: string | null;
}

/**
 * Chat export format
 */
export type ExportFormat = 'pdf' | 'txt' | 'md';

/**
 * Chat export request
 */
export interface ExportChatRequest {
  format: ExportFormat;
  dateFrom?: string;
  dateTo?: string;
}

/**
 * Cache statistics - top question
 */
export interface TopQuestion {
  questionHash: string;
  hitCount: number;
  missCount: number;
  lastHitAt: string | null;
}

/**
 * Cache statistics response (PERF-03)
 */
export interface CacheStats {
  totalHits: number;
  totalMisses: number;
  hitRate: number;
  totalKeys: number;
  cacheSizeBytes: number;
  topQuestions: TopQuestion[];
}

/**
 * Enhanced API error with correlation ID (PDF-06)
 */
export class ApiError extends Error {
  constructor(
    message: string,
    public statusCode?: number,
    public correlationId?: string,
    public response?: Response
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

/**
 * Helper to create ApiError from response
 */
export async function createApiError(path: string, response: Response): Promise<ApiError> {
  const correlationId = typeof response.headers?.get === 'function'
    ? response.headers.get('X-Correlation-Id') || undefined
    : undefined;
  const status = typeof response.status === 'number' ? response.status : 500;
  let errorMessage = `API ${path} ${status}`;

  // Try to extract error message from response body
  try {
    const body = await response.json();
    if (body?.error) {
      errorMessage = body.error;
    }
  } catch {
    // If JSON parsing fails, use default message
  }

  return new ApiError(errorMessage, status, correlationId, response);
}

/**
 * Validation result
 */
export interface ValidationResult {
  valid: boolean;
  errors: string[];
}

/**
 * PDF validation error details
 */
export interface PdfValidationError {
  error: string;
  details?: Record<string, string>;
}

/**
 * Generic API response wrapper
 */
export interface ApiResponse<T> {
  data?: T;
  error?: string;
  correlationId?: string;
}

/**
 * Paginated response wrapper
 */
export interface PaginatedResponse<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
  hasMore: boolean;
}
