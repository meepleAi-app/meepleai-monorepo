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
  type CoverCandidates,
  type CoverAssignment,
  type CoverContext,
  type AssignCoverRequest,
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
  };
}

export type AdminCoverClient = ReturnType<typeof createAdminCoverClient>;
