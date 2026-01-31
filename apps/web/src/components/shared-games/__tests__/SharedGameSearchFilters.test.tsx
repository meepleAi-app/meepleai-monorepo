/**
 * SharedGameSearchFilters Component Tests
 *
 * Issue #2763: Sprint 3 - Catalog & Shared Games Components (0% → 85%)
 * Updated for Issue #2873: Advanced Filter Panel Component
 *
 * Tests:
 * - Filter toggle button with active count badge
 * - Advanced Filter Panel integration
 * - Active filters badges display
 * - Clear all filters functionality
 * - Individual filter removal from badges
 *
 * Note: Mocks api module directly due to singleton timing issues with MSW.
 * The api singleton is created at module import time before MSW can intercept.
 */

import { render, screen, fireEvent, waitFor, act } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import {
  SharedGameSearchFilters,
  DEFAULT_FILTERS,
  type SearchFilters,
} from '../SharedGameSearchFilters';

// Mock the api module directly (singleton timing issue with MSW)
const mockGetCategories = vi.fn();
const mockGetMechanics = vi.fn();

vi.mock('@/lib/api', () => ({
  api: {
    sharedGames: {
      getCategories: () => mockGetCategories(),
      getMechanics: () => mockGetMechanics(),
    },
  },
}));

// Mock categories data
const mockCategories = [
  { id: 'cat-strategy', name: 'Strategy' },
  { id: 'cat-family', name: 'Family' },
  { id: 'cat-party', name: 'Party Games' },
];

// Mock mechanics data
const mockMechanics = [
  { id: 'mech-worker-placement', name: 'Worker Placement' },
  { id: 'mech-deck-building', name: 'Deck Building' },
  { id: 'mech-area-control', name: 'Area Control' },
];

describe('SharedGameSearchFilters', () => {
  const defaultFilters: SearchFilters = { ...DEFAULT_FILTERS };
  const mockOnFiltersChange = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();

    // Set up default API mocks
    mockGetCategories.mockResolvedValue(mockCategories);
    mockGetMechanics.mockResolvedValue(mockMechanics);
  });

  // ==========================================================================
  // Basic Rendering
  // ==========================================================================

  describe('Basic Rendering', () => {
    it('renders filter toggle button', async () => {
      render(
        <SharedGameSearchFilters
          filters={defaultFilters}
          onFiltersChange={mockOnFiltersChange}
        />
      );

      expect(screen.getByRole('button', { name: /Filtri Avanzati/i })).toBeInTheDocument();
    });

    it('does not show filter panel by default', () => {
      render(
        <SharedGameSearchFilters
          filters={defaultFilters}
          onFiltersChange={mockOnFiltersChange}
        />
      );

      // Sheet content should not be visible
      expect(screen.queryByText('Applica Filtri')).not.toBeInTheDocument();
    });

    it('opens AdvancedFilterPanel when button is clicked', async () => {
      render(
        <SharedGameSearchFilters
          filters={defaultFilters}
          onFiltersChange={mockOnFiltersChange}
        />
      );

      const toggleButton = screen.getByRole('button', { name: /Filtri Avanzati/i });
      await act(async () => {
        fireEvent.click(toggleButton);
      });

      await waitFor(() => {
        // Sheet dialog should be visible with panel content
        expect(screen.getByRole('dialog')).toBeInTheDocument();
        expect(screen.getByText('Applica Filtri')).toBeInTheDocument();
      });
    });
  });

  // ==========================================================================
  // Active Filters Count
  // ==========================================================================

  describe('Active Filters Count', () => {
    it('shows count badge when filters are active', () => {
      const activeFilters: SearchFilters = {
        ...defaultFilters,
        categoryIds: ['cat-strategy'],
        minPlayers: 2,
      };

      render(
        <SharedGameSearchFilters
          filters={activeFilters}
          onFiltersChange={mockOnFiltersChange}
        />
      );

      // Should show badge with count 2 (categories + players)
      expect(screen.getByText('2')).toBeInTheDocument();
    });

    it('does not show count badge when no filters', () => {
      render(
        <SharedGameSearchFilters
          filters={defaultFilters}
          onFiltersChange={mockOnFiltersChange}
        />
      );

      // Only the button should exist, no badge number
      const filterButton = screen.getByRole('button', { name: /Filtri Avanzati/i });
      // Check that there's no number in the button (no count badge)
      expect(filterButton.textContent).not.toMatch(/\d+/);
    });

    it('counts complexity filter correctly', () => {
      const activeFilters: SearchFilters = {
        ...defaultFilters,
        minComplexity: 2.0,
        maxComplexity: 4.0,
      };

      render(
        <SharedGameSearchFilters
          filters={activeFilters}
          onFiltersChange={mockOnFiltersChange}
        />
      );

      // Should show badge with count 1 (complexity)
      expect(screen.getByText('1')).toBeInTheDocument();
    });

    it('counts all filter types correctly', () => {
      const activeFilters: SearchFilters = {
        ...defaultFilters,
        categoryIds: ['cat-strategy'],
        mechanicIds: ['mech-worker-placement'],
        minPlayers: 2,
        minComplexity: 2.0,
        minPlayingTime: 30,
        catalogOnly: true,
      };

      render(
        <SharedGameSearchFilters
          filters={activeFilters}
          onFiltersChange={mockOnFiltersChange}
        />
      );

      // Should show badge with count 6
      expect(screen.getByText('6')).toBeInTheDocument();
    });
  });

  // ==========================================================================
  // Active Filters Badges
  // ==========================================================================

  describe('Active Filters Badges', () => {
    it('shows category badges with orange styling', async () => {
      const filtersWithCategory: SearchFilters = {
        ...defaultFilters,
        categoryIds: ['cat-strategy'],
      };

      render(
        <SharedGameSearchFilters
          filters={filtersWithCategory}
          onFiltersChange={mockOnFiltersChange}
        />
      );

      await waitFor(() => {
        const badge = screen.getByText('Strategy');
        expect(badge).toBeInTheDocument();
        // Check for orange styling class
        expect(badge.closest('.bg-orange-500\\/10')).toBeInTheDocument();
      });
    });

    it('shows mechanic badges with orange border', async () => {
      const filtersWithMechanic: SearchFilters = {
        ...defaultFilters,
        mechanicIds: ['mech-worker-placement'],
      };

      render(
        <SharedGameSearchFilters
          filters={filtersWithMechanic}
          onFiltersChange={mockOnFiltersChange}
        />
      );

      await waitFor(() => {
        const badge = screen.getByText('Worker Placement');
        expect(badge).toBeInTheDocument();
      });
    });

    it('shows player filter badge when set', () => {
      const filtersWithPlayers: SearchFilters = {
        ...defaultFilters,
        minPlayers: 2,
        maxPlayers: 4,
      };

      render(
        <SharedGameSearchFilters
          filters={filtersWithPlayers}
          onFiltersChange={mockOnFiltersChange}
        />
      );

      expect(screen.getByText('2-4 giocatori')).toBeInTheDocument();
    });

    it('shows complexity filter badge when set', () => {
      const filtersWithComplexity: SearchFilters = {
        ...defaultFilters,
        minComplexity: 2.0,
        maxComplexity: 4.0,
      };

      render(
        <SharedGameSearchFilters
          filters={filtersWithComplexity}
          onFiltersChange={mockOnFiltersChange}
        />
      );

      expect(screen.getByText(/Complessit.*2\.0.*4\.0/)).toBeInTheDocument();
    });

    it('shows playing time badge when set', () => {
      const filtersWithTime: SearchFilters = {
        ...defaultFilters,
        minPlayingTime: 30,
        maxPlayingTime: 120,
      };

      render(
        <SharedGameSearchFilters
          filters={filtersWithTime}
          onFiltersChange={mockOnFiltersChange}
        />
      );

      expect(screen.getByText('30-120 min')).toBeInTheDocument();
    });

    it('shows catalog only badge with solid orange when enabled', () => {
      const filtersWithCatalog: SearchFilters = {
        ...defaultFilters,
        catalogOnly: true,
      };

      render(
        <SharedGameSearchFilters
          filters={filtersWithCatalog}
          onFiltersChange={mockOnFiltersChange}
        />
      );

      const badge = screen.getByText('Solo catalogo');
      expect(badge).toBeInTheDocument();
      expect(badge.closest('.bg-orange-500')).toBeInTheDocument();
    });
  });

  // ==========================================================================
  // Clear All Filters
  // ==========================================================================

  describe('Clear All Filters', () => {
    it('shows clear button when filters are active', () => {
      const activeFilters: SearchFilters = {
        ...defaultFilters,
        categoryIds: ['cat-strategy'],
      };

      render(
        <SharedGameSearchFilters
          filters={activeFilters}
          onFiltersChange={mockOnFiltersChange}
        />
      );

      expect(screen.getByText('Cancella tutti')).toBeInTheDocument();
    });

    it('does not show clear button when no filters', () => {
      render(
        <SharedGameSearchFilters
          filters={defaultFilters}
          onFiltersChange={mockOnFiltersChange}
        />
      );

      expect(screen.queryByText('Cancella tutti')).not.toBeInTheDocument();
    });

    it('calls onFiltersChange with DEFAULT_FILTERS when clear is clicked', async () => {
      const activeFilters: SearchFilters = {
        ...defaultFilters,
        categoryIds: ['cat-strategy'],
        minPlayers: 2,
      };

      render(
        <SharedGameSearchFilters
          filters={activeFilters}
          onFiltersChange={mockOnFiltersChange}
        />
      );

      const clearButton = screen.getByText('Cancella tutti');
      await act(async () => {
        fireEvent.click(clearButton);
      });

      expect(mockOnFiltersChange).toHaveBeenCalledWith(DEFAULT_FILTERS);
    });
  });

  // ==========================================================================
  // Remove Individual Filter Badge
  // ==========================================================================

  describe('Remove Individual Filter', () => {
    it('removes category when badge X is clicked', async () => {
      const filtersWithCategory: SearchFilters = {
        ...defaultFilters,
        categoryIds: ['cat-strategy'],
      };

      render(
        <SharedGameSearchFilters
          filters={filtersWithCategory}
          onFiltersChange={mockOnFiltersChange}
        />
      );

      await waitFor(() => {
        expect(screen.getByText('Strategy')).toBeInTheDocument();
      });

      const removeButton = screen.getByLabelText('Rimuovi Strategy');
      await act(async () => {
        fireEvent.click(removeButton);
      });

      expect(mockOnFiltersChange).toHaveBeenCalledWith(
        expect.objectContaining({
          categoryIds: [],
        })
      );
    });

    it('removes mechanic when badge X is clicked', async () => {
      const filtersWithMechanic: SearchFilters = {
        ...defaultFilters,
        mechanicIds: ['mech-worker-placement'],
      };

      render(
        <SharedGameSearchFilters
          filters={filtersWithMechanic}
          onFiltersChange={mockOnFiltersChange}
        />
      );

      await waitFor(() => {
        expect(screen.getByText('Worker Placement')).toBeInTheDocument();
      });

      const removeButton = screen.getByLabelText('Rimuovi Worker Placement');
      await act(async () => {
        fireEvent.click(removeButton);
      });

      expect(mockOnFiltersChange).toHaveBeenCalledWith(
        expect.objectContaining({
          mechanicIds: [],
        })
      );
    });

    it('removes player filter when badge X is clicked', async () => {
      const filtersWithPlayers: SearchFilters = {
        ...defaultFilters,
        minPlayers: 2,
        maxPlayers: 4,
      };

      render(
        <SharedGameSearchFilters
          filters={filtersWithPlayers}
          onFiltersChange={mockOnFiltersChange}
        />
      );

      const removeButton = screen.getByLabelText('Rimuovi filtro giocatori');
      await act(async () => {
        fireEvent.click(removeButton);
      });

      expect(mockOnFiltersChange).toHaveBeenCalledWith(
        expect.objectContaining({
          minPlayers: null,
          maxPlayers: null,
        })
      );
    });

    it('removes complexity filter when badge X is clicked', async () => {
      const filtersWithComplexity: SearchFilters = {
        ...defaultFilters,
        minComplexity: 2.0,
        maxComplexity: 4.0,
      };

      render(
        <SharedGameSearchFilters
          filters={filtersWithComplexity}
          onFiltersChange={mockOnFiltersChange}
        />
      );

      const removeButton = screen.getByLabelText(/Rimuovi filtro complessit/);
      await act(async () => {
        fireEvent.click(removeButton);
      });

      expect(mockOnFiltersChange).toHaveBeenCalledWith(
        expect.objectContaining({
          minComplexity: null,
          maxComplexity: null,
        })
      );
    });

    it('removes duration filter when badge X is clicked', async () => {
      const filtersWithTime: SearchFilters = {
        ...defaultFilters,
        minPlayingTime: 30,
        maxPlayingTime: 120,
      };

      render(
        <SharedGameSearchFilters
          filters={filtersWithTime}
          onFiltersChange={mockOnFiltersChange}
        />
      );

      const removeButton = screen.getByLabelText('Rimuovi filtro durata');
      await act(async () => {
        fireEvent.click(removeButton);
      });

      expect(mockOnFiltersChange).toHaveBeenCalledWith(
        expect.objectContaining({
          minPlayingTime: null,
          maxPlayingTime: null,
        })
      );
    });

    it('removes catalog only filter when badge X is clicked', async () => {
      const filtersWithCatalog: SearchFilters = {
        ...defaultFilters,
        catalogOnly: true,
      };

      render(
        <SharedGameSearchFilters
          filters={filtersWithCatalog}
          onFiltersChange={mockOnFiltersChange}
        />
      );

      const removeButton = screen.getByLabelText('Rimuovi filtro catalogo');
      await act(async () => {
        fireEvent.click(removeButton);
      });

      expect(mockOnFiltersChange).toHaveBeenCalledWith(
        expect.objectContaining({
          catalogOnly: false,
        })
      );
    });
  });

  // ==========================================================================
  // Orange Button Styling
  // ==========================================================================

  describe('Orange Button Styling', () => {
    it('shows orange border on toggle button when filters are active', () => {
      const activeFilters: SearchFilters = {
        ...defaultFilters,
        categoryIds: ['cat-strategy'],
      };

      render(
        <SharedGameSearchFilters
          filters={activeFilters}
          onFiltersChange={mockOnFiltersChange}
        />
      );

      const toggleButton = screen.getByRole('button', { name: /Filtri Avanzati/i });
      expect(toggleButton).toHaveClass('border-orange-500/50');
    });

    it('shows orange badge on toggle button when filters are active', () => {
      const activeFilters: SearchFilters = {
        ...defaultFilters,
        categoryIds: ['cat-strategy'],
      };

      render(
        <SharedGameSearchFilters
          filters={activeFilters}
          onFiltersChange={mockOnFiltersChange}
        />
      );

      // Check for orange badge inside the button
      const badge = screen.getByText('1');
      expect(badge).toHaveClass('bg-orange-500');
    });
  });

  // ==========================================================================
  // Integration with AdvancedFilterPanel
  // ==========================================================================

  describe('AdvancedFilterPanel Integration', () => {
    it('passes filters to AdvancedFilterPanel', async () => {
      const activeFilters: SearchFilters = {
        ...defaultFilters,
        categoryIds: ['cat-strategy'],
      };

      render(
        <SharedGameSearchFilters
          filters={activeFilters}
          onFiltersChange={mockOnFiltersChange}
        />
      );

      // Open panel
      const toggleButton = screen.getByRole('button', { name: /Filtri Avanzati/i });
      await act(async () => {
        fireEvent.click(toggleButton);
      });

      await waitFor(() => {
        // Panel should show "1 attivi" from the passed filters
        expect(screen.getByText(/1 attivi/i)).toBeInTheDocument();
      });
    });

    it('calls onFiltersChange when panel applies filters', async () => {
      render(
        <SharedGameSearchFilters
          filters={defaultFilters}
          onFiltersChange={mockOnFiltersChange}
        />
      );

      // Open panel
      const toggleButton = screen.getByRole('button', { name: /Filtri Avanzati/i });
      await act(async () => {
        fireEvent.click(toggleButton);
      });

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /Applica Filtri/i })).toBeInTheDocument();
      });

      // Click Apply
      const applyButton = screen.getByRole('button', { name: /Applica Filtri/i });
      await act(async () => {
        fireEvent.click(applyButton);
      });

      expect(mockOnFiltersChange).toHaveBeenCalled();
    });
  });
});
