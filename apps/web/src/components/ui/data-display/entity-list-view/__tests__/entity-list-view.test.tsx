/**
 * Tests for EntityListView component (Phase 1: Grid mode only)
 */

import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
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
  persistenceKey: 'test-list',
  renderItem: (game: MockGame) => ({
    id: game.id,
    title: game.title,
    subtitle: game.publisher,
    rating: game.rating,
    ratingMax: 10,
  }),
};

describe('EntityListView (Phase 1: Grid Mode)', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  describe('Basic Rendering', () => {
    it('should render grid layout by default', () => {
      renderWithQuery(<EntityListView {...defaultProps} />);

      expect(screen.getByTestId('grid-layout')).toBeInTheDocument();
      expect(screen.getAllByTestId('meeple-card')).toHaveLength(3);
    });

    it('should render all items as MeepleCard components', () => {
      renderWithQuery(<EntityListView {...defaultProps} />);

      expect(screen.getByText('Twilight Imperium')).toBeInTheDocument();
      expect(screen.getByText('Gloomhaven')).toBeInTheDocument();
      expect(screen.getByText('Wingspan')).toBeInTheDocument();
    });

    it('should use grid variant for MeepleCard', () => {
      renderWithQuery(<EntityListView {...defaultProps} />);

      const cards = screen.getAllByTestId('meeple-card');
      cards.forEach((card) => {
        expect(card).toHaveAttribute('data-variant', 'grid');
      });
    });

    it('should apply correct entity type to cards', () => {
      renderWithQuery(<EntityListView {...defaultProps} entity="game" />);

      const cards = screen.getAllByTestId('meeple-card');
      cards.forEach((card) => {
        expect(card).toHaveAttribute('data-entity', 'game');
      });
    });
  });

  describe('Title & Subtitle', () => {
    it('should render title when provided', () => {
      renderWithQuery(<EntityListView {...defaultProps} title="Featured Games" />);

      expect(screen.getByRole('heading', { name: /featured games/i })).toBeInTheDocument();
    });

    it('should render subtitle when provided', () => {
      renderWithQuery(
        <EntityListView {...defaultProps} title="Games" subtitle="Explore the collection" />
      );

      expect(screen.getByText(/explore the collection/i)).toBeInTheDocument();
    });

    it('should not render header when no title, subtitle, or switcher', () => {
      const { container } = renderWithQuery(<EntityListView {...defaultProps} showViewSwitcher={false} />);

      // Check that the header element is not rendered (use container query to avoid global headings)
      const header = container.querySelector('header');
      expect(header).not.toBeInTheDocument();
    });
  });

  describe('ViewModeSwitcher', () => {
    it('should render ViewModeSwitcher by default', () => {
      renderWithQuery(<EntityListView {...defaultProps} />);

      expect(screen.getByRole('radiogroup', { name: /view mode/i })).toBeInTheDocument();
    });

    it('should hide ViewModeSwitcher when showViewSwitcher is false', () => {
      renderWithQuery(<EntityListView {...defaultProps} showViewSwitcher={false} />);

      expect(screen.queryByRole('radiogroup')).not.toBeInTheDocument();
    });

    it('should show all 3 modes by default (Phase 2)', () => {
      renderWithQuery(<EntityListView {...defaultProps} />);

      expect(screen.getByRole('radio', { name: /grid view/i })).toBeInTheDocument();
      expect(screen.getByRole('radio', { name: /list view/i })).toBeInTheDocument();
      expect(screen.getByRole('radio', { name: /carousel view/i })).toBeInTheDocument();
    });

    it('should respect availableModes prop', () => {
      renderWithQuery(<EntityListView {...defaultProps} availableModes={['grid', 'list']} />);

      expect(screen.getByRole('radio', { name: /grid view/i })).toBeInTheDocument();
      expect(screen.getByRole('radio', { name: /list view/i })).toBeInTheDocument();
      expect(screen.queryByRole('radio', { name: /carousel view/i })).not.toBeInTheDocument();
    });
  });

  describe('Empty State', () => {
    it('should render empty state when items array is empty', () => {
      renderWithQuery(<EntityListView {...defaultProps} items={[]} />);

      expect(screen.getByTestId('empty-state')).toBeInTheDocument();
      expect(screen.getByText(/no items to display/i)).toBeInTheDocument();
    });

    it('should render custom empty message', () => {
      renderWithQuery(
        <EntityListView {...defaultProps} items={[]} emptyMessage="No games found. Try adjusting your filters." />
      );

      expect(screen.getByText(/no games found/i)).toBeInTheDocument();
    });

    it('should not render grid layout when empty', () => {
      renderWithQuery(<EntityListView {...defaultProps} items={[]} />);

      expect(screen.queryByTestId('grid-layout')).not.toBeInTheDocument();
    });
  });

  describe('Loading State', () => {
    it('should render loading skeleton when loading is true', () => {
      renderWithQuery(<EntityListView {...defaultProps} loading />);

      expect(screen.getByTestId('loading-skeleton-grid')).toBeInTheDocument();
    });

    it('should not render items when loading', () => {
      renderWithQuery(<EntityListView {...defaultProps} loading />);

      expect(screen.queryByTestId('grid-layout')).not.toBeInTheDocument();
      expect(screen.queryByText('Twilight Imperium')).not.toBeInTheDocument();
    });

    it('should not render empty state when loading', () => {
      renderWithQuery(<EntityListView {...defaultProps} items={[]} loading />);

      expect(screen.queryByTestId('empty-state')).not.toBeInTheDocument();
    });
  });

  describe('Item Interaction', () => {
    it('should call onItemClick when card is clicked', async () => {
      const mockOnClick = vi.fn();
      const user = userEvent.setup();

      renderWithQuery(<EntityListView {...defaultProps} onItemClick={mockOnClick} />);

      const firstCard = screen.getAllByTestId('meeple-card')[0];
      await user.click(firstCard);

      expect(mockOnClick).toHaveBeenCalledWith(mockGames[0]);
      expect(mockOnClick).toHaveBeenCalledTimes(1);
    });

    it('should not throw when onItemClick is not provided', async () => {
      const user = userEvent.setup();

      renderWithQuery(<EntityListView {...defaultProps} />);

      const firstCard = screen.getAllByTestId('meeple-card')[0];

      expect(async () => {
        await user.click(firstCard);
      }).not.toThrow();
    });
  });

  describe('Grid Configuration', () => {
    it('should apply default grid columns', () => {
      const { container } = renderWithQuery(<EntityListView {...defaultProps} />);

      const gridLayout = container.querySelector('[data-testid="grid-layout"]');
      expect(gridLayout).toHaveClass('grid-cols-1');
      expect(gridLayout).toHaveClass('sm:grid-cols-2');
      expect(gridLayout).toHaveClass('lg:grid-cols-3');
      expect(gridLayout).toHaveClass('xl:grid-cols-4');
    });

    it('should apply custom grid columns', () => {
      const { container } = renderWithQuery(
        <EntityListView
          {...defaultProps}
          gridColumns={{ default: 2, sm: 3, lg: 4, xl: 5 }}
        />
      );

      const gridLayout = container.querySelector('[data-testid="grid-layout"]');
      expect(gridLayout).toHaveClass('grid-cols-2');
      expect(gridLayout).toHaveClass('sm:grid-cols-3');
      expect(gridLayout).toHaveClass('lg:grid-cols-4');
      expect(gridLayout).toHaveClass('xl:grid-cols-5');
    });

    it('should apply custom grid gap', () => {
      const { container } = renderWithQuery(<EntityListView {...defaultProps} gridGap={6} />);

      const gridLayout = container.querySelector('[data-testid="grid-layout"]');
      expect(gridLayout).toHaveClass('gap-6');
    });
  });

  describe('View Mode Persistence', () => {
    it('should persist grid mode to localStorage', () => {
      renderWithQuery(<EntityListView {...defaultProps} />);

      expect(screen.getByTestId('grid-layout')).toBeInTheDocument();
      expect(localStorage.getItem('view-mode:test-list')).toBe('"grid"');
    });

    it('should restore grid mode from localStorage', () => {
      localStorage.setItem('view-mode:test-list', '"grid"');

      renderWithQuery(<EntityListView {...defaultProps} />);

      expect(screen.getByTestId('grid-layout')).toBeInTheDocument();
    });

    it('should use different localStorage keys for different persistenceKey values', () => {
      const { unmount } = renderWithQuery(<EntityListView {...defaultProps} persistenceKey="page-a" />);

      expect(localStorage.getItem('view-mode:page-a')).toBe('"grid"');

      unmount();
      renderWithQuery(<EntityListView {...defaultProps} persistenceKey="page-b" />);

      expect(localStorage.getItem('view-mode:page-b')).toBe('"grid"');
      expect(localStorage.getItem('view-mode:page-a')).toBe('"grid"');
    });
  });

  describe('Controlled Mode', () => {
    it('should use controlled viewMode when provided', () => {
      renderWithQuery(<EntityListView {...defaultProps} viewMode="grid" />);

      expect(screen.getByTestId('grid-layout')).toBeInTheDocument();
    });

    it('should call onViewModeChange when mode changes', async () => {
      const mockOnChange = vi.fn();

      renderWithQuery(<EntityListView {...defaultProps} onViewModeChange={mockOnChange} />);

      // Should be called on mount with initial mode
      expect(mockOnChange).toHaveBeenCalledWith('grid');
    });
  });

  describe('Custom Styling', () => {
    it('should apply custom className to section', () => {
      renderWithQuery(<EntityListView {...defaultProps} className="custom-section-class" />);

      expect(screen.getByTestId('entity-list-view')).toHaveClass('custom-section-class');
    });

    it('should apply cardClassName to individual cards', () => {
      renderWithQuery(<EntityListView {...defaultProps} cardClassName="custom-card-class" />);

      const cards = screen.getAllByTestId('meeple-card');
      cards.forEach((card) => {
        expect(card).toHaveClass('custom-card-class');
      });
    });
  });

  describe('Accessibility', () => {
    it('should have aria-label on section', () => {
      renderWithQuery(<EntityListView {...defaultProps} title="My Games" />);

      expect(screen.getByRole('region', { name: /my games/i })).toBeInTheDocument();
    });

    it('should announce item count to screen readers', () => {
      renderWithQuery(<EntityListView {...defaultProps} />);

      expect(screen.getByText(/showing 3 items in grid view/i)).toBeInTheDocument();
    });

    it('should announce loading state to screen readers', () => {
      renderWithQuery(<EntityListView {...defaultProps} loading />);

      expect(screen.getByLabelText(/loading/i)).toBeInTheDocument();
    });
  });

  describe('List Mode (Phase 2)', () => {
    it('should render list layout when mode is list', async () => {
      const user = userEvent.setup();
      renderWithQuery(<EntityListView {...defaultProps} />);

      await user.click(screen.getByRole('radio', { name: /list view/i }));

      expect(screen.getByTestId('list-layout')).toBeInTheDocument();
      expect(screen.queryByTestId('grid-layout')).not.toBeInTheDocument();
    });

    it('should use MeepleCard variant="list" in list mode', async () => {
      const user = userEvent.setup();
      renderWithQuery(<EntityListView {...defaultProps} />);

      await user.click(screen.getByRole('radio', { name: /list view/i }));

      const cards = screen.getAllByTestId('meeple-card');
      cards.forEach((card) => {
        expect(card).toHaveAttribute('data-variant', 'list');
      });
    });

    it('should display all items in list mode', async () => {
      const user = userEvent.setup();
      renderWithQuery(<EntityListView {...defaultProps} />);

      await user.click(screen.getByRole('radio', { name: /list view/i }));

      expect(screen.getByText('Twilight Imperium')).toBeInTheDocument();
      expect(screen.getByText('Gloomhaven')).toBeInTheDocument();
      expect(screen.getByText('Wingspan')).toBeInTheDocument();
    });
  });

  describe('Carousel Mode (Phase 2)', () => {
    it('should render carousel layout when mode is carousel', async () => {
      const user = userEvent.setup();
      renderWithQuery(<EntityListView {...defaultProps} />);

      await user.click(screen.getByRole('radio', { name: /carousel view/i }));

      expect(screen.getByTestId('carousel-layout')).toBeInTheDocument();
      expect(screen.queryByTestId('grid-layout')).not.toBeInTheDocument();
    });

    it('should transform items to CarouselGame format', async () => {
      const user = userEvent.setup();
      renderWithQuery(<EntityListView {...defaultProps} />);

      await user.click(screen.getByRole('radio', { name: /carousel view/i }));

      // GameCarousel should receive games and display them
      expect(screen.getByText('Twilight Imperium')).toBeInTheDocument();
    });

    it('should pass carousel options correctly', async () => {
      const user = userEvent.setup();
      renderWithQuery(
        <EntityListView
          {...defaultProps}
          carouselOptions={{ autoPlay: true, showDots: false }}
        />
      );

      await user.click(screen.getByRole('radio', { name: /carousel view/i }));

      // Verify carousel with options (auto-play button should show "Pause")
      expect(screen.queryByLabelText(/pause auto-play/i)).toBeInTheDocument();
    });

    it('should handle onItemClick via carousel onGameSelect', async () => {
      const user = userEvent.setup();
      const mockOnClick = vi.fn();

      renderWithQuery(<EntityListView {...defaultProps} onItemClick={mockOnClick} />);

      await user.click(screen.getByRole('radio', { name: /carousel view/i }));

      // Click on carousel item (GameCarousel uses carousel-card-{index} testid)
      const firstCard = screen.getByTestId('carousel-card-0');
      await user.click(firstCard);

      expect(mockOnClick).toHaveBeenCalledWith(mockGames[0]);
    });
  });

  describe('TypeScript Generics', () => {
    it('should maintain type safety with custom item types', () => {
      interface CustomItem {
        customId: number;
        customTitle: string;
      }

      const customItems: CustomItem[] = [
        { customId: 1, customTitle: 'Item 1' },
        { customId: 2, customTitle: 'Item 2' },
      ];

      renderWithQuery(
        <EntityListView<CustomItem>
          items={customItems}
          entity="custom"
          persistenceKey="custom-list"
          renderItem={(item) => ({
            id: item.customId.toString(),
            title: item.customTitle,
          })}
        />
      );

      expect(screen.getByText('Item 1')).toBeInTheDocument();
      expect(screen.getByText('Item 2')).toBeInTheDocument();
    });
  });
});
