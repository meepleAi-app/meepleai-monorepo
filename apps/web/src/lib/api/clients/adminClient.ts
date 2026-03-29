/**
 * Administration Client (Issue #1679)
 *
 * Thin factory that composes 6 domain-specific sub-clients into a single
 * flat object for backward compatibility. All consumers continue to use
 * `api.admin.methodName()` without changes.
 */

import {
  createAdminUsersClient,
  createAdminContentClient,
  createAdminAiClient,
  createAdminAnalyticsClient,
  createAdminSystemClient,
  createAdminMonitorClient,
  createAdminConfigClient,
} from './admin';

import type { HttpClient } from '../core/httpClient';

// Re-export route constants for test imports
export { ADMIN_PDF_ROUTES, ADMIN_KB_ROUTES } from './admin/adminAiClient';

// Re-export all types from sub-clients for backward compatibility
export type {
  UserBadge,
  UserLibraryStats,
  RoleChangeHistory,
  ImpersonateUserResponse,
  EndImpersonationResponse,
  Invitation,
  InvitationStats,
} from './admin/adminUsersClient';

export type {
  SharedGameDocumentDetail,
  SharedGameDocumentsResult,
  EmailQueueStats,
  EmailQueueItem,
  EmailHistoryResult,
  DeadLetterResult,
  KBSettingsResponse,
} from './admin/adminContentClient';

export type {
  AgentTypology,
  AgentTypologyListResponse,
  EmbeddingServiceInfo,
  EmbeddingServiceMetrics,
  RagExecutionListItem,
  RagExecutionListResult,
  RagExecutionDetail,
  RagExecutionStatsResult,
  PipelineStageStatus,
  PipelineStage,
  PipelineRecentActivity,
  PipelineDistribution,
  PipelineHealthResponse,
  ProcessingStepAverages,
  ProcessingStepPercentiles,
  ProcessingMetricsResponse,
  KBClearCacheResponse,
  BulkUploadItemResult,
  BulkUploadPdfsResult,
  TierStrategyMatrixDto,
  StrategyModelMappingDto,
} from './admin/adminAiClient';

export type { DailyActiveUsers, ActiveAiUsersResult } from './admin/adminAnalyticsClient';

export type { ConfigurationDto } from './admin/adminConfigClient';

export interface CreateAdminClientParams {
  httpClient: HttpClient;
}

/**
 * Administration API client with Zod validation
 */
export function createAdminClient({ httpClient }: CreateAdminClientParams) {
  const http = httpClient;
  return {
    ...createAdminUsersClient(http),
    ...createAdminContentClient(http),
    ...createAdminAiClient(http),
    ...createAdminAnalyticsClient(http),
    ...createAdminSystemClient(http),
    ...createAdminMonitorClient(http),
    ...createAdminConfigClient(http),
  };
}

export type AdminClient = ReturnType<typeof createAdminClient>;
