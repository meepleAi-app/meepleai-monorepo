/**
 * Admin Cover Picker Client (Epic #3470 — Slice 1d-c)
 *
 * Read/write client for the admin cover-source picker. Wraps the merged backend
 * endpoints under /admin/shared-games/{id}/cover-*. All three require the
 * AdminOrEditorPolicy server-side (the FE role gate is progressive enhancement only).
 */

import {
  CoverCandidatesSchema,
  CoverAssignmentSchema,
  ManualCoverResultSchema,
  CoverGapPageSchema,
  type CoverGapCause,
  type CoverGapPage,
  type CoverCandidates,
  type CoverAssignment,
  type CoverContext,
  type AssignCoverRequest,
  type SetManualCoverRequest,
  type ManualCoverResult,
} from '../../schemas/admin/admin-cover.schemas';

import type { HttpClient } from '../../core/httpClient';

export function createAdminCoverClient(http: HttpClient) {
  return {
    /**
     * GET the materialized cover source candidates (each with a presigned R2
     * preview URL) plus the current per-context assignments. Returns null on 401.
     */
    async getCoverCandidates(gameId: string): Promise<CoverCandidates | null> {
      return http.get(
        `/api/v1/admin/shared-games/${encodeURIComponent(gameId)}/cover-candidates`,
        CoverCandidatesSchema
      );
    },

    /**
     * PUT: pin a source (+ crop focal point) for a UI context. Returns the
     * persisted assignment so the picker can reflect the new state without a round-trip.
     */
    async assignCover(
      gameId: string,
      context: CoverContext,
      body: AssignCoverRequest
    ): Promise<CoverAssignment> {
      return http.put(
        `/api/v1/admin/shared-games/${encodeURIComponent(gameId)}/cover-assignments/${encodeURIComponent(context)}`,
        body,
        CoverAssignmentSchema
      );
    },

    /** DELETE: reset a context's cover to implicit precedence. Idempotent (204). */
    async removeCoverAssignment(gameId: string, context: CoverContext): Promise<void> {
      return http.delete(
        `/api/v1/admin/shared-games/${encodeURIComponent(gameId)}/cover-assignments/${encodeURIComponent(context)}`
      );
    },

    /**
     * POST: set a manual cover from an admin-supplied HTTPS image URL (epic #3470 Slice 3d).
     * The server SSRF-fetches + re-encodes + stores it, then materializes a `Manual` candidate
     * the picker surfaces after a candidates refetch. Returns the persisted DB key + a presigned
     * R2 preview URL for the freshly-written object. Server 400s on a non-HTTPS URL or a
     * non-whitelisted license; 404 on a missing game.
     */
    async setManualCover(gameId: string, body: SetManualCoverRequest): Promise<ManualCoverResult> {
      return http.post(
        `/api/v1/admin/shared-games/${encodeURIComponent(gameId)}/manual-cover`,
        body,
        ManualCoverResultSchema
      );
    },

    /**
     * GET the catalog games that have NO cover at all, each with the cause the cover-from-PDF
     * pipeline cannot cover it (#3590). Optional `cause` filters to one bounded value.
     * Returns null on 401.
     */
    async getCoverGap(
      params: {
        cause?: CoverGapCause;
        pageNumber?: number;
        pageSize?: number;
      } = {}
    ): Promise<CoverGapPage | null> {
      const search = new URLSearchParams();
      if (params.cause) search.set('cause', params.cause);
      if (params.pageNumber) search.set('pageNumber', String(params.pageNumber));
      if (params.pageSize) search.set('pageSize', String(params.pageSize));
      const qs = search.toString();

      return http.get(
        `/api/v1/admin/shared-games/cover-gap${qs ? `?${qs}` : ''}`,
        CoverGapPageSchema
      );
    },
  };
}

export type AdminCoverClient = ReturnType<typeof createAdminCoverClient>;
