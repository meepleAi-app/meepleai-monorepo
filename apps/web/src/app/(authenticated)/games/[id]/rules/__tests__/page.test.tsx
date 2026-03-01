/**
 * Game Rules Page Tests (Issue #4889)
 */

import { screen, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { renderWithQuery } from '@/__tests__/utils/query-test-utils';

import GameRulesPage from '../page';

const mockGetRules = vi.hoisted(() => vi.fn());

const mockUseParams = vi.hoisted(() => vi.fn(() => ({ id: 'game-123' })));

vi.mock('next/navigation', () => ({
  useParams: mockUseParams,
  useRouter: vi.fn(() => ({ push: vi.fn() })),
}));

vi.mock('@/lib/api', () => ({
  api: {
    games: {
      getRules: mockGetRules,
    },
  },
}));

const mockRules = [
  {
    id: 'rule-1',
    gameId: 'game-123',
    version: '1.0',
    createdAt: '2026-01-01T10:00:00Z',
    createdByUserId: null,
    parentVersionId: null,
    atoms: [
      { id: 'atom-1', text: 'Players take turns clockwise.', section: 'Setup', page: 1 },
      { id: 'atom-2', text: 'Draw 5 cards at start.', section: 'Setup', page: 2 },
    ],
  },
  {
    id: 'rule-2',
    gameId: 'game-123',
    version: '1.1',
    createdAt: '2026-02-01T10:00:00Z',
    createdByUserId: null,
    parentVersionId: 'rule-1',
    atoms: [{ id: 'atom-3', text: 'Updated rule text.', section: null, page: null }],
  },
];

describe('GameRulesPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders rule versions', async () => {
    mockGetRules.mockResolvedValue(mockRules);

    renderWithQuery(<GameRulesPage />);

    await waitFor(() => {
      expect(screen.getByText('Version 1.0')).toBeInTheDocument();
    });

    expect(screen.getByText('Version 1.1')).toBeInTheDocument();
  });

  it('shows page heading', async () => {
    mockGetRules.mockResolvedValue(mockRules);

    renderWithQuery(<GameRulesPage />);

    await waitFor(() => {
      expect(screen.getByText('Rules')).toBeInTheDocument();
    });
  });

  it('shows atom count badge', async () => {
    mockGetRules.mockResolvedValue(mockRules);

    renderWithQuery(<GameRulesPage />);

    await waitFor(() => {
      expect(screen.getByText('2 rules')).toBeInTheDocument();
    });

    expect(screen.getByText('1 rule')).toBeInTheDocument();
  });

  it('shows loading skeletons', () => {
    mockGetRules.mockReturnValue(new Promise(() => {}));

    renderWithQuery(<GameRulesPage />);

    const skeletons = document.querySelectorAll('.animate-pulse');
    expect(skeletons.length).toBeGreaterThan(0);
  });

  it('shows error state', async () => {
    mockGetRules.mockRejectedValue(new Error('Rules not found'));

    renderWithQuery(<GameRulesPage />);

    await waitFor(() => {
      expect(screen.getByText(/Failed to load rules/i)).toBeInTheDocument();
    });
  });

  it('shows empty state when no rules', async () => {
    mockGetRules.mockResolvedValue([]);

    renderWithQuery(<GameRulesPage />);

    await waitFor(() => {
      expect(screen.getByText(/No rules have been published/i)).toBeInTheDocument();
    });
  });
});
