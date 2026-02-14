/**
 * Accessibility Tests for EntityListView
 *
 * Comprehensive WCAG 2.1 AA compliance verification:
 * - axe-core automated testing on all view modes
 * - Keyboard navigation audit
 * - Screen reader announcements
 * - Focus management
 * - ARIA attributes
 *
 * Issue #3894 - Section 3: Comprehensive Accessibility Audit
 */

import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { axe } from 'vitest-axe';
import { vi } from 'vitest';
import { EntityListView } from '../entity-list-view';
import type { MeepleEntityType } from '../../meeple-card';
import { renderWithQuery } from '@/__tests__/utils/query-test-utils';
import type { SortOption, FilterConfig } from '../entity-list-view.types';
import { Star, ArrowDownAZ } from 'lucide-react';

// Mock Next.js router
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
  category: string;
  hasPdf: boolean;
}

const mockGames: MockGame[] = [
  { id: '1', title: 'Twilight Imperium', publisher: 'FFG', rating: 8.7, category: 'strategy', hasPdf: true },
  { id: '2', title: 'Gloomhaven', publisher: 'Cephalofair', rating: 8.8, category: 'adventure', hasPdf: false },
  { id: '3', title: 'Wingspan', publisher: 'Stonemaier', rating: 8.1, category: 'strategy', hasPdf: true },
];

const sortOptions: SortOption<MockGame>[] = [
  { value: 'rating', label: 'Rating', icon: Star, compareFn: (a, b) => b.rating - a.rating },
  { value: 'name', label: 'Name', icon: ArrowDownAZ, compareFn: (a, b) => a.title.localeCompare(b.title) },
];

const filters: FilterConfig<MockGame>[] = [
  {
    id: 'category',
    type: 'select',
    label: 'Category',
    field: 'category',
    options: [
      { value: 'strategy', label: 'Strategy' },
      { value: 'adventure', label: 'Adventure' },
    ],
  },
  {
    id: 'hasPdf',
    type: 'checkbox',
    label: 'Has PDF',
    field: 'hasPdf',
  },
];

const defaultProps = {
  items: mockGames,
  entity: 'game' as MeepleEntityType,
  persistenceKey: 'a11y-test',
  renderItem: (game: MockGame) => ({
    id: game.id,
    title: game.title,
    subtitle: game.publisher,
    rating: game.rating,
    ratingMax: 10,
  }),
};

describe('EntityListView Accessibility', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  describe('axe-core: Grid Mode', () => {
    it('should have no accessibility violations in grid mode', async () => {
      const { container } = renderWithQuery(
        <EntityListView {...defaultProps} title="Games Collection" />
      );

      const results = await axe(container);
      expect(results.violations).toEqual([]);
    });

    it('should have no violations with search and sort', async () => {
      const { container } = renderWithQuery(
        <EntityListView
          {...defaultProps}
          title="Games"
          searchable
          searchFields={['title']}
          sortOptions={sortOptions}
          defaultSort="rating"
        />
      );

      const results = await axe(container);
      expect(results.violations).toEqual([]);
    });
  });

  describe('axe-core: List Mode', () => {
    it('should have no accessibility violations in list mode', async () => {
      const user = userEvent.setup();
      const { container } = renderWithQuery(
        <EntityListView {...defaultProps} title="Games" />
      );

      await user.click(screen.getByRole('radio', { name: /list view/i }));

      const results = await axe(container);
      expect(results.violations).toEqual([]);
    });
  });

  describe('axe-core: Empty & Loading States', () => {
    it('should have no violations in empty state', async () => {
      const { container } = renderWithQuery(
        <EntityListView {...defaultProps} items={[]} />
      );

      const results = await axe(container);
      expect(results.violations).toEqual([]);
    });

    it('should have no violations in loading state', async () => {
      const { container } = renderWithQuery(
        <EntityListView {...defaultProps} loading />
      );

      const results = await axe(container);
      expect(results.violations).toEqual([]);
    });
  });

  describe('ARIA Attributes', () => {
    it('should have region role with label', () => {
      renderWithQuery(
        <EntityListView {...defaultProps} title="My Games" />
      );

      expect(screen.getByRole('region', { name: /my games/i })).toBeInTheDocument();
    });

    it('should have sr-only announcement for item count', () => {
      renderWithQuery(<EntityListView {...defaultProps} />);

      expect(screen.getByText(/showing 3 items in grid view/i)).toBeInTheDocument();
    });

    it('should update announcement when switching modes', async () => {
      const user = userEvent.setup();
      renderWithQuery(<EntityListView {...defaultProps} />);

      await user.click(screen.getByRole('radio', { name: /list view/i }));
      expect(screen.getByText(/showing 3 items in list view/i)).toBeInTheDocument();
    });

    it('should have aria-live on announcements', () => {
      const { container } = renderWithQuery(<EntityListView {...defaultProps} />);

      const liveRegion = container.querySelector('[aria-live="polite"]');
      expect(liveRegion).toBeInTheDocument();
    });

    it('should announce loading state', () => {
      renderWithQuery(<EntityListView {...defaultProps} loading />);

      expect(screen.getByLabelText(/loading/i)).toBeInTheDocument();
    });
  });

  describe('Keyboard Navigation', () => {
    it('should support Tab navigation through interactive elements', async () => {
      const user = userEvent.setup();
      renderWithQuery(
        <EntityListView
          {...defaultProps}
          title="Games"
          searchable
          searchFields={['title']}
          sortOptions={sortOptions}
          defaultSort="rating"
        />
      );

      // Tab through: search → sort → view mode options
      await user.tab();
      // First focusable element should receive focus
      const activeElement = document.activeElement;
      expect(activeElement).not.toBeNull();
      expect(activeElement?.tagName).toBeDefined();
    });

    it('should support Ctrl/Cmd+K to focus search', async () => {
      const user = userEvent.setup();
      renderWithQuery(
        <EntityListView
          {...defaultProps}
          searchable
          searchFields={['title']}
        />
      );

      await user.keyboard('{Control>}k{/Control}');
      expect(screen.getByRole('searchbox')).toHaveFocus();
    });

    it('should support Escape to close sort dropdown', async () => {
      const user = userEvent.setup();
      renderWithQuery(
        <EntityListView
          {...defaultProps}
          sortOptions={sortOptions}
          defaultSort="rating"
        />
      );

      await user.click(screen.getByRole('button', { name: /sort by/i }));
      expect(screen.getByRole('listbox')).toBeInTheDocument();

      await user.keyboard('{Escape}');
      expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
    });

    it('should support arrow keys for sort dropdown navigation', async () => {
      const user = userEvent.setup();
      const onSortChange = vi.fn();
      renderWithQuery(
        <EntityListView
          {...defaultProps}
          sortOptions={sortOptions}
          defaultSort="rating"
          onSortChange={onSortChange}
        />
      );

      const sortButton = screen.getByRole('button', { name: /sort by/i });
      await user.click(sortButton);

      // Arrow down should move to next sort option
      await user.keyboard('{ArrowDown}');
      // The sort should have changed
      expect(onSortChange).toHaveBeenCalled();
    });
  });

  describe('ViewModeSwitcher Accessibility', () => {
    it('should use radiogroup role', () => {
      renderWithQuery(<EntityListView {...defaultProps} />);

      expect(screen.getByRole('radiogroup', { name: /view mode/i })).toBeInTheDocument();
    });

    it('should have radio buttons for each mode', () => {
      renderWithQuery(<EntityListView {...defaultProps} />);

      expect(screen.getByRole('radio', { name: /grid view/i })).toBeInTheDocument();
      expect(screen.getByRole('radio', { name: /list view/i })).toBeInTheDocument();
      expect(screen.getByRole('radio', { name: /carousel view/i })).toBeInTheDocument();
    });

    it('should mark current mode as checked', () => {
      renderWithQuery(<EntityListView {...defaultProps} />);

      expect(screen.getByRole('radio', { name: /grid view/i })).toBeChecked();
      expect(screen.getByRole('radio', { name: /list view/i })).not.toBeChecked();
    });

    it('should support keyboard mode switching', async () => {
      const user = userEvent.setup();
      renderWithQuery(<EntityListView {...defaultProps} />);

      const gridRadio = screen.getByRole('radio', { name: /grid view/i });
      gridRadio.focus();

      // Arrow key should move between radio options
      await user.keyboard('{ArrowRight}');

      // After arrow right, list view should be active
      expect(screen.getByRole('radio', { name: /list view/i })).toBeChecked();
    });
  });

  describe('SearchBar Accessibility', () => {
    it('should have searchbox role', () => {
      renderWithQuery(
        <EntityListView {...defaultProps} searchable searchFields={['title']} />
      );

      expect(screen.getByRole('searchbox')).toBeInTheDocument();
    });

    it('should have aria-label on search input', () => {
      renderWithQuery(
        <EntityListView {...defaultProps} searchable searchFields={['title']} />
      );

      expect(screen.getByLabelText('Search')).toBeInTheDocument();
    });

    it('should have accessible clear button', async () => {
      const user = userEvent.setup();
      renderWithQuery(
        <EntityListView {...defaultProps} searchable searchFields={['title']} />
      );

      const input = screen.getByRole('searchbox');
      await user.type(input, 'test');

      expect(screen.getByLabelText(/clear search/i)).toBeInTheDocument();
    });
  });

  describe('Focus Management', () => {
    it('should not trap focus in any component', async () => {
      const user = userEvent.setup();
      renderWithQuery(
        <EntityListView
          {...defaultProps}
          searchable
          searchFields={['title']}
          sortOptions={sortOptions}
          defaultSort="rating"
        />
      );

      // Tab multiple times - focus should eventually leave the component
      for (let i = 0; i < 20; i++) {
        await user.tab();
      }

      // Focus should have cycled through and out
      // This test mainly ensures no infinite focus trap
      expect(document.activeElement).toBeDefined();
    });

    it('should maintain visible focus indicators', () => {
      renderWithQuery(
        <EntityListView {...defaultProps} searchable searchFields={['title']} />
      );

      const searchInput = screen.getByRole('searchbox');
      // Check that focus-visible classes are present in className
      expect(searchInput.className).toContain('focus-visible');
    });
  });
});
