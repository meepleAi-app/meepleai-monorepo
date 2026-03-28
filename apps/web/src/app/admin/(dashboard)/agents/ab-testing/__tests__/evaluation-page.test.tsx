import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { renderWithQuery } from '@/__tests__/utils/query-test-utils';

import { AbTestEvaluationPageInner } from '../[id]/AbTestEvaluationPageInner';

const mockGetAbTest = vi.hoisted(() => vi.fn());
const mockEvaluateAbTest = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getAbTest: mockGetAbTest,
      evaluateAbTest: mockEvaluateAbTest,
    },
  },
}));

vi.mock('@/hooks/useSetNavConfig', () => ({
  useSetNavConfig: () => vi.fn(),
}));

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
  useSearchParams: () => new URLSearchParams(),
  usePathname: () => '/admin/agents/ab-testing',
  redirect: vi.fn(),
}));

const SESSION_ID = '00000000-0000-0000-0000-000000000001';

const MOCK_SESSION = {
  id: SESSION_ID,
  createdBy: '00000000-0000-0000-0000-000000000099',
  query: 'What are the rules for Catan?',
  status: 'InProgress',
  variants: [
    {
      id: '00000000-0000-0000-0000-000000000010',
      label: 'A',
      response: 'Catan is a resource trading game...',
      tokensUsed: 100,
      latencyMs: 250,
      costUsd: 0.001,
      failed: false,
    },
    {
      id: '00000000-0000-0000-0000-000000000020',
      label: 'B',
      response: 'In Settlers of Catan, players...',
      tokensUsed: 80,
      latencyMs: 300,
      costUsd: 0.002,
      failed: false,
    },
  ],
  totalCost: 0.003,
  createdAt: '2026-03-09T12:00:00Z',
};

const MOCK_REVEALED = {
  id: SESSION_ID,
  createdBy: '00000000-0000-0000-0000-000000000099',
  query: 'What are the rules for Catan?',
  status: 'Evaluated',
  variants: [
    {
      id: '00000000-0000-0000-0000-000000000010',
      label: 'A',
      provider: 'OpenRouter',
      modelId: 'openai/gpt-4o',
      response: 'Catan is a resource trading game...',
      tokensUsed: 100,
      latencyMs: 250,
      costUsd: 0.001,
      failed: false,
      evaluation: {
        evaluatorId: '00000000-0000-0000-0000-000000000099',
        accuracy: 4,
        completeness: 5,
        clarity: 4,
        tone: 3,
        notes: 'Good response',
        averageScore: 4.0,
        evaluatedAt: '2026-03-09T12:05:00Z',
      },
    },
    {
      id: '00000000-0000-0000-0000-000000000020',
      label: 'B',
      provider: 'OpenRouter',
      modelId: 'anthropic/claude-3-haiku',
      response: 'In Settlers of Catan, players...',
      tokensUsed: 80,
      latencyMs: 300,
      costUsd: 0.002,
      failed: false,
      evaluation: {
        evaluatorId: '00000000-0000-0000-0000-000000000099',
        accuracy: 3,
        completeness: 4,
        clarity: 5,
        tone: 4,
        notes: null,
        averageScore: 4.0,
        evaluatedAt: '2026-03-09T12:05:00Z',
      },
    },
  ],
  totalCost: 0.003,
  createdAt: '2026-03-09T12:00:00Z',
  winnerLabel: 'A',
  winnerModelId: 'openai/gpt-4o',
};

describe('AbTestEvaluationPage', () => {
  const user = userEvent.setup();

  beforeEach(() => {
    vi.clearAllMocks();
    mockGetAbTest.mockResolvedValue(MOCK_SESSION);
  });

  it('renders blind evaluation with variant responses', async () => {
    renderWithQuery(<AbTestEvaluationPageInner id={SESSION_ID} />);

    await waitFor(() => {
      expect(screen.getByText('Variant A')).toBeInTheDocument();
    });

    expect(screen.getByText('A/B Test Evaluation')).toBeInTheDocument();
    expect(screen.getByText('Variant B')).toBeInTheDocument();
    expect(screen.getByText('Catan is a resource trading game...')).toBeInTheDocument();
    expect(screen.getByText('In Settlers of Catan, players...')).toBeInTheDocument();
  });

  it('shows star ratings for each dimension', async () => {
    renderWithQuery(<AbTestEvaluationPageInner id={SESSION_ID} />);

    await waitFor(() => {
      expect(screen.getByText('Variant A')).toBeInTheDocument();
    });

    // 4 dimensions × 2 variants = 8 labels
    expect(screen.getAllByText('Accuracy')).toHaveLength(2);
    expect(screen.getAllByText('Completeness')).toHaveLength(2);
    expect(screen.getAllByText('Clarity')).toHaveLength(2);
    expect(screen.getAllByText('Tone')).toHaveLength(2);
  });

  it('disables submit button when not all variants scored', async () => {
    renderWithQuery(<AbTestEvaluationPageInner id={SESSION_ID} />);

    await waitFor(() => {
      expect(screen.getByText('Variant A')).toBeInTheDocument();
    });

    const submitButton = screen.getByRole('button', { name: /submit evaluation/i });
    expect(submitButton).toBeDisabled();
  });

  it('enables submit after scoring all variants', async () => {
    renderWithQuery(<AbTestEvaluationPageInner id={SESSION_ID} />);

    await waitFor(() => {
      expect(screen.getByText('Variant A')).toBeInTheDocument();
    });

    // Click star ratings: 4 dimensions × 2 variants = 8 clicks needed
    // Stars are in groups of 5, aria-label "3 stars" etc.
    const allStarButtons = screen.getAllByRole('button', { name: /star/i });
    // Each dimension has 5 star buttons → 4 dims × 2 variants × 5 = 40 total
    // Click the 3rd star (index 2) in each group of 5
    for (let i = 0; i < 8; i++) {
      await user.click(allStarButtons[i * 5 + 2]); // 3 stars for each
    }

    const submitButton = screen.getByRole('button', { name: /submit evaluation/i });
    expect(submitButton).toBeEnabled();
  });

  it('shows revealed models after evaluation submit', async () => {
    mockEvaluateAbTest.mockResolvedValue(MOCK_REVEALED);

    renderWithQuery(<AbTestEvaluationPageInner id={SESSION_ID} />);

    await waitFor(() => {
      expect(screen.getByText('Variant A')).toBeInTheDocument();
    });

    // Score all variants
    const allStarButtons = screen.getAllByRole('button', { name: /star/i });
    for (let i = 0; i < 8; i++) {
      await user.click(allStarButtons[i * 5 + 2]);
    }

    const submitButton = screen.getByRole('button', { name: /submit evaluation/i });
    await user.click(submitButton);

    await waitFor(() => {
      expect(screen.getByText('openai/gpt-4o')).toBeInTheDocument();
      expect(screen.getByText('anthropic/claude-3-haiku')).toBeInTheDocument();
    });

    // Winner banner
    expect(screen.getByText(/Winner: Variant A/)).toBeInTheDocument();
  });

  it('shows query text from session', async () => {
    renderWithQuery(<AbTestEvaluationPageInner id={SESSION_ID} />);

    await waitFor(() => {
      expect(screen.getByText('What are the rules for Catan?')).toBeInTheDocument();
    });
  });

  it('handles failed variants', async () => {
    mockGetAbTest.mockResolvedValue({
      ...MOCK_SESSION,
      variants: [
        ...MOCK_SESSION.variants,
        {
          id: '00000000-0000-0000-0000-000000000030',
          label: 'C',
          response: null,
          tokensUsed: 0,
          latencyMs: 0,
          costUsd: 0,
          failed: true,
          errorMessage: 'Model timeout after 30s',
        },
      ],
    });

    renderWithQuery(<AbTestEvaluationPageInner id={SESSION_ID} />);

    await waitFor(() => {
      expect(screen.getByText(/Model timeout after 30s/)).toBeInTheDocument();
    });
  });

  it('shows not found message for missing session', async () => {
    mockGetAbTest.mockResolvedValue(null);

    renderWithQuery(<AbTestEvaluationPageInner id={SESSION_ID} />);

    await waitFor(() => {
      expect(screen.getByText('A/B test session not found.')).toBeInTheDocument();
    });
  });

  it('shows error alert when evaluation submit fails', async () => {
    mockEvaluateAbTest.mockRejectedValue(new Error('Budget exhausted'));

    renderWithQuery(<AbTestEvaluationPageInner id={SESSION_ID} />);

    await waitFor(() => {
      expect(screen.getByText('Variant A')).toBeInTheDocument();
    });

    const allStarButtons = screen.getAllByRole('button', { name: /star/i });
    for (let i = 0; i < 8; i++) {
      await user.click(allStarButtons[i * 5 + 2]);
    }

    const submitButton = screen.getByRole('button', { name: /submit evaluation/i });
    await user.click(submitButton);

    await waitFor(() => {
      expect(screen.getByText('Budget exhausted')).toBeInTheDocument();
    });
  });

  it('sends correct evaluation payload on submit', async () => {
    mockEvaluateAbTest.mockResolvedValue(MOCK_REVEALED);

    renderWithQuery(<AbTestEvaluationPageInner id={SESSION_ID} />);

    await waitFor(() => {
      expect(screen.getByText('Variant A')).toBeInTheDocument();
    });

    const allStarButtons = screen.getAllByRole('button', { name: /star/i });
    for (let i = 0; i < 8; i++) {
      await user.click(allStarButtons[i * 5 + 2]); // 3 stars for each
    }

    const submitButton = screen.getByRole('button', { name: /submit evaluation/i });
    await user.click(submitButton);

    await waitFor(() => {
      expect(mockEvaluateAbTest).toHaveBeenCalledWith(
        SESSION_ID,
        expect.objectContaining({
          evaluations: expect.arrayContaining([
            expect.objectContaining({
              label: 'A',
              accuracy: 3,
              completeness: 3,
              clarity: 3,
              tone: 3,
            }),
            expect.objectContaining({
              label: 'B',
              accuracy: 3,
              completeness: 3,
              clarity: 3,
              tone: 3,
            }),
          ]),
        })
      );
    });
  });

  it('shows results title for already-evaluated session', async () => {
    mockGetAbTest.mockResolvedValue({
      ...MOCK_SESSION,
      status: 'Evaluated',
    });

    renderWithQuery(<AbTestEvaluationPageInner id={SESSION_ID} />);

    await waitFor(() => {
      expect(screen.getByText('A/B Test Results')).toBeInTheDocument();
    });

    // Scoring UI should be hidden
    expect(screen.queryByRole('button', { name: /submit evaluation/i })).not.toBeInTheDocument();
  });
});
