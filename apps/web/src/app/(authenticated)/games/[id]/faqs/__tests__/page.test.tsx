/**
 * Game FAQs Page Tests (Issue #4889)
 */

import { screen, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { renderWithQuery } from '@/__tests__/utils/query-test-utils';

import GameFaqsPage from '../page';

const mockGetFAQs = vi.hoisted(() => vi.fn());

const mockUseParams = vi.hoisted(() => vi.fn(() => ({ id: 'game-123' })));

vi.mock('next/navigation', () => ({
  useParams: mockUseParams,
  useRouter: vi.fn(() => ({ push: vi.fn() })),
}));

vi.mock('@/lib/api', () => ({
  api: {
    games: {
      getFAQs: mockGetFAQs,
    },
  },
}));

const mockFAQsResult = {
  faqs: [
    {
      id: 'faq-1',
      gameId: 'game-123',
      question: 'Can I trade resources on my turn?',
      answer: 'Yes, you can trade with other players or the bank.',
      upvotes: 15,
      createdAt: '2026-01-01T10:00:00Z',
      updatedAt: null,
    },
    {
      id: 'faq-2',
      gameId: 'game-123',
      question: 'How many cards can I hold?',
      answer: 'You can hold a maximum of 7 cards.',
      upvotes: 8,
      createdAt: '2026-01-02T10:00:00Z',
      updatedAt: null,
    },
  ],
  totalCount: 2,
};

describe('GameFaqsPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders FAQ questions', async () => {
    mockGetFAQs.mockResolvedValue(mockFAQsResult);

    renderWithQuery(<GameFaqsPage />);

    await waitFor(() => {
      expect(screen.getByText('Can I trade resources on my turn?')).toBeInTheDocument();
    });

    expect(screen.getByText('How many cards can I hold?')).toBeInTheDocument();
  });

  it('shows total count in header', async () => {
    mockGetFAQs.mockResolvedValue(mockFAQsResult);

    renderWithQuery(<GameFaqsPage />);

    await waitFor(() => {
      expect(screen.getByText('2 Questions')).toBeInTheDocument();
    });
  });

  it('shows page heading', async () => {
    mockGetFAQs.mockResolvedValue(mockFAQsResult);

    renderWithQuery(<GameFaqsPage />);

    await waitFor(() => {
      expect(screen.getByText('FAQs')).toBeInTheDocument();
    });
  });

  it('shows loading skeletons', () => {
    mockGetFAQs.mockReturnValue(new Promise(() => {}));

    renderWithQuery(<GameFaqsPage />);

    const skeletons = document.querySelectorAll('.animate-pulse');
    expect(skeletons.length).toBeGreaterThan(0);
  });

  it('shows error state', async () => {
    mockGetFAQs.mockRejectedValue(new Error('Server error'));

    renderWithQuery(<GameFaqsPage />);

    await waitFor(() => {
      expect(screen.getByText(/Failed to load FAQs/i)).toBeInTheDocument();
    });
  });

  it('shows empty state when no FAQs', async () => {
    mockGetFAQs.mockResolvedValue({ faqs: [], totalCount: 0 });

    renderWithQuery(<GameFaqsPage />);

    await waitFor(() => {
      expect(screen.getByText(/No FAQs available/i)).toBeInTheDocument();
    });
  });
});
