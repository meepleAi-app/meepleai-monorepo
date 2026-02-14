/**
 * Integration Tests for EntityListView - View Mode Persistence
 *
 * Tests cross-mode integration and localStorage persistence across
 * Grid, List, and Carousel view modes.
 */

import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { renderWithQuery } from '@/__tests__/utils/query-test-utils';
import { EntityListView } from '../entity-list-view';
import type { MeepleEntityType } from '../../meeple-card';
import { vi } from 'vitest';
import { renderWithQuery } from '@/__tests__/utils/query-test-utils';

// Mock Next.js router (required by GameCarousel → useEntityActions → useRouter)
vi.mock('next/navigation', () => ({
  useRouter: vi.fn(() => ({ push: vi.fn(), replace: vi.fn(), back: vi.fn() })),
  usePathname: vi.fn(() => '/'),
  useSearchParams: vi.fn(() => new URLSearchParams()),
}));

vi.mock('next/navigation', () => ({
  useRouter: vi.fn(() => ({
    push: vi.fn(),
    back: vi.fn(),
    replace: vi.fn(),
    prefetch: vi.fn(),
    refresh: vi.fn(),
  })),
  usePathname: vi.fn(() => '/test'),
  useSearchParams: vi.fn(() => new URLSearchParams()),
}));

// Mock data
interface MockGame {
  id: string;
  title: string;
  publisher: string;
  rating: number;
}

const mockGames: MockGame[] = [
  { id: '1', title: 'Twilight Imperium', publisher: 'FFG', rating: 8.7 },
  { id: '2', title: 'Gloomhaven', publisher: 'Cephalofair', rating: 8.8 },
  { id: '3', title: 'Wingspan', publisher: 'Stonemaier', rating: 8.1 },
];

const defaultProps = {
  items: mockGames,
  entity: 'game' as MeepleEntityType,
  persistenceKey: 'integration-test',
  renderItem: (game: MockGame) => ({
    id: game.id,
    title: game.title,
    subtitle: game.publisher,
    rating: game.rating,
    ratingMax: 10,
  }),
};

describe('EntityListView - Mode Persistence Integration', () => {
  beforeEach(() => {
    localStorage.clear();
    vi.clearAllMocks();
  });

  describe('Grid Mode Persistence', () => {
    it('should persist grid mode across reload', () => {
      const { unmount } = renderWithQuery(<EntityListView {...defaultProps} />);

      expect(screen.getByTestId('grid-layout')).toBeInTheDocument();
      expect(localStorage.getItem('view-mode:integration-test')).toBe('"grid"');

      unmount();
      renderWithQuery(<EntityListView {...defaultProps} />);

      expect(screen.getByTestId('grid-layout')).toBeInTheDocument();
    });
  });

  describe('List Mode Persistence', () => {
    it('should persist list mode across reload', async () => {
      const user = userEvent.setup();
      const { unmount } = renderWithQuery(<EntityListView {...defaultProps} />);

      // Switch to list mode
      await user.click(screen.getByRole('radio', { name: /list view/i }));

      expect(screen.getByTestId('list-layout')).toBeInTheDocument();
      expect(localStorage.getItem('view-mode:integration-test')).toBe('"list"');

      unmount();
      renderWithQuery(<EntityListView {...defaultProps} />);

      // Should restore list mode
      expect(screen.getByTestId('list-layout')).toBeInTheDocument();
    });
  });

  describe('Carousel Mode Persistence', () => {
    it('should persist carousel mode across reload', async () => {
      const user = userEvent.setup();
      const { unmount } = renderWithQuery(<EntityListView {...defaultProps} />);

      // Switch to carousel mode
      await user.click(screen.getByRole('radio', { name: /carousel view/i }));

      expect(screen.getByTestId('carousel-layout')).toBeInTheDocument();
      expect(screen.getByTestId('game-carousel')).toBeInTheDocument();
      expect(localStorage.getItem('view-mode:integration-test')).toBe('"carousel"');

      unmount();
      renderWithQuery(<EntityListView {...defaultProps} />);

      // Should restore carousel mode
      expect(screen.getByTestId('carousel-layout')).toBeInTheDocument();
      expect(screen.getByTestId('game-carousel')).toBeInTheDocument();
    });
  });

  describe('Independent Persistence Keys', () => {
    it('should maintain independent state per persistenceKey', async () => {
      const user = userEvent.setup();

      // Render with persistenceKey "page-a"
      const { unmount: unmountA } = renderWithQuery(
        <EntityListView {...defaultProps} persistenceKey="page-a" />
      );

      // Switch page-a to list mode
      await user.click(screen.getByRole('radio', { name: /list view/i }));
      expect(localStorage.getItem('view-mode:page-a')).toBe('"list"');

      unmountA();

      // Render with persistenceKey "page-b" (should default to grid)
      renderWithQuery(<EntityListView {...defaultProps} persistenceKey="page-b" />);

      expect(screen.getByTestId('grid-layout')).toBeInTheDocument();
      expect(localStorage.getItem('view-mode:page-b')).toBe('"grid"');
      expect(localStorage.getItem('view-mode:page-a')).toBe('"list"'); // Unchanged
    });
  });

  describe('Mode Switching Flow', () => {
    it('should switch between all 3 modes seamlessly', async () => {
      const user = userEvent.setup();
      renderWithQuery(<EntityListView {...defaultProps} />);

      // Start: Grid
      expect(screen.getByTestId('grid-layout')).toBeInTheDocument();

      // Switch: Grid → List
      await user.click(screen.getByRole('radio', { name: /list view/i }));
      expect(screen.getByTestId('list-layout')).toBeInTheDocument();
      expect(screen.queryByTestId('grid-layout')).not.toBeInTheDocument();

      // Switch: List → Carousel
      await user.click(screen.getByRole('radio', { name: /carousel view/i }));
      expect(screen.getByTestId('carousel-layout')).toBeInTheDocument();
      expect(screen.getByTestId('game-carousel')).toBeInTheDocument();
      expect(screen.queryByTestId('list-layout')).not.toBeInTheDocument();

      // Switch: Carousel → Grid
      await user.click(screen.getByRole('radio', { name: /grid view/i }));
      expect(screen.getByTestId('grid-layout')).toBeInTheDocument();
      expect(screen.queryByTestId('carousel-layout')).not.toBeInTheDocument();
    });

    it('should maintain items across mode switches', async () => {
      const user = userEvent.setup();
      renderWithQuery(<EntityListView {...defaultProps} />);

      // Verify items in grid
      expect(screen.getByText('Twilight Imperium')).toBeInTheDocument();

      // Switch to list
      await user.click(screen.getByRole('radio', { name: /list view/i }));
      expect(screen.getByText('Twilight Imperium')).toBeInTheDocument();

      // Switch to carousel
      await user.click(screen.getByRole('radio', { name: /carousel view/i }));
      expect(screen.getByText('Twilight Imperium')).toBeInTheDocument();
    });
  });
});
