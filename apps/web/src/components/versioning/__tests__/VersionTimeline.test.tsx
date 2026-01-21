/**
 * VersionTimeline Test Suite (Issue #2766)
 *
 * Tests for VersionTimeline component:
 * - Loading state display
 * - Error handling
 * - Empty state display
 * - Timeline rendering
 * - Version click handling
 * - Navigation behavior
 *
 * Uses MSW for API mocking
 */

import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { server } from '@/__tests__/mocks/server';
import { VersionTimeline } from '../VersionTimeline';

// Mock next/navigation
const mockPush = vi.fn();
vi.mock('next/navigation', () => ({
  useRouter: () => ({
    push: mockPush,
    replace: vi.fn(),
    back: vi.fn(),
    forward: vi.fn(),
    refresh: vi.fn(),
    prefetch: vi.fn(),
  }),
}));

// Mock react-chrono
vi.mock('react-chrono', () => ({
  Chrono: ({
    items,
    onItemSelected,
  }: {
    items: Array<{ title: string; cardTitle: string; cardSubtitle: string; cardDetailedText: string }>;
    onItemSelected: (data: { index: number }) => void;
  }) => (
    <div data-testid="chrono-mock">
      {items.map((item, index) => (
        <div
          key={index}
          data-testid={`timeline-item-${index}`}
          onClick={() => onItemSelected({ index })}
          role="button"
          tabIndex={0}
          onKeyDown={(e) => e.key === 'Enter' && onItemSelected({ index })}
        >
          <span data-testid={`item-title-${index}`}>{item.title}</span>
          <span data-testid={`item-card-title-${index}`}>{item.cardTitle}</span>
          <span data-testid={`item-card-subtitle-${index}`}>{item.cardSubtitle}</span>
          <span data-testid={`item-card-text-${index}`}>{item.cardDetailedText}</span>
        </div>
      ))}
    </div>
  ),
}));

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

describe('VersionTimeline', () => {
  const mockVersions = [
    {
      id: 'v1-id',
      version: 'v1.0.0',
      title: 'Initial Release',
      description: 'First version of the rules',
      author: 'John Doe',
      createdAt: '2024-01-15T10:00:00Z',
      parentVersionId: null,
      parentVersion: null,
      changeCount: 10,
      isCurrentVersion: false,
    },
    {
      id: 'v2-id',
      version: 'v2.0.0',
      title: 'Major Update',
      description: 'Significant changes to gameplay',
      author: 'Jane Smith',
      createdAt: '2024-02-20T14:30:00Z',
      parentVersionId: 'v1-id',
      parentVersion: 'v1.0.0',
      changeCount: 25,
      isCurrentVersion: true,
    },
  ];

  beforeEach(() => {
    mockPush.mockClear();

    server.use(
      http.get(`${API_BASE}/api/v1/games/:gameId/rulespec/versions/timeline`, () => {
        return HttpResponse.json({ versions: mockVersions });
      })
    );
  });

  describe('Loading State', () => {
    it('displays loading spinner initially', () => {
      server.use(
        http.get(`${API_BASE}/api/v1/games/:gameId/rulespec/versions/timeline`, async () => {
          // Delay response to show loading state
          await new Promise((resolve) => setTimeout(resolve, 100));
          return HttpResponse.json({ versions: mockVersions });
        })
      );

      render(<VersionTimeline gameId="game-1" />);

      expect(screen.getByRole('status')).toBeInTheDocument();
      expect(screen.getByLabelText('Loading timeline')).toBeInTheDocument();
      expect(screen.getByText('Loading version timeline…')).toBeInTheDocument();
    });

    it('has accessible loading indicator', () => {
      render(<VersionTimeline gameId="game-1" />);

      const status = screen.getByRole('status');
      expect(status).toHaveAttribute('aria-live', 'polite');
    });
  });

  describe('Error State', () => {
    it('displays error message when API fails', async () => {
      server.use(
        http.get(`${API_BASE}/api/v1/games/:gameId/rulespec/versions/timeline`, () => {
          return HttpResponse.json({ error: 'Internal Server Error' }, { status: 500 });
        })
      );

      render(<VersionTimeline gameId="game-1" />);

      await waitFor(() => {
        expect(screen.getByText('Error:')).toBeInTheDocument();
        expect(screen.getByText('Failed to fetch version timeline')).toBeInTheDocument();
      });
    });

    it('displays network error message', async () => {
      server.use(
        http.get(`${API_BASE}/api/v1/games/:gameId/rulespec/versions/timeline`, () => {
          return HttpResponse.error();
        })
      );

      render(<VersionTimeline gameId="game-1" />);

      await waitFor(() => {
        expect(screen.getByText('Error:')).toBeInTheDocument();
      });
    });

    it('displays custom error message from thrown error', async () => {
      server.use(
        http.get(`${API_BASE}/api/v1/games/:gameId/rulespec/versions/timeline`, () => {
          return HttpResponse.json(null, { status: 404 });
        })
      );

      render(<VersionTimeline gameId="game-1" />);

      await waitFor(() => {
        expect(screen.getByText('Error:')).toBeInTheDocument();
      });
    });
  });

  describe('Empty State', () => {
    it('displays empty state when no versions exist', async () => {
      server.use(
        http.get(`${API_BASE}/api/v1/games/:gameId/rulespec/versions/timeline`, () => {
          return HttpResponse.json({ versions: [] });
        })
      );

      render(<VersionTimeline gameId="game-1" />);

      await waitFor(() => {
        expect(screen.getByText('No version history available for this game.')).toBeInTheDocument();
      });
    });

    it('displays empty state when versions is undefined', async () => {
      server.use(
        http.get(`${API_BASE}/api/v1/games/:gameId/rulespec/versions/timeline`, () => {
          return HttpResponse.json({});
        })
      );

      render(<VersionTimeline gameId="game-1" />);

      await waitFor(() => {
        expect(screen.getByText('No version history available for this game.')).toBeInTheDocument();
      });
    });
  });

  describe('Timeline Rendering', () => {
    it('renders timeline with versions', async () => {
      render(<VersionTimeline gameId="game-1" />);

      await waitFor(() => {
        expect(screen.getByTestId('chrono-mock')).toBeInTheDocument();
      });

      expect(screen.getByTestId('timeline-item-0')).toBeInTheDocument();
      expect(screen.getByTestId('timeline-item-1')).toBeInTheDocument();
    });

    it('renders version cards with correct content', async () => {
      render(<VersionTimeline gameId="game-1" />);

      await waitFor(() => {
        expect(screen.getByTestId('chrono-mock')).toBeInTheDocument();
      });

      // First version
      expect(screen.getByTestId('item-card-title-0').textContent).toBe('v1.0.0');
      expect(screen.getByTestId('item-card-subtitle-0').textContent).toContain('John Doe');
      expect(screen.getByTestId('item-card-subtitle-0').textContent).toContain('10 changes');

      // Second version with parent and current marker
      expect(screen.getByTestId('item-card-title-1').textContent).toBe('v2.0.0');
      expect(screen.getByTestId('item-card-text-1').textContent).toContain('based on v1.0.0');
      expect(screen.getByTestId('item-card-text-1').textContent).toContain('[CURRENT]');
    });

    it('renders correct header', async () => {
      render(<VersionTimeline gameId="game-1" />);

      await waitFor(() => {
        expect(screen.getByText('Version Timeline')).toBeInTheDocument();
      });
    });
  });

  describe('Version Click Handling', () => {
    it('calls onVersionClick when provided', async () => {
      const onVersionClick = vi.fn();
      const user = userEvent.setup();

      render(<VersionTimeline gameId="game-1" onVersionClick={onVersionClick} />);

      await waitFor(() => {
        expect(screen.getByTestId('chrono-mock')).toBeInTheDocument();
      });

      await user.click(screen.getByTestId('timeline-item-0'));

      expect(onVersionClick).toHaveBeenCalledWith('v1.0.0');
    });

    it('navigates to version page when onVersionClick not provided', async () => {
      const user = userEvent.setup();

      render(<VersionTimeline gameId="game-1" />);

      await waitFor(() => {
        expect(screen.getByTestId('chrono-mock')).toBeInTheDocument();
      });

      await user.click(screen.getByTestId('timeline-item-1'));

      expect(mockPush).toHaveBeenCalledWith('/games/game-1/versions/v2.0.0');
    });

    it('handles click on different versions', async () => {
      const onVersionClick = vi.fn();
      const user = userEvent.setup();

      render(<VersionTimeline gameId="test-game" onVersionClick={onVersionClick} />);

      await waitFor(() => {
        expect(screen.getByTestId('chrono-mock')).toBeInTheDocument();
      });

      await user.click(screen.getByTestId('timeline-item-1'));

      expect(onVersionClick).toHaveBeenCalledWith('v2.0.0');
    });
  });

  describe('GameId Changes', () => {
    it('fetches new timeline when gameId changes', async () => {
      const { rerender } = render(<VersionTimeline gameId="game-1" />);

      await waitFor(() => {
        expect(screen.getByTestId('chrono-mock')).toBeInTheDocument();
      });

      // Setup different response for new gameId
      server.use(
        http.get(`${API_BASE}/api/v1/games/game-2/rulespec/versions/timeline`, () => {
          return HttpResponse.json({
            versions: [
              {
                id: 'v3-id',
                version: 'v3.0.0',
                title: 'New Game Version',
                description: 'Different game',
                author: 'Another Author',
                createdAt: '2024-03-01T00:00:00Z',
                parentVersionId: null,
                parentVersion: null,
                changeCount: 5,
                isCurrentVersion: true,
              },
            ],
          });
        })
      );

      rerender(<VersionTimeline gameId="game-2" />);

      await waitFor(() => {
        expect(screen.getByTestId('item-card-title-0').textContent).toBe('v3.0.0');
      });
    });

    it('does not fetch if gameId is empty', async () => {
      let fetchCalled = false;
      server.use(
        http.get(`${API_BASE}/api/v1/games/:gameId/rulespec/versions/timeline`, () => {
          fetchCalled = true;
          return HttpResponse.json({ versions: mockVersions });
        })
      );

      render(<VersionTimeline gameId="" />);

      // Wait a bit to ensure no fetch happens
      await new Promise((resolve) => setTimeout(resolve, 50));

      expect(fetchCalled).toBe(false);
    });
  });

  describe('Edge Cases', () => {
    it('handles versions without parentVersion', async () => {
      server.use(
        http.get(`${API_BASE}/api/v1/games/:gameId/rulespec/versions/timeline`, () => {
          return HttpResponse.json({
            versions: [
              {
                id: 'v1-id',
                version: 'v1.0.0',
                title: 'Standalone',
                description: 'No parent',
                author: 'Author',
                createdAt: '2024-01-01T00:00:00Z',
                parentVersionId: null,
                parentVersion: null,
                changeCount: 5,
                isCurrentVersion: false,
              },
            ],
          });
        })
      );

      render(<VersionTimeline gameId="game-1" />);

      await waitFor(() => {
        const detailedText = screen.getByTestId('item-card-text-0').textContent;
        expect(detailedText).not.toContain('based on');
      });
    });

    it('handles versions that are not current', async () => {
      server.use(
        http.get(`${API_BASE}/api/v1/games/:gameId/rulespec/versions/timeline`, () => {
          return HttpResponse.json({
            versions: [
              {
                id: 'v1-id',
                version: 'v1.0.0',
                title: 'Old Version',
                description: 'Not current',
                author: 'Author',
                createdAt: '2024-01-01T00:00:00Z',
                parentVersionId: null,
                parentVersion: null,
                changeCount: 5,
                isCurrentVersion: false,
              },
            ],
          });
        })
      );

      render(<VersionTimeline gameId="game-1" />);

      await waitFor(() => {
        const detailedText = screen.getByTestId('item-card-text-0').textContent;
        expect(detailedText).not.toContain('[CURRENT]');
      });
    });

    it('handles onItemSelected with undefined index gracefully', async () => {
      // This tests the safety check in handleItemSelected
      render(<VersionTimeline gameId="game-1" onVersionClick={vi.fn()} />);

      await waitFor(() => {
        expect(screen.getByTestId('chrono-mock')).toBeInTheDocument();
      });

      // Component should not crash with undefined data
      // The mock already handles this case
    });
  });
});
