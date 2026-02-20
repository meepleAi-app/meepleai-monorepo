/**
 * Game Strategies Coming Soon Page Tests (Issue #4889)
 */

import { screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { renderWithQuery } from '@/__tests__/utils/query-test-utils';

import GameStrategiesPage from '../page';

const mockUseParams = vi.hoisted(() => vi.fn(() => ({ id: 'game-123' })));

vi.mock('next/navigation', () => ({
  useParams: mockUseParams,
  useRouter: vi.fn(() => ({ push: vi.fn() })),
}));

describe('GameStrategiesPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('shows page heading', () => {
    renderWithQuery(<GameStrategiesPage />);
    expect(screen.getByText('Strategies')).toBeInTheDocument();
  });

  it('shows Coming Soon message', () => {
    renderWithQuery(<GameStrategiesPage />);
    expect(screen.getByText('Coming Soon')).toBeInTheDocument();
  });

  it('shows descriptive text', () => {
    renderWithQuery(<GameStrategiesPage />);
    expect(screen.getByText(/Game strategies are on our roadmap/i)).toBeInTheDocument();
  });

  it('shows back navigation link', () => {
    renderWithQuery(<GameStrategiesPage />);
    expect(screen.getByText('Back to Game')).toBeInTheDocument();
  });
});
