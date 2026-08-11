import { describe, it, expect, vi } from 'vitest';

import {
  CoverCandidatesSchema,
  CoverAssignmentSchema,
  ManualCoverResultSchema,
  type AssignCoverRequest,
  type SetManualCoverRequest,
} from '@/lib/api/schemas/admin/admin-cover.schemas';

import { createAdminCoverClient } from '../adminCoverClient';

const GAME_ID = '550e8400-e29b-41d4-a716-446655440000';

function makeHttp() {
  return {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    patch: vi.fn(),
    delete: vi.fn(),
  };
}

describe('adminCoverClient', () => {
  describe('getCoverCandidates', () => {
    it('GETs the cover-candidates read shape with the CoverCandidatesSchema', async () => {
      const http = makeHttp();
      const payload = {
        gameId: GAME_ID,
        candidates: [],
        assignments: { card: null, hero: null, social: null },
      };
      http.get.mockResolvedValue(payload);

      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      const client = createAdminCoverClient(http as any);
      const result = await client.getCoverCandidates(GAME_ID);

      expect(http.get).toHaveBeenCalledWith(
        `/api/v1/admin/shared-games/${GAME_ID}/cover-candidates`,
        CoverCandidatesSchema
      );
      expect(result).toEqual(payload);
    });
  });

  describe('assignCover', () => {
    it('PUTs the pin to the per-context path with body + CoverAssignmentSchema', async () => {
      const http = makeHttp();
      const body: AssignCoverRequest = { source: 'Pdf', focalX: 0.25, focalY: 0.75 };
      const assignment = { context: 'Hero', source: 'Pdf', focalX: 0.25, focalY: 0.75 };
      http.put.mockResolvedValue(assignment);

      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      const client = createAdminCoverClient(http as any);
      const result = await client.assignCover(GAME_ID, 'Hero', body);

      expect(http.put).toHaveBeenCalledWith(
        `/api/v1/admin/shared-games/${GAME_ID}/cover-assignments/Hero`,
        body,
        CoverAssignmentSchema
      );
      expect(result).toEqual(assignment);
    });
  });

  describe('removeCoverAssignment', () => {
    it('DELETEs the per-context assignment path', async () => {
      const http = makeHttp();
      http.delete.mockResolvedValue(undefined);

      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      const client = createAdminCoverClient(http as any);
      await client.removeCoverAssignment(GAME_ID, 'Card');

      expect(http.delete).toHaveBeenCalledWith(
        `/api/v1/admin/shared-games/${GAME_ID}/cover-assignments/Card`
      );
    });
  });

  describe('setManualCover', () => {
    it('POSTs the manual-cover body to the manual-cover path with the ManualCoverResultSchema', async () => {
      const http = makeHttp();
      const body: SetManualCoverRequest = {
        sourceUrl: 'https://commons.example.org/c.png',
        license: 'CC0',
        attribution: null,
      };
      const payload = { dbKey: 'covers/manual/x/cover', presignedUrl: 'https://r2.example/m.webp' };
      http.post.mockResolvedValue(payload);

      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      const client = createAdminCoverClient(http as any);
      const result = await client.setManualCover(GAME_ID, body);

      expect(http.post).toHaveBeenCalledWith(
        `/api/v1/admin/shared-games/${GAME_ID}/manual-cover`,
        body,
        ManualCoverResultSchema
      );
      expect(result).toEqual(payload);
    });
  });
});
