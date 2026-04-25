/**
 * Admin Mechanic Extractor — AI Comprehension Validation Client (ADR-051 Sprint 1 / Task 34)
 *
 * Typed client wrapping `/api/v1/admin/mechanic-extractor/*` endpoints.
 * Backend authoritative source:
 *   apps/api/src/Api/Routing/AdminMechanicExtractorValidationEndpoints.cs
 *
 * Pattern follows the sibling `adminAiClient.ts`:
 *  - factory function `createAdminMechanicExtractorValidationClient(http)`
 *  - route constants hoisted to a top-level `ADMIN_MECHANIC_EXTRACTOR_VALIDATION_ROUTES`
 *  - response shapes parsed via Zod schemas from
 *    `../../schemas/admin-mechanic-extractor-validation.schemas`
 */

import {
  CalculateMetricsResponseSchema,
  CertificationThresholdsDtoSchema,
  CreateGoldenClaimResponseSchema,
  GoldenForGameDtoSchema,
  ImportBggTagsResponseSchema,
  MechanicValidationTrendDtoSchema,
  RecalculateAllMetricsResponseSchema,
  ValidationDashboardDtoSchema,
  type CalculateMetricsResponse,
  type CertificationThresholdsDto,
  type CreateGoldenClaimRequest,
  type CreateGoldenClaimResponse,
  type GoldenForGameDto,
  type ImportBggTagsRequest,
  type ImportBggTagsResponse,
  type MechanicValidationTrendDto,
  type OverrideCertificationRequest,
  type RecalculateAllMetricsResponse,
  type UpdateCertificationThresholdsRequest,
  type UpdateGoldenClaimRequest,
  type ValidationDashboardDto,
} from '../../schemas/admin-mechanic-extractor-validation.schemas';

import type { HttpClient } from '../../core/httpClient';

// ========== Route Constants ==========

const BASE = '/api/v1/admin/mechanic-extractor';

export const ADMIN_MECHANIC_EXTRACTOR_VALIDATION_ROUTES = {
  goldenForGame: (sharedGameId: string) =>
    `${BASE}/golden/${encodeURIComponent(sharedGameId)}` as const,
  goldenVersionHash: (sharedGameId: string) =>
    `${BASE}/golden/${encodeURIComponent(sharedGameId)}/version-hash` as const,
  goldenCreate: `${BASE}/golden`,
  goldenById: (id: string) => `${BASE}/golden/${encodeURIComponent(id)}` as const,
  goldenBggTags: (sharedGameId: string) =>
    `${BASE}/golden/${encodeURIComponent(sharedGameId)}/bgg-tags` as const,
  analysisMetrics: (analysisId: string) =>
    `${BASE}/analyses/${encodeURIComponent(analysisId)}/metrics` as const,
  overrideCertification: (analysisId: string) =>
    `${BASE}/analyses/${encodeURIComponent(analysisId)}/override-certification` as const,
  recalculateAll: `${BASE}/metrics/recalculate-all`,
  dashboard: `${BASE}/dashboard`,
  trend: (sharedGameId: string) =>
    `${BASE}/dashboard/${encodeURIComponent(sharedGameId)}/trend` as const,
  thresholds: `${BASE}/thresholds`,
} as const;

// ========== Client Factory ==========

export function createAdminMechanicExtractorValidationClient(http: HttpClient) {
  return {
    // ─── Golden set: queries ──────────────────────────────────────────────

    /** `GET /golden/{sharedGameId}` — curated golden-set bundle. */
    async getGolden(sharedGameId: string): Promise<GoldenForGameDto> {
      const result = await http.get(
        ADMIN_MECHANIC_EXTRACTOR_VALIDATION_ROUTES.goldenForGame(sharedGameId),
        GoldenForGameDtoSchema
      );
      if (!result) {
        throw new Error(`Failed to load golden set for shared game ${sharedGameId}`);
      }
      return result;
    },

    /**
     * `GET /golden/{sharedGameId}/version-hash` — lightweight version-hash probe.
     * Endpoint returns `Results.Ok(versionHash)`; the wire shape is the bare string.
     */
    async getGoldenVersionHash(sharedGameId: string): Promise<string> {
      const result = await http.get<string>(
        ADMIN_MECHANIC_EXTRACTOR_VALIDATION_ROUTES.goldenVersionHash(sharedGameId)
      );
      if (result === null || result === undefined) {
        throw new Error(`Failed to load golden version hash for shared game ${sharedGameId}`);
      }
      return result;
    },

    // ─── Golden set: CRUD ─────────────────────────────────────────────────

    /** `POST /golden` — create a curator-authored golden claim. Returns the new id. */
    async createClaim(request: CreateGoldenClaimRequest): Promise<CreateGoldenClaimResponse> {
      return http.post(
        ADMIN_MECHANIC_EXTRACTOR_VALIDATION_ROUTES.goldenCreate,
        request,
        CreateGoldenClaimResponseSchema
      );
    },

    /** `PUT /golden/{id}` — update statement / expectedPage / sourceQuote. 204 on success. */
    async updateClaim(id: string, request: UpdateGoldenClaimRequest): Promise<void> {
      await http.put(ADMIN_MECHANIC_EXTRACTOR_VALIDATION_ROUTES.goldenById(id), request);
    },

    /** `DELETE /golden/{id}` — soft-delete (deactivate). 204 on success. */
    async deactivateClaim(id: string): Promise<void> {
      await http.delete(ADMIN_MECHANIC_EXTRACTOR_VALIDATION_ROUTES.goldenById(id));
    },

    /** `POST /golden/{sharedGameId}/bgg-tags` — bulk-upsert BGG tags. */
    async importBggTags(
      sharedGameId: string,
      tags: ImportBggTagsRequest['tags']
    ): Promise<ImportBggTagsResponse> {
      const body: ImportBggTagsRequest = { tags };
      return http.post(
        ADMIN_MECHANIC_EXTRACTOR_VALIDATION_ROUTES.goldenBggTags(sharedGameId),
        body,
        ImportBggTagsResponseSchema
      );
    },

    // ─── Metrics + certification override ─────────────────────────────────

    /** `POST /analyses/{id}/metrics` — compute + persist a metrics snapshot. */
    async calculateMetrics(analysisId: string): Promise<CalculateMetricsResponse> {
      return http.post(
        ADMIN_MECHANIC_EXTRACTOR_VALIDATION_ROUTES.analysisMetrics(analysisId),
        {},
        CalculateMetricsResponseSchema
      );
    },

    /** `POST /analyses/{id}/override-certification` — admin escalation, 20..500-char reason. */
    async overrideCertification(analysisId: string, reason: string): Promise<void> {
      const body: OverrideCertificationRequest = { reason };
      await http.post(
        ADMIN_MECHANIC_EXTRACTOR_VALIDATION_ROUTES.overrideCertification(analysisId),
        body
      );
    },

    /**
     * `POST /metrics/recalculate-all` — synchronous batch recompute over every
     * Published mechanic analysis. Included for completeness even though the
     * Sprint 1 UI may not surface it.
     */
    async recalculateAllMetrics(): Promise<RecalculateAllMetricsResponse> {
      return http.post(
        ADMIN_MECHANIC_EXTRACTOR_VALIDATION_ROUTES.recalculateAll,
        {},
        RecalculateAllMetricsResponseSchema
      );
    },

    // ─── Dashboard + trend ────────────────────────────────────────────────

    /** `GET /dashboard` — per-game certification dashboard rows. */
    async getDashboard(): Promise<ValidationDashboardDto> {
      const result = await http.get(
        ADMIN_MECHANIC_EXTRACTOR_VALIDATION_ROUTES.dashboard,
        ValidationDashboardDtoSchema
      );
      return result ?? [];
    },

    /**
     * `GET /dashboard/{sharedGameId}/trend?take=N` — historical metrics snapshots
     * (descending by `computedAt`). `take` defaults to 20 server-side; capped at 100.
     */
    async getTrend(sharedGameId: string, take?: number): Promise<MechanicValidationTrendDto> {
      const params = new URLSearchParams();
      if (take !== undefined) {
        params.set('take', take.toString());
      }
      const qs = params.toString();
      const url = qs
        ? `${ADMIN_MECHANIC_EXTRACTOR_VALIDATION_ROUTES.trend(sharedGameId)}?${qs}`
        : ADMIN_MECHANIC_EXTRACTOR_VALIDATION_ROUTES.trend(sharedGameId);

      const result = await http.get(url, MechanicValidationTrendDtoSchema);
      return result ?? [];
    },

    // ─── Certification thresholds (singleton) ─────────────────────────────

    /** `GET /thresholds` — current `CertificationThresholds` value object. */
    async getThresholds(): Promise<CertificationThresholdsDto> {
      const result = await http.get(
        ADMIN_MECHANIC_EXTRACTOR_VALIDATION_ROUTES.thresholds,
        CertificationThresholdsDtoSchema
      );
      if (!result) {
        throw new Error('Failed to load certification thresholds');
      }
      return result;
    },

    /** `PUT /thresholds` — replace the singleton. 204 on success. */
    async updateThresholds(thresholds: UpdateCertificationThresholdsRequest): Promise<void> {
      await http.put(ADMIN_MECHANIC_EXTRACTOR_VALIDATION_ROUTES.thresholds, thresholds);
    },
  };
}

export type AdminMechanicExtractorValidationClient = ReturnType<
  typeof createAdminMechanicExtractorValidationClient
>;
