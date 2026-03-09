/**
 * AnalysisComparisonView Component Tests
 *
 * Tests for the analysis comparison UI:
 * - Empty state when no IDs provided
 * - Loading state while fetching
 * - Mechanics diff rendering (added/removed)
 * - FAQ modifications (before/after)
 * - Summary changed section
 * - Confidence delta display
 */

import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
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

import { AnalysisComparisonView } from '@/app/admin/(dashboard)/knowledge-base/queue/components/analysis-comparison-view';

function createQueryClient() {
  return new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
}

function renderView(leftId: string | null, rightId: string | null) {
  const queryClient = createQueryClient();
  return render(
    <QueryClientProvider client={queryClient}>
      <AnalysisComparisonView leftId={leftId} rightId={rightId} />
    </QueryClientProvider>
  );
}

function makeComparisonData(overrides: Record<string, unknown> = {}) {
  return {
    leftId: 'left-id',
    rightId: 'right-id',
    leftVersion: 'v1',
    rightVersion: 'v2',
    leftAnalyzedAt: '2026-01-01T00:00:00Z',
    rightAnalyzedAt: '2026-01-02T00:00:00Z',
    confidenceScoreDelta: 0,
    mechanicsDiff: { added: [], removed: [], unchanged: [] },
    commonQuestionsDiff: { added: [], removed: [], unchanged: [] },
    keyConceptsDiff: { added: [], removed: [], unchanged: [] },
    faqDiff: { added: [], removed: [], modified: [], unchanged: [] },
    summaryChanged: false,
    leftSummary: null,
    rightSummary: null,
    ...overrides,
  };
}

describe('AnalysisComparisonView', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should show empty state when no IDs provided', () => {
    renderView(null, null);
    expect(screen.getByTestId('comparison-empty')).toBeInTheDocument();
  });

  it('should show loading state', () => {
    // Never resolve to keep loading state visible
    mockGet.mockReturnValue(new Promise(() => {}));
    renderView('left-id', 'right-id');
    expect(screen.getByTestId('comparison-loading')).toBeInTheDocument();
  });

  it('should render comparison with mechanics diff', async () => {
    mockGet.mockResolvedValue(
      makeComparisonData({
        mechanicsDiff: {
          added: ['Worker Placement', 'Area Control'],
          removed: ['Dice Rolling'],
          unchanged: ['Hand Management'],
        },
      })
    );

    renderView('left-id', 'right-id');

    const view = await screen.findByTestId('comparison-view');
    expect(view).toBeInTheDocument();

    // Added mechanics
    expect(screen.getByText('Worker Placement')).toBeInTheDocument();
    expect(screen.getByText('Area Control')).toBeInTheDocument();

    // Removed mechanic
    expect(screen.getByText('Dice Rolling')).toBeInTheDocument();

    // Unchanged mechanic
    expect(screen.getByText('Hand Management')).toBeInTheDocument();
  });

  it('should render FAQ modifications', async () => {
    mockGet.mockResolvedValue(
      makeComparisonData({
        faqDiff: {
          added: [],
          removed: [],
          modified: [
            {
              question: 'How many players?',
              leftAnswer: '2-4 players',
              rightAnswer: '2-6 players',
              leftConfidence: 0.8,
              rightConfidence: 0.9,
            },
          ],
          unchanged: [],
        },
      })
    );

    renderView('left-id', 'right-id');

    await screen.findByTestId('comparison-view');

    // Question text
    expect(screen.getByText('How many players?')).toBeInTheDocument();

    // Before / after answers
    expect(screen.getByText('2-4 players')).toBeInTheDocument();
    expect(screen.getByText('2-6 players')).toBeInTheDocument();
  });

  it('should show summary changed section', async () => {
    mockGet.mockResolvedValue(
      makeComparisonData({
        summaryChanged: true,
        leftSummary: 'Old game summary text',
        rightSummary: 'New improved summary text',
      })
    );

    renderView('left-id', 'right-id');

    const summarySection = await screen.findByTestId('summary-changed');
    expect(summarySection).toBeInTheDocument();

    expect(screen.getByText('Old game summary text')).toBeInTheDocument();
    expect(screen.getByText('New improved summary text')).toBeInTheDocument();
  });

  it('should display confidence delta', async () => {
    mockGet.mockResolvedValue(
      makeComparisonData({
        confidenceScoreDelta: 0.15,
      })
    );

    renderView('left-id', 'right-id');

    const delta = await screen.findByTestId('confidence-delta');
    expect(delta).toBeInTheDocument();
    // 0.15 * 100 = 15.0, displayed as "+15.0%"
    expect(delta.textContent).toContain('+15.0%');
  });
});
