/**
 * Analysis Comparison API Tests
 *
 * Tests for the analysis comparison API client function:
 * - getAnalysisComparison calls correct endpoint
 * - Returns comparison data correctly
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';

const { mockGet } = vi.hoisted(() => ({
  mockGet: vi.fn(),
}));

vi.mock('@/lib/api/client', () => ({
  apiClient: {
    get: mockGet,
    post: vi.fn(),
    put: vi.fn(),
    delete: vi.fn(),
    patch: vi.fn(),
  },
}));

import { getAnalysisComparison } from '@/app/admin/(dashboard)/knowledge-base/queue/lib/analysis-comparison-api';

describe('Analysis Comparison API', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('getAnalysisComparison', () => {
    it('should GET comparison endpoint', async () => {
      const leftId = 'aaaa-1111';
      const rightId = 'bbbb-2222';
      mockGet.mockResolvedValue({
        leftId,
        rightId,
        leftVersion: 'v1',
        rightVersion: 'v2',
        leftAnalyzedAt: '2026-01-01T00:00:00Z',
        rightAnalyzedAt: '2026-01-02T00:00:00Z',
        confidenceScoreDelta: 0.05,
        mechanicsDiff: { added: [], removed: [], unchanged: [] },
        commonQuestionsDiff: { added: [], removed: [], unchanged: [] },
        keyConceptsDiff: { added: [], removed: [], unchanged: [] },
        faqDiff: { added: [], removed: [], modified: [], unchanged: [] },
        summaryChanged: false,
        leftSummary: null,
        rightSummary: null,
      });

      await getAnalysisComparison(leftId, rightId);

      expect(mockGet).toHaveBeenCalledWith(`/api/v1/admin/analysis/${leftId}/compare/${rightId}`);
    });

    it('should return comparison data', async () => {
      const comparisonData = {
        leftId: 'left-id',
        rightId: 'right-id',
        leftVersion: 'v1',
        rightVersion: 'v2',
        leftAnalyzedAt: '2026-01-01T00:00:00Z',
        rightAnalyzedAt: '2026-01-02T00:00:00Z',
        confidenceScoreDelta: 0.12,
        mechanicsDiff: {
          added: ['Worker Placement'],
          removed: ['Dice Rolling'],
          unchanged: ['Hand Management'],
        },
        commonQuestionsDiff: { added: [], removed: [], unchanged: [] },
        keyConceptsDiff: { added: [], removed: [], unchanged: [] },
        faqDiff: { added: [], removed: [], modified: [], unchanged: [] },
        summaryChanged: true,
        leftSummary: 'Old summary',
        rightSummary: 'New summary',
      };

      mockGet.mockResolvedValue(comparisonData);

      const result = await getAnalysisComparison('left-id', 'right-id');

      expect(result).toEqual(comparisonData);
    });
  });
});
