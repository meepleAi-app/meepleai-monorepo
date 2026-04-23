/**
 * Mechanic Analyses (M1.2 async pipeline) schemas.
 *
 * Backend endpoints (ISSUE-524 / ADR-051):
 *   POST /api/v1/admin/mechanic-analyses                   -> 202 Accepted
 *   GET  /api/v1/admin/mechanic-analyses/{id}/status       -> MechanicAnalysisStatusDto
 *   POST /api/v1/admin/mechanic-analyses/{id}/submit-review
 *   POST /api/v1/admin/mechanic-analyses/{id}/approve
 *   POST /api/v1/admin/mechanic-analyses/{id}/suppress
 *
 * Enum values mirror the C# domain enums (see
 * apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Enums).
 */
import { z } from 'zod';

// ========== Enums ==========

export const MechanicAnalysisStatus = {
  Draft: 0,
  InReview: 1,
  Published: 2,
  Rejected: 3,
} as const;
export type MechanicAnalysisStatusValue =
  (typeof MechanicAnalysisStatus)[keyof typeof MechanicAnalysisStatus];

export const MechanicAnalysisStatusSchema = z.nativeEnum(MechanicAnalysisStatus);

export const SuppressionRequestSource = {
  Email: 1,
  Legal: 2,
  Other: 3,
} as const;
export type SuppressionRequestSourceValue =
  (typeof SuppressionRequestSource)[keyof typeof SuppressionRequestSource];

export const SuppressionRequestSourceSchema = z.nativeEnum(SuppressionRequestSource);

/**
 * Section id values for `MechanicSectionRunSummaryDto.section`.
 * Kept as an enum-like map so the UI can label section runs.
 */
export const MechanicSection = {
  Summary: 0,
  Mechanics: 1,
  Victory: 2,
  Resources: 3,
  Phases: 4,
  Faq: 5,
} as const;
export type MechanicSectionValue = (typeof MechanicSection)[keyof typeof MechanicSection];

export const MECHANIC_SECTION_LABELS: Record<number, string> = {
  0: 'Summary',
  1: 'Mechanics',
  2: 'Victory',
  3: 'Resources',
  4: 'Phases',
  5: 'FAQ',
};

/** Section run status: 0=Succeeded, 1=Failed, 2=SkippedDueToCostCap. */
export const MechanicSectionRunStatus = {
  Succeeded: 0,
  Failed: 1,
  SkippedDueToCostCap: 2,
} as const;
export const MECHANIC_SECTION_RUN_STATUS_LABELS: Record<number, string> = {
  0: 'Succeeded',
  1: 'Failed',
  2: 'Skipped (cost cap)',
};

// ========== DTOs ==========

export const MechanicSectionRunSummaryDtoSchema = z.object({
  section: z.number().int(),
  runOrder: z.number().int(),
  provider: z.string(),
  modelUsed: z.string(),
  promptTokens: z.number().int(),
  completionTokens: z.number().int(),
  totalTokens: z.number().int(),
  estimatedCostUsd: z.number(),
  latencyMs: z.number().int(),
  status: z.number().int(),
  errorMessage: z.string().nullable(),
  startedAt: z.string(),
  completedAt: z.string(),
});
export type MechanicSectionRunSummaryDto = z.infer<typeof MechanicSectionRunSummaryDtoSchema>;

export const MechanicAnalysisStatusDtoSchema = z.object({
  id: z.string().uuid(),
  sharedGameId: z.string().uuid(),
  pdfDocumentId: z.string().uuid(),
  promptVersion: z.string(),
  status: MechanicAnalysisStatusSchema,
  rejectionReason: z.string().nullable(),
  createdBy: z.string().uuid(),
  createdAt: z.string(),
  reviewedBy: z.string().uuid().nullable(),
  reviewedAt: z.string().nullable(),
  provider: z.string(),
  modelUsed: z.string(),
  totalTokensUsed: z.number().int(),
  estimatedCostUsd: z.number(),
  costCapUsd: z.number(),
  costCapOverrideApplied: z.boolean(),
  costCapOverrideAt: z.string().nullable(),
  costCapOverrideBy: z.string().uuid().nullable(),
  costCapOverrideReason: z.string().nullable(),
  isSuppressed: z.boolean(),
  suppressedAt: z.string().nullable(),
  suppressedBy: z.string().uuid().nullable(),
  suppressionReason: z.string().nullable(),
  claimsCount: z.number().int(),
  sectionRuns: z.array(MechanicSectionRunSummaryDtoSchema),
});
export type MechanicAnalysisStatusDto = z.infer<typeof MechanicAnalysisStatusDtoSchema>;

export const MechanicAnalysisGenerationResponseDtoSchema = z.object({
  id: z.string().uuid(),
  status: MechanicAnalysisStatusSchema,
  promptVersion: z.string(),
  costCapUsd: z.number(),
  estimatedCostUsd: z.number(),
  projectedTotalTokens: z.number().int(),
  costCapOverrideApplied: z.boolean(),
  statusUrl: z.string(),
  isExistingAnalysis: z.boolean(),
});
export type MechanicAnalysisGenerationResponseDto = z.infer<
  typeof MechanicAnalysisGenerationResponseDtoSchema
>;

export const MechanicAnalysisLifecycleResponseDtoSchema = z.object({
  id: z.string().uuid(),
  status: MechanicAnalysisStatusSchema,
  reviewedBy: z.string().uuid().nullable(),
  reviewedAt: z.string().nullable(),
  isSuppressed: z.boolean(),
  suppressedAt: z.string().nullable(),
  suppressedBy: z.string().uuid().nullable(),
  suppressionReason: z.string().nullable(),
  suppressionRequestSource: SuppressionRequestSourceSchema.nullable(),
});
export type MechanicAnalysisLifecycleResponseDto = z.infer<
  typeof MechanicAnalysisLifecycleResponseDtoSchema
>;

// ========== Request types ==========

export interface CostCapOverrideRequest {
  newCapUsd: number;
  reason: string;
}

export interface GenerateMechanicAnalysisRequest {
  sharedGameId: string;
  pdfDocumentId: string;
  costCapUsd: number;
  costCapOverride?: CostCapOverrideRequest;
}

export interface SuppressMechanicAnalysisRequest {
  reason: string;
  requestSource: SuppressionRequestSourceValue;
  requestedAt?: string;
}

// ========== Labels ==========

export const MECHANIC_ANALYSIS_STATUS_LABELS: Record<number, string> = {
  0: 'Draft',
  1: 'In Review',
  2: 'Published',
  3: 'Rejected',
};

export const SUPPRESSION_REQUEST_SOURCE_LABELS: Record<number, string> = {
  1: 'Email',
  2: 'Legal',
  3: 'Other',
};

// ========== Routes ==========

export const MECHANIC_ANALYSES_ROUTES = {
  create: '/api/v1/admin/mechanic-analyses',
  status: (id: string) => `/api/v1/admin/mechanic-analyses/${id}/status`,
  submitReview: (id: string) => `/api/v1/admin/mechanic-analyses/${id}/submit-review`,
  approve: (id: string) => `/api/v1/admin/mechanic-analyses/${id}/approve`,
  suppress: (id: string) => `/api/v1/admin/mechanic-analyses/${id}/suppress`,
} as const;

export const MECHANIC_ANALYSES_PAGES = {
  analyses: '/admin/knowledge-base/mechanic-extractor/analyses',
} as const;
