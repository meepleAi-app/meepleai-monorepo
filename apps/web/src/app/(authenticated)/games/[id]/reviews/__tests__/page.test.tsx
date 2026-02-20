/**
 * Game Reviews Coming Soon Page Tests (Issue #4889)
 */

import { screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { renderWithQuery } from '@/__tests__/utils/query-test-utils';

import GameReviewsPage from '../page';

const mockUseParams = vi.hoisted(() => vi.fn(() => ({ id: 'game-123' })));

vi.mock('next/navigation', () => ({
  useParams: mockUseParams,
  useRouter: vi.fn(() => ({ push: vi.fn() })),
}));

describe('GameReviewsPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('shows page heading', () => {
    renderWithQuery(<GameReviewsPage />);
    expect(screen.getByText('Reviews')).toBeInTheDocument();
  });

  it('shows Coming Soon message', () => {
    renderWithQuery(<GameReviewsPage />);
    expect(screen.getByText('Coming Soon')).toBeInTheDocument();
  });

  it('shows descriptive text', () => {
    renderWithQuery(<GameReviewsPage />);
    expect(screen.getByText(/Community reviews are on our roadmap/i)).toBeInTheDocument();
  });

  it('shows back navigation link', () => {
    renderWithQuery(<GameReviewsPage />);
    expect(screen.getByText('Back to Game')).toBeInTheDocument();
  });
});
