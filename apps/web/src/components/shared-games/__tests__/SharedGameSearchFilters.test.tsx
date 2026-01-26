/**
 * SharedGameSearchFilters Component Tests
 *
 * Issue #2763: Sprint 3 - Catalog & Shared Games Components (0% → 85%)
 *
 * Tests:
 * - Filter panel expansion/collapse
 * - Category multi-select
 * - Mechanic multi-select
 * - Player count inputs
 * - Playing time input
 * - Catalog-only toggle
 * - Active filters badges
 * - Clear filters functionality
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

      expect(screen.getByRole('button', { name: /Filtri/i })).toBeInTheDocument();
    });

    it('does not show filters panel by default', () => {
      render(
        <SharedGameSearchFilters
          filters={defaultFilters}
          onFiltersChange={mockOnFiltersChange}
        />
      );

      expect(screen.queryByText('Categorie')).not.toBeInTheDocument();
    });

    it('shows filters panel when expanded', async () => {
      render(
        <SharedGameSearchFilters
          filters={defaultFilters}
          onFiltersChange={mockOnFiltersChange}
        />
      );

      const toggleButton = screen.getByRole('button', { name: /Filtri/i });
      await act(async () => {
        fireEvent.click(toggleButton);
      });

      await waitFor(() => {
        expect(screen.getByText('Categorie')).toBeInTheDocument();
        expect(screen.getByText('Meccaniche')).toBeInTheDocument();
        expect(screen.getByText('Numero giocatori')).toBeInTheDocument();
        expect(screen.getByText('Tempo massimo di gioco')).toBeInTheDocument();
        expect(screen.getByText('Solo dal catalogo')).toBeInTheDocument();
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

      // No badge element should exist
      const filterButton = screen.getByRole('button', { name: /Filtri/i });
      expect(filterButton.querySelector('[class*="badge"]')).toBeNull();
    });
  });

  // ==========================================================================
  // Category Selection
  // ==========================================================================

  describe('Category Selection', () => {
    it('loads and displays categories from API', async () => {
      render(
        <SharedGameSearchFilters
          filters={defaultFilters}
          onFiltersChange={mockOnFiltersChange}
        />
      );

      // Expand filters
      await act(async () => {
        fireEvent.click(screen.getByRole('button', { name: /Filtri/i }));
      });

      await waitFor(() => {
        expect(screen.getByText('Categorie')).toBeInTheDocument();
      });

      // Open category dropdown
      const categoryButton = screen.getByRole('button', { name: /Seleziona categorie/i });
      await act(async () => {
        fireEvent.click(categoryButton);
      });

      await waitFor(() => {
        expect(screen.getByText('Strategy')).toBeInTheDocument();
        expect(screen.getByText('Family')).toBeInTheDocument();
      });
    });

    it('calls onFiltersChange when category is selected', async () => {
      render(
        <SharedGameSearchFilters
          filters={defaultFilters}
          onFiltersChange={mockOnFiltersChange}
        />
      );

      // Expand filters and open dropdown
      await act(async () => {
        fireEvent.click(screen.getByRole('button', { name: /Filtri/i }));
      });

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /Seleziona categorie/i })).toBeInTheDocument();
      });

      await act(async () => {
        fireEvent.click(screen.getByRole('button', { name: /Seleziona categorie/i }));
      });

      await waitFor(() => {
        expect(screen.getByText('Strategy')).toBeInTheDocument();
      });

      // Find and click the checkbox for Strategy
      const strategyLabel = screen.getByText('Strategy');
      await act(async () => {
        fireEvent.click(strategyLabel);
      });

      expect(mockOnFiltersChange).toHaveBeenCalledWith(
        expect.objectContaining({
          categoryIds: ['cat-strategy'],
        })
      );
    });
  });

  // ==========================================================================
  // Mechanic Selection
  // ==========================================================================

  describe('Mechanic Selection', () => {
    it('loads and displays mechanics from API', async () => {
      render(
        <SharedGameSearchFilters
          filters={defaultFilters}
          onFiltersChange={mockOnFiltersChange}
        />
      );

      // Expand filters
      await act(async () => {
        fireEvent.click(screen.getByRole('button', { name: /Filtri/i }));
      });

      await waitFor(() => {
        expect(screen.getByText('Meccaniche')).toBeInTheDocument();
      });

      // Open mechanic dropdown
      const mechanicButton = screen.getByRole('button', { name: /Seleziona meccaniche/i });
      await act(async () => {
        fireEvent.click(mechanicButton);
      });

      await waitFor(() => {
        expect(screen.getByText('Worker Placement')).toBeInTheDocument();
        expect(screen.getByText('Deck Building')).toBeInTheDocument();
      });
    });
  });

  // ==========================================================================
  // Player Count Filters
  // ==========================================================================

  describe('Player Count Filters', () => {
    it('renders min and max player inputs', async () => {
      render(
        <SharedGameSearchFilters
          filters={defaultFilters}
          onFiltersChange={mockOnFiltersChange}
        />
      );

      // Expand filters
      await act(async () => {
        fireEvent.click(screen.getByRole('button', { name: /Filtri/i }));
      });

      await waitFor(() => {
        expect(screen.getByLabelText('Minimo giocatori')).toBeInTheDocument();
        expect(screen.getByLabelText('Massimo giocatori')).toBeInTheDocument();
      });
    });

    it('calls onFiltersChange when min players is changed', async () => {
      render(
        <SharedGameSearchFilters
          filters={defaultFilters}
          onFiltersChange={mockOnFiltersChange}
        />
      );

      // Expand filters
      await act(async () => {
        fireEvent.click(screen.getByRole('button', { name: /Filtri/i }));
      });

      await waitFor(() => {
        expect(screen.getByLabelText('Minimo giocatori')).toBeInTheDocument();
      });

      const minInput = screen.getByLabelText('Minimo giocatori');
      await act(async () => {
        fireEvent.change(minInput, { target: { value: '2' } });
      });

      expect(mockOnFiltersChange).toHaveBeenCalledWith(
        expect.objectContaining({
          minPlayers: 2,
        })
      );
    });

    it('calls onFiltersChange when max players is changed', async () => {
      render(
        <SharedGameSearchFilters
          filters={defaultFilters}
          onFiltersChange={mockOnFiltersChange}
        />
      );

      // Expand filters
      await act(async () => {
        fireEvent.click(screen.getByRole('button', { name: /Filtri/i }));
      });

      await waitFor(() => {
        expect(screen.getByLabelText('Massimo giocatori')).toBeInTheDocument();
      });

      const maxInput = screen.getByLabelText('Massimo giocatori');
      await act(async () => {
        fireEvent.change(maxInput, { target: { value: '4' } });
      });

      expect(mockOnFiltersChange).toHaveBeenCalledWith(
        expect.objectContaining({
          maxPlayers: 4,
        })
      );
    });

    it('sets null when player input is cleared', async () => {
      const filtersWithPlayers: SearchFilters = {
        ...defaultFilters,
        minPlayers: 2,
      };

      render(
        <SharedGameSearchFilters
          filters={filtersWithPlayers}
          onFiltersChange={mockOnFiltersChange}
        />
      );

      // Expand filters - use getAllByRole to find toggle button (first one, not "Cancella filtri")
      const filterButtons = screen.getAllByRole('button', { name: /Filtri/i });
      await act(async () => {
        // First button is the toggle, second is "Cancella filtri"
        fireEvent.click(filterButtons[0]);
      });

      await waitFor(() => {
        expect(screen.getByLabelText('Minimo giocatori')).toBeInTheDocument();
      });

      const minInput = screen.getByLabelText('Minimo giocatori');
      await act(async () => {
        fireEvent.change(minInput, { target: { value: '' } });
      });

      expect(mockOnFiltersChange).toHaveBeenCalledWith(
        expect.objectContaining({
          minPlayers: null,
        })
      );
    });
  });

  // ==========================================================================
  // Playing Time Filter
  // ==========================================================================

  describe('Playing Time Filter', () => {
    it('renders max playing time input', async () => {
      render(
        <SharedGameSearchFilters
          filters={defaultFilters}
          onFiltersChange={mockOnFiltersChange}
        />
      );

      // Expand filters
      await act(async () => {
        fireEvent.click(screen.getByRole('button', { name: /Filtri/i }));
      });

      await waitFor(() => {
        expect(screen.getByLabelText('Tempo massimo in minuti')).toBeInTheDocument();
      });
    });

    it('calls onFiltersChange when max playing time is changed', async () => {
      render(
        <SharedGameSearchFilters
          filters={defaultFilters}
          onFiltersChange={mockOnFiltersChange}
        />
      );

      // Expand filters
      await act(async () => {
        fireEvent.click(screen.getByRole('button', { name: /Filtri/i }));
      });

      await waitFor(() => {
        expect(screen.getByLabelText('Tempo massimo in minuti')).toBeInTheDocument();
      });

      const timeInput = screen.getByLabelText('Tempo massimo in minuti');
      await act(async () => {
        fireEvent.change(timeInput, { target: { value: '60' } });
      });

      expect(mockOnFiltersChange).toHaveBeenCalledWith(
        expect.objectContaining({
          maxPlayingTime: 60,
        })
      );
    });
  });

  // ==========================================================================
  // Catalog Only Toggle
  // ==========================================================================

  describe('Catalog Only Toggle', () => {
    it('renders catalog only switch', async () => {
      render(
        <SharedGameSearchFilters
          filters={defaultFilters}
          onFiltersChange={mockOnFiltersChange}
        />
      );

      // Expand filters
      await act(async () => {
        fireEvent.click(screen.getByRole('button', { name: /Filtri/i }));
      });

      await waitFor(() => {
        expect(screen.getByRole('switch', { name: /Solo dal catalogo/i })).toBeInTheDocument();
      });
    });

    it('calls onFiltersChange when catalog only is toggled', async () => {
      render(
        <SharedGameSearchFilters
          filters={defaultFilters}
          onFiltersChange={mockOnFiltersChange}
        />
      );

      // Expand filters
      await act(async () => {
        fireEvent.click(screen.getByRole('button', { name: /Filtri/i }));
      });

      await waitFor(() => {
        expect(screen.getByRole('switch', { name: /Solo dal catalogo/i })).toBeInTheDocument();
      });

      const toggle = screen.getByRole('switch', { name: /Solo dal catalogo/i });
      await act(async () => {
        fireEvent.click(toggle);
      });

      expect(mockOnFiltersChange).toHaveBeenCalledWith(
        expect.objectContaining({
          catalogOnly: true,
        })
      );
    });
  });

  // ==========================================================================
  // Active Filters Badges
  // ==========================================================================

  describe('Active Filters Badges', () => {
    it('shows category badges when collapsed', async () => {
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

      // Wait for the category badge to appear (shows category name from mock data)
      await waitFor(() => {
        // Should show badge with category name from mock useCategories hook
        expect(screen.getByText('Strategy')).toBeInTheDocument();
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

    it('shows playing time badge when set', () => {
      const filtersWithTime: SearchFilters = {
        ...defaultFilters,
        maxPlayingTime: 60,
      };

      render(
        <SharedGameSearchFilters
          filters={filtersWithTime}
          onFiltersChange={mockOnFiltersChange}
        />
      );

      expect(screen.getByText('Max 60 min')).toBeInTheDocument();
    });

    it('shows catalog only badge when enabled', () => {
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

      expect(screen.getByText('Solo catalogo')).toBeInTheDocument();
    });
  });

  // ==========================================================================
  // Clear Filters
  // ==========================================================================

  describe('Clear Filters', () => {
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

      expect(screen.getByText('Cancella filtri')).toBeInTheDocument();
    });

    it('does not show clear button when no filters', () => {
      render(
        <SharedGameSearchFilters
          filters={defaultFilters}
          onFiltersChange={mockOnFiltersChange}
        />
      );

      expect(screen.queryByText('Cancella filtri')).not.toBeInTheDocument();
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

      const clearButton = screen.getByText('Cancella filtri');
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

    it('removes playing time filter when badge X is clicked', async () => {
      const filtersWithTime: SearchFilters = {
        ...defaultFilters,
        maxPlayingTime: 60,
      };

      render(
        <SharedGameSearchFilters
          filters={filtersWithTime}
          onFiltersChange={mockOnFiltersChange}
        />
      );

      const removeButton = screen.getByLabelText('Rimuovi filtro tempo');
      await act(async () => {
        fireEvent.click(removeButton);
      });

      expect(mockOnFiltersChange).toHaveBeenCalledWith(
        expect.objectContaining({
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
});
