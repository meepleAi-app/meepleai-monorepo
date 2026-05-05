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
  PartiallyExtracted: 4,
} as const;
export type MechanicAnalysisStatusValue =
  (typeof MechanicAnalysisStatus)[keyof typeof MechanicAnalysisStatus];

/**
 * Backend JSON serializer (`JsonStringEnumConverter`) emits enum members as
 * PascalCase strings (e.g. `"Draft"`), while the C# domain enum is numeric.
 * Accept both forms here so callers can keep using numeric comparisons against
 * `MechanicAnalysisStatus.Draft`.
 */
const MECHANIC_ANALYSIS_STATUS_NAME_TO_NUM: Record<string, MechanicAnalysisStatusValue> = {
  Draft: MechanicAnalysisStatus.Draft,
  InReview: MechanicAnalysisStatus.InReview,
  Published: MechanicAnalysisStatus.Published,
  Rejected: MechanicAnalysisStatus.Rejected,
  PartiallyExtracted: MechanicAnalysisStatus.PartiallyExtracted,
};

export const MechanicAnalysisStatusSchema = z.preprocess(v => {
  if (typeof v === 'string' && v in MECHANIC_ANALYSIS_STATUS_NAME_TO_NUM) {
    return MECHANIC_ANALYSIS_STATUS_NAME_TO_NUM[v];
  }
  return v;
}, z.nativeEnum(MechanicAnalysisStatus));

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

/**
 * Per-claim review status (ISSUE-584). Numeric mirror of the C# enum.
 * Backend serializer emits PascalCase strings, so the schema accepts both.
 */
export const MechanicClaimStatus = {
  Pending: 0,
  Approved: 1,
  Rejected: 2,
} as const;
export type MechanicClaimStatusValue =
  (typeof MechanicClaimStatus)[keyof typeof MechanicClaimStatus];

const MECHANIC_CLAIM_STATUS_NAME_TO_NUM: Record<string, MechanicClaimStatusValue> = {
  Pending: MechanicClaimStatus.Pending,
  Approved: MechanicClaimStatus.Approved,
  Rejected: MechanicClaimStatus.Rejected,
};

export const MechanicClaimStatusSchema = z.preprocess(v => {
  if (typeof v === 'string' && v in MECHANIC_CLAIM_STATUS_NAME_TO_NUM) {
    return MECHANIC_CLAIM_STATUS_NAME_TO_NUM[v];
  }
  return v;
}, z.nativeEnum(MechanicClaimStatus));

export const MECHANIC_CLAIM_STATUS_LABELS: Record<number, string> = {
  0: 'Pending',
  1: 'Approved',
  2: 'Rejected',
};

/**
 * `MechanicSection` may also arrive as a PascalCase string. Mirrors handling
 * elsewhere so claim payloads can use either form.
 */
const MECHANIC_SECTION_NAME_TO_NUM: Record<string, MechanicSectionValue> = {
  Summary: MechanicSection.Summary,
  Mechanics: MechanicSection.Mechanics,
  Victory: MechanicSection.Victory,
  Resources: MechanicSection.Resources,
  Phases: MechanicSection.Phases,
  Faq: MechanicSection.Faq,
  FAQ: MechanicSection.Faq,
  // Some older C# emit aliases used "Questions" — keep tolerant.
  Questions: MechanicSection.Faq,
};

export const MechanicSectionSchema = z.preprocess(v => {
  if (typeof v === 'string' && v in MECHANIC_SECTION_NAME_TO_NUM) {
    return MECHANIC_SECTION_NAME_TO_NUM[v];
  }
  return v;
}, z.nativeEnum(MechanicSection));

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

// ========== Claim DTOs (ISSUE-584) ==========

export const MechanicCitationDtoSchema = z.object({
  id: z.string().uuid(),
  pdfPage: z.number().int(),
  quote: z.string(),
  displayOrder: z.number().int(),
});
export type MechanicCitationDto = z.infer<typeof MechanicCitationDtoSchema>;

export const MechanicClaimDtoSchema = z.object({
  id: z.string().uuid(),
  analysisId: z.string().uuid(),
  section: MechanicSectionSchema,
  text: z.string(),
  displayOrder: z.number().int(),
  status: MechanicClaimStatusSchema,
  reviewedBy: z.string().uuid().nullable(),
  reviewedAt: z.string().nullable(),
  rejectionNote: z.string().nullable(),
  citations: z.array(MechanicCitationDtoSchema),
});
export type MechanicClaimDto = z.infer<typeof MechanicClaimDtoSchema>;

export const MechanicClaimsListSchema = z.array(MechanicClaimDtoSchema);

// ========== Discovery list (spec-panel gap #2) ==========

/**
 * Lightweight row returned by the index endpoint
 * `GET /api/v1/admin/mechanic-analyses?page=&pageSize=`. Mirrors the C# DTO
 * `MechanicAnalysisListItemDto`.
 */
export const MechanicAnalysisListItemDtoSchema = z.object({
  id: z.string().uuid(),
  sharedGameId: z.string().uuid(),
  gameTitle: z.string(),
  pdfDocumentId: z.string().uuid(),
  promptVersion: z.string(),
  status: MechanicAnalysisStatusSchema,
  claimsCount: z.number().int(),
  totalTokensUsed: z.number().int(),
  estimatedCostUsd: z.number(),
  certificationStatus: z.number().int(),
  isSuppressed: z.boolean(),
  createdAt: z.string(),
});
export type MechanicAnalysisListItemDto = z.infer<typeof MechanicAnalysisListItemDtoSchema>;

export const MechanicAnalysisListPageDtoSchema = z.object({
  items: z.array(MechanicAnalysisListItemDtoSchema),
  page: z.number().int(),
  pageSize: z.number().int(),
  totalCount: z.number().int(),
});
export type MechanicAnalysisListPageDto = z.infer<typeof MechanicAnalysisListPageDtoSchema>;

export const BulkApproveMechanicClaimsResponseDtoSchema = z.object({
  approvedCount: z.number().int(),
  skippedRejectedCount: z.number().int(),
  claims: MechanicClaimsListSchema,
});
export type BulkApproveMechanicClaimsResponseDto = z.infer<
  typeof BulkApproveMechanicClaimsResponseDtoSchema
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

/** Body for `POST .../claims/{claimId}/reject` (ISSUE-584). */
export interface RejectMechanicClaimRequest {
  note: string;
}

/** Local validation bounds for the rejection note (mirror backend validator). */
export const REJECT_CLAIM_NOTE_MIN_LENGTH = 1;
export const REJECT_CLAIM_NOTE_MAX_LENGTH = 500;

// ========== Labels ==========

export const MECHANIC_ANALYSIS_STATUS_LABELS: Record<number, string> = {
  0: 'Draft',
  1: 'In Review',
  2: 'Published',
  3: 'Rejected',
  4: 'Partially Extracted',
};

export const SUPPRESSION_REQUEST_SOURCE_LABELS: Record<number, string> = {
  1: 'Email',
  2: 'Legal',
  3: 'Other',
};

// ========== Routes ==========

export const MECHANIC_ANALYSES_ROUTES = {
  list: '/api/v1/admin/mechanic-analyses',
  create: '/api/v1/admin/mechanic-analyses',
  status: (id: string) => `/api/v1/admin/mechanic-analyses/${id}/status`,
  submitReview: (id: string) => `/api/v1/admin/mechanic-analyses/${id}/submit-review`,
  approve: (id: string) => `/api/v1/admin/mechanic-analyses/${id}/approve`,
  suppress: (id: string) => `/api/v1/admin/mechanic-analyses/${id}/suppress`,
  // ISSUE-584 — claims viewer endpoints
  claims: (id: string) => `/api/v1/admin/mechanic-analyses/${id}/claims`,
  approveClaim: (id: string, claimId: string) =>
    `/api/v1/admin/mechanic-analyses/${id}/claims/${claimId}/approve`,
  rejectClaim: (id: string, claimId: string) =>
    `/api/v1/admin/mechanic-analyses/${id}/claims/${claimId}/reject`,
  bulkApproveClaims: (id: string) => `/api/v1/admin/mechanic-analyses/${id}/claims/bulk-approve`,
} as const;

export const MECHANIC_ANALYSES_PAGES = {
  analyses: '/admin/knowledge-base/mechanic-extractor/analyses',
} as const;
