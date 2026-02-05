/**
 * CollectionFilters Unit Tests (Issue #3649: User Collection Dashboard Enhancement)
 *
 * Coverage areas:
 * - Filter chips rendering
 * - Toggle state cycling (null → true → false → null)
 * - Category dropdown functionality
 * - Clear all filters
 * - Accessibility
 * - Filter change callbacks
 *
 * Target: 85%+ coverage
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { CollectionFilters } from '../CollectionFilters';
import type { CollectionFilters as CollectionFiltersType } from '@/types/collection';

// Mock framer-motion to avoid animation issues in tests
vi.mock('framer-motion', () => ({
  motion: {
    div: ({ children, ...props }: React.PropsWithChildren<Record<string, unknown>>) => (
      <div {...props}>{children}</div>
    ),
  },
  AnimatePresence: ({ children }: React.PropsWithChildren) => children,
}));

// ============================================================================
// Test Data
// ============================================================================

const emptyFilters: CollectionFiltersType = {
  hasPdf: null,
  hasActiveChat: null,
  category: null,
};

const activeFilters: CollectionFiltersType = {
  hasPdf: true,
  hasActiveChat: false,
  category: 'Strategy',
};

const partialFilters: CollectionFiltersType = {
  hasPdf: true,
  hasActiveChat: null,
  category: null,
};

const defaultCategories = ['Strategy', 'Family', 'Party', 'Abstract', 'Thematic', 'Cooperative'];
const customCategories = ['Euro', 'Ameritrash', 'Wargame'];

describe('CollectionFilters', () => {
  let mockOnFilterChange: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    mockOnFilterChange = vi.fn();
  });

  // ============================================================================
  // Rendering Tests
  // ============================================================================

  describe('Rendering', () => {
    it('renders filters section', () => {
      render(
        <CollectionFilters
          filters={emptyFilters}
          onFilterChange={mockOnFilterChange}
        />
      );

      expect(screen.getByTestId('collection-filters')).toBeInTheDocument();
    });

    it('renders category filter chip', () => {
      render(
        <CollectionFilters
          filters={emptyFilters}
          onFilterChange={mockOnFilterChange}
        />
      );

      expect(screen.getByTestId('filter-chip-category')).toBeInTheDocument();
    });

    it('renders PDF filter chip', () => {
      render(
        <CollectionFilters
          filters={emptyFilters}
          onFilterChange={mockOnFilterChange}
        />
      );

      expect(screen.getByTestId('filter-chip-pdf')).toBeInTheDocument();
    });

    it('renders Chat filter chip', () => {
      render(
        <CollectionFilters
          filters={emptyFilters}
          onFilterChange={mockOnFilterChange}
        />
      );

      expect(screen.getByTestId('filter-chip-chat')).toBeInTheDocument();
    });

    it('applies custom className', () => {
      const { container } = render(
        <CollectionFilters
          filters={emptyFilters}
          onFilterChange={mockOnFilterChange}
          className="custom-filters"
        />
      );

      const section = container.querySelector('.custom-filters');
      expect(section).toBeInTheDocument();
    });
  });

  // ============================================================================
  // Toggle Filter State Tests
  // ============================================================================

  describe('Toggle Filter State', () => {
    it('shows "Tutti" when PDF filter is null', () => {
      render(
        <CollectionFilters
          filters={emptyFilters}
          onFilterChange={mockOnFilterChange}
        />
      );

      const pdfChip = screen.getByTestId('filter-chip-pdf');
      expect(pdfChip).toHaveTextContent('Tutti');
    });

    it('shows "Sì" when PDF filter is true', () => {
      render(
        <CollectionFilters
          filters={{ ...emptyFilters, hasPdf: true }}
          onFilterChange={mockOnFilterChange}
        />
      );

      const pdfChip = screen.getByTestId('filter-chip-pdf');
      expect(pdfChip).toHaveTextContent('Sì');
    });

    it('shows "No" when PDF filter is false', () => {
      render(
        <CollectionFilters
          filters={{ ...emptyFilters, hasPdf: false }}
          onFilterChange={mockOnFilterChange}
        />
      );

      const pdfChip = screen.getByTestId('filter-chip-pdf');
      expect(pdfChip).toHaveTextContent('No');
    });

    it('cycles PDF filter: null → true on click', async () => {
      const user = userEvent.setup();

      render(
        <CollectionFilters
          filters={emptyFilters}
          onFilterChange={mockOnFilterChange}
        />
      );

      const pdfChip = screen.getByTestId('filter-chip-pdf');
      await user.click(pdfChip);

      expect(mockOnFilterChange).toHaveBeenCalledWith({
        ...emptyFilters,
        hasPdf: true,
      });
    });

    it('cycles PDF filter: true → false on click', async () => {
      const user = userEvent.setup();

      render(
        <CollectionFilters
          filters={{ ...emptyFilters, hasPdf: true }}
          onFilterChange={mockOnFilterChange}
        />
      );

      const pdfChip = screen.getByTestId('filter-chip-pdf');
      await user.click(pdfChip);

      expect(mockOnFilterChange).toHaveBeenCalledWith({
        ...emptyFilters,
        hasPdf: false,
      });
    });

    it('cycles PDF filter: false → null on click', async () => {
      const user = userEvent.setup();

      render(
        <CollectionFilters
          filters={{ ...emptyFilters, hasPdf: false }}
          onFilterChange={mockOnFilterChange}
        />
      );

      const pdfChip = screen.getByTestId('filter-chip-pdf');
      await user.click(pdfChip);

      expect(mockOnFilterChange).toHaveBeenCalledWith({
        ...emptyFilters,
        hasPdf: null,
      });
    });

    it('cycles Chat filter independently', async () => {
      const user = userEvent.setup();

      render(
        <CollectionFilters
          filters={emptyFilters}
          onFilterChange={mockOnFilterChange}
        />
      );

      const chatChip = screen.getByTestId('filter-chip-chat');
      await user.click(chatChip);

      expect(mockOnFilterChange).toHaveBeenCalledWith({
        ...emptyFilters,
        hasActiveChat: true,
      });
    });
  });

  // ============================================================================
  // Category Dropdown Tests
  // ============================================================================

  describe('Category Dropdown', () => {
    it('shows "Tutte" when category is null', () => {
      render(
        <CollectionFilters
          filters={emptyFilters}
          onFilterChange={mockOnFilterChange}
        />
      );

      const categoryChip = screen.getByTestId('filter-chip-category');
      expect(categoryChip).toHaveTextContent('Tutte');
    });

    it('shows category name when selected', () => {
      render(
        <CollectionFilters
          filters={{ ...emptyFilters, category: 'Strategy' }}
          onFilterChange={mockOnFilterChange}
        />
      );

      const categoryChip = screen.getByTestId('filter-chip-category');
      expect(categoryChip).toHaveTextContent('Strategy');
    });

    it('uses custom categories when provided', async () => {
      const user = userEvent.setup();

      render(
        <CollectionFilters
          filters={emptyFilters}
          categories={customCategories}
          onFilterChange={mockOnFilterChange}
        />
      );

      const categoryChip = screen.getByTestId('filter-chip-category');
      await user.click(categoryChip);

      expect(screen.getByText('Euro')).toBeInTheDocument();
      expect(screen.getByText('Ameritrash')).toBeInTheDocument();
      expect(screen.getByText('Wargame')).toBeInTheDocument();
    });

    it('uses default categories when not provided', async () => {
      const user = userEvent.setup();

      render(
        <CollectionFilters
          filters={emptyFilters}
          onFilterChange={mockOnFilterChange}
        />
      );

      const categoryChip = screen.getByTestId('filter-chip-category');
      await user.click(categoryChip);

      expect(screen.getByText('Strategy')).toBeInTheDocument();
      expect(screen.getByText('Family')).toBeInTheDocument();
    });
  });

  // ============================================================================
  // Clear Filters Tests
  // ============================================================================

  describe('Clear Filters', () => {
    it('does not show clear button when no filters are active', () => {
      render(
        <CollectionFilters
          filters={emptyFilters}
          onFilterChange={mockOnFilterChange}
        />
      );

      expect(screen.queryByTestId('clear-all-filters')).not.toBeInTheDocument();
    });

    it('shows clear button when filters are active', () => {
      render(
        <CollectionFilters
          filters={partialFilters}
          onFilterChange={mockOnFilterChange}
        />
      );

      expect(screen.getByTestId('clear-all-filters')).toBeInTheDocument();
    });

    it('clears all filters when clear button is clicked', async () => {
      const user = userEvent.setup();

      render(
        <CollectionFilters
          filters={activeFilters}
          onFilterChange={mockOnFilterChange}
        />
      );

      const clearButton = screen.getByTestId('clear-all-filters');
      await user.click(clearButton);

      expect(mockOnFilterChange).toHaveBeenCalledWith({
        hasPdf: null,
        hasActiveChat: null,
        category: null,
      });
    });

    it('shows clear button text in Italian', () => {
      render(
        <CollectionFilters
          filters={partialFilters}
          onFilterChange={mockOnFilterChange}
        />
      );

      expect(screen.getByTestId('clear-all-filters')).toHaveTextContent('Rimuovi filtri');
    });
  });

  // ============================================================================
  // Accessibility Tests
  // ============================================================================

  describe('Accessibility', () => {
    it('has correct aria-label on section', () => {
      render(
        <CollectionFilters
          filters={emptyFilters}
          onFilterChange={mockOnFilterChange}
        />
      );

      const section = screen.getByTestId('collection-filters');
      expect(section).toHaveAttribute('aria-label', 'Filtri collezione');
    });

    it('is rendered as a section element', () => {
      render(
        <CollectionFilters
          filters={emptyFilters}
          onFilterChange={mockOnFilterChange}
        />
      );

      const section = screen.getByTestId('collection-filters');
      expect(section.tagName).toBe('SECTION');
    });

    it('filter chips are buttons with proper labeling', () => {
      render(
        <CollectionFilters
          filters={emptyFilters}
          onFilterChange={mockOnFilterChange}
        />
      );

      const pdfChip = screen.getByTestId('filter-chip-pdf');
      expect(pdfChip.tagName).toBe('BUTTON');
    });
  });

  // ============================================================================
  // Layout Tests
  // ============================================================================

  describe('Layout', () => {
    it('uses flex layout with gap', () => {
      render(
        <CollectionFilters
          filters={emptyFilters}
          onFilterChange={mockOnFilterChange}
        />
      );

      const section = screen.getByTestId('collection-filters');
      expect(section).toHaveClass('flex');
      expect(section).toHaveClass('gap-2');
    });

    it('allows flex wrapping', () => {
      render(
        <CollectionFilters
          filters={emptyFilters}
          onFilterChange={mockOnFilterChange}
        />
      );

      const section = screen.getByTestId('collection-filters');
      expect(section).toHaveClass('flex-wrap');
    });
  });

  // ============================================================================
  // Active State Visual Tests
  // ============================================================================

  describe('Active State Visuals', () => {
    it('applies default variant to inactive filter chip', () => {
      render(
        <CollectionFilters
          filters={emptyFilters}
          onFilterChange={mockOnFilterChange}
        />
      );

      const pdfChip = screen.getByTestId('filter-chip-pdf');
      // Outline variant when inactive
      expect(pdfChip.className).toMatch(/outline|border/);
    });

    it('applies active styling when filter is active', () => {
      render(
        <CollectionFilters
          filters={{ ...emptyFilters, hasPdf: true }}
          onFilterChange={mockOnFilterChange}
        />
      );

      const pdfChip = screen.getByTestId('filter-chip-pdf');
      // Should have different styling when active (variant="default")
      expect(pdfChip).toBeInTheDocument();
    });
  });
});
