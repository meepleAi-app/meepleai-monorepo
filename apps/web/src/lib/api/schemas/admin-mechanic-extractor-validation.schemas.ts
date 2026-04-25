/**
 * Admin Mechanic Extractor — AI Comprehension Validation Schemas (ADR-051 Sprint 1 / Task 34)
 *
 * Zod schemas + inferred TypeScript types for the admin AI Comprehension Validation
 * pipeline endpoints mounted under `/api/v1/admin/mechanic-extractor`.
 *
 * Mirrors backend DTO field names (camelCase, per Program.cs JSON config) and enum
 * casings (string converter — see `JsonStringEnumConverter` registration).
 *
 * Backend authoritative sources:
 *  - apps/api/src/Api/Routing/AdminMechanicExtractorValidationEndpoints.cs (request DTOs)
 *  - apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Queries/MechanicValidation/*.cs (query DTOs)
 *  - apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/ValueObjects/CertificationThresholds.cs
 *  - apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/ValueObjects/CertificationStatus.cs
 *  - apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Enums/MechanicSection.cs
 *  - apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Aggregates/MechanicAnalysisMetrics.cs
 */

import { z } from 'zod';

// ──────────────────────────────────────────────────────────────────────────
// Enums (string-serialized — see Program.cs `JsonStringEnumConverter`)
// ──────────────────────────────────────────────────────────────────────────

/**
 * Logical section of the rulebook a MechanicClaim belongs to.
 * Mirrors `MechanicSection` enum (Domain/Enums/MechanicSection.cs).
 */
export const MechanicSectionSchema = z.enum([
  'Summary',
  'Mechanics',
  'Victory',
  'Resources',
  'Phases',
  'Faq',
]);
export type MechanicSection = z.infer<typeof MechanicSectionSchema>;

/**
 * Certification verdict for a `MechanicAnalysisMetrics` snapshot.
 * Mirrors `CertificationStatus` enum (Domain/ValueObjects/CertificationStatus.cs).
 */
export const CertificationStatusSchema = z.enum(['NotEvaluated', 'Certified', 'NotCertified']);
export type CertificationStatus = z.infer<typeof CertificationStatusSchema>;

// ──────────────────────────────────────────────────────────────────────────
// Golden set: response DTOs
// ──────────────────────────────────────────────────────────────────────────

/**
 * Single golden claim projection. Mirrors `GoldenClaimDto`
 * (Application/Queries/MechanicValidation/GoldenForGameDto.cs).
 */
export const MechanicGoldenClaimDtoSchema = z.object({
  id: z.string().uuid(),
  section: MechanicSectionSchema,
  statement: z.string(),
  expectedPage: z.number().int(),
  sourceQuote: z.string(),
  keywords: z.array(z.string()),
  createdAt: z.string().datetime({ offset: true }),
});
export type MechanicGoldenClaimDto = z.infer<typeof MechanicGoldenClaimDtoSchema>;

/**
 * BGG mechanic tag projection. Mirrors `BggTagDto`
 * (Application/Queries/MechanicValidation/GoldenForGameDto.cs — query side).
 */
export const MechanicGoldenBggTagDtoSchema = z.object({
  name: z.string(),
  category: z.string(),
});
export type MechanicGoldenBggTagDto = z.infer<typeof MechanicGoldenBggTagDtoSchema>;

/**
 * Golden-set bundle for a single shared game. Mirrors `GoldenForGameDto`.
 */
export const GoldenForGameDtoSchema = z.object({
  sharedGameId: z.string().uuid(),
  versionHash: z.string(),
  claims: z.array(MechanicGoldenClaimDtoSchema),
  bggTags: z.array(MechanicGoldenBggTagDtoSchema),
});
export type GoldenForGameDto = z.infer<typeof GoldenForGameDtoSchema>;

// ──────────────────────────────────────────────────────────────────────────
// Golden set: request DTOs
// ──────────────────────────────────────────────────────────────────────────

/**
 * `POST /admin/mechanic-extractor/golden` — create a curator-authored golden claim.
 * Mirrors `CreateMechanicGoldenClaimRequest`
 * (Routing/AdminMechanicExtractorValidationEndpoints.cs).
 */
export const CreateGoldenClaimRequestSchema = z.object({
  sharedGameId: z.string().uuid(),
  section: MechanicSectionSchema,
  statement: z.string(),
  expectedPage: z.number().int(),
  sourceQuote: z.string(),
});
export type CreateGoldenClaimRequest = z.infer<typeof CreateGoldenClaimRequestSchema>;

/**
 * `PUT /admin/mechanic-extractor/golden/{id}` — update an existing golden claim.
 * Section is immutable; mirrors `UpdateMechanicGoldenClaimRequest`.
 */
export const UpdateGoldenClaimRequestSchema = z.object({
  statement: z.string(),
  expectedPage: z.number().int(),
  sourceQuote: z.string(),
});
export type UpdateGoldenClaimRequest = z.infer<typeof UpdateGoldenClaimRequestSchema>;

/**
 * Single BGG tag input on the bulk-import body.
 * Mirrors `BggTagInput` (Routing/AdminMechanicExtractorValidationEndpoints.cs).
 */
export const BggTagInputSchema = z.object({
  name: z.string(),
  category: z.string(),
});
export type BggTagInput = z.infer<typeof BggTagInputSchema>;

/**
 * `POST /admin/mechanic-extractor/golden/{sharedGameId}/bgg-tags` — bulk-import BGG tags.
 * Mirrors `ImportBggTagsRequest`.
 */
export const ImportBggTagsRequestSchema = z.object({
  tags: z.array(BggTagInputSchema),
});
export type ImportBggTagsRequest = z.infer<typeof ImportBggTagsRequestSchema>;

// ──────────────────────────────────────────────────────────────────────────
// Golden set: response wrappers (Created / Ok shells from endpoints)
// ──────────────────────────────────────────────────────────────────────────

/** Response for `POST /golden` — `Results.Created(... new { Id = newId })`. */
export const CreateGoldenClaimResponseSchema = z.object({
  id: z.string().uuid(),
});
export type CreateGoldenClaimResponse = z.infer<typeof CreateGoldenClaimResponseSchema>;

/**
 * Response for `POST /golden/{sharedGameId}/bgg-tags` — `{ Inserted, Skipped }`
 * (Sprint 2 / Task 17). `Inserted + Skipped == request.Tags.length` for non-empty
 * batches; empty submissions return `{ Inserted: 0, Skipped: 0 }`. The importer UI
 * surfaces both counts so duplicates are not silently lost.
 */
export const ImportBggTagsResponseSchema = z.object({
  inserted: z.number().int(),
  skipped: z.number().int(),
});
export type ImportBggTagsResponse = z.infer<typeof ImportBggTagsResponseSchema>;

// ──────────────────────────────────────────────────────────────────────────
// Metrics + certification override
// ──────────────────────────────────────────────────────────────────────────

/** Response for `POST /analyses/{id}/metrics` — `{ MetricsId: Guid }`. */
export const CalculateMetricsResponseSchema = z.object({
  metricsId: z.string().uuid(),
});
export type CalculateMetricsResponse = z.infer<typeof CalculateMetricsResponseSchema>;

/** Request body for `POST /analyses/{id}/override-certification`. */
export const OverrideCertificationRequestSchema = z.object({
  reason: z.string(),
});
export type OverrideCertificationRequest = z.infer<typeof OverrideCertificationRequestSchema>;

/** Response for `POST /metrics/recalculate-all` — `{ Processed: int }`. */
export const RecalculateAllMetricsResponseSchema = z.object({
  processed: z.number().int(),
});
export type RecalculateAllMetricsResponse = z.infer<typeof RecalculateAllMetricsResponseSchema>;

// ──────────────────────────────────────────────────────────────────────────
// Dashboard + trend
// ──────────────────────────────────────────────────────────────────────────

/**
 * Per-game dashboard row. Mirrors `DashboardGameRow`
 * (Domain/Repositories/IMechanicAnalysisMetricsRepository.cs).
 */
export const ValidationDashboardRowDtoSchema = z.object({
  sharedGameId: z.string().uuid(),
  name: z.string(),
  status: CertificationStatusSchema,
  overallScore: z.number(),
  lastComputedAt: z.string().datetime({ offset: true }).nullable(),
});
export type ValidationDashboardRowDto = z.infer<typeof ValidationDashboardRowDtoSchema>;

/** Whole dashboard payload — endpoint returns `IReadOnlyList<DashboardGameRow>`. */
export const ValidationDashboardDtoSchema = z.array(ValidationDashboardRowDtoSchema);
export type ValidationDashboardDto = z.infer<typeof ValidationDashboardDtoSchema>;

/**
 * Single historical metrics snapshot. Mirrors `MechanicAnalysisMetrics`
 * (Domain/Aggregates/MechanicAnalysisMetrics.cs) — public getters serialize as
 * camelCase. `thresholdsSnapshotJson` and `matchDetailsJson` are JSON strings
 * (not parsed structurally on the wire).
 */
export const MechanicAnalysisMetricsDtoSchema = z.object({
  id: z.string().uuid(),
  mechanicAnalysisId: z.string().uuid(),
  sharedGameId: z.string().uuid(),
  coveragePct: z.number(),
  pageAccuracyPct: z.number(),
  bggMatchPct: z.number(),
  overallScore: z.number(),
  certificationStatus: CertificationStatusSchema,
  goldenVersionHash: z.string(),
  thresholdsSnapshotJson: z.string(),
  matchDetailsJson: z.string(),
  computedAt: z.string().datetime({ offset: true }),
});
export type MechanicAnalysisMetricsDto = z.infer<typeof MechanicAnalysisMetricsDtoSchema>;

/** Trend payload — endpoint returns `IReadOnlyList<MechanicAnalysisMetrics>` desc by `computedAt`. */
export const MechanicValidationTrendDtoSchema = z.array(MechanicAnalysisMetricsDtoSchema);
export type MechanicValidationTrendDto = z.infer<typeof MechanicValidationTrendDtoSchema>;

// ──────────────────────────────────────────────────────────────────────────
// Certification thresholds (singleton VO)
// ──────────────────────────────────────────────────────────────────────────

/**
 * Operator-configurable certification thresholds. Mirrors `CertificationThresholds`
 * (Domain/ValueObjects/CertificationThresholds.cs).
 */
export const CertificationThresholdsDtoSchema = z.object({
  minCoveragePct: z.number(),
  maxPageTolerance: z.number().int(),
  minBggMatchPct: z.number(),
  minOverallScore: z.number(),
});
export type CertificationThresholdsDto = z.infer<typeof CertificationThresholdsDtoSchema>;

/** Request body for `PUT /admin/mechanic-extractor/thresholds`. */
export const UpdateCertificationThresholdsRequestSchema = z.object({
  minCoveragePct: z.number(),
  maxPageTolerance: z.number().int(),
  minBggMatchPct: z.number(),
  minOverallScore: z.number(),
});
export type UpdateCertificationThresholdsRequest = z.infer<
  typeof UpdateCertificationThresholdsRequestSchema
>;
