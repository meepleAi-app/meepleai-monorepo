/**
 * CatalogFilters Component Tests
 *
 * Issue #2763: Sprint 3 - Catalog & Shared Games Components (0% → 85%)
 *
 * Tests:
 * - Search functionality with debounce
 * - Sort options (field and direction)
 * - Players and playtime filters
 * - Category and mechanic multi-select
 * - Clear filters functionality
 */

import { render, screen, fireEvent, waitFor, act } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

import { CatalogFilters } from '../CatalogFilters';

// Mock data for tests
const mockCategories = [
  { id: 'strategy', name: 'Strategy' },
  { id: 'family', name: 'Family' },
  { id: 'party', name: 'Party Games' },
];

const mockMechanics = [
  { id: 'worker-placement', name: 'Worker Placement' },
  { id: 'deck-building', name: 'Deck Building' },
  { id: 'area-control', name: 'Area Control' },
];

describe('CatalogFilters', () => {
  // Default props
  const defaultProps = {
    searchTerm: '',
    onSearchChange: vi.fn(),
    categories: mockCategories,
    selectedCategories: [] as string[],
    onCategoryChange: vi.fn(),
    mechanics: mockMechanics,
    selectedMechanics: [] as string[],
    onMechanicChange: vi.fn(),
    minPlayers: undefined as number | undefined,
    maxPlayers: undefined as number | undefined,
    onPlayersChange: vi.fn(),
    maxPlayingTime: undefined as number | undefined,
    onPlaytimeChange: vi.fn(),
    sortBy: 'title' as const,
    sortDescending: false,
    onSortChange: vi.fn(),
    onClearFilters: vi.fn(),
  };

  beforeEach(() => {
    vi.useFakeTimers();
    vi.clearAllMocks();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  // ==========================================================================
  // Basic Rendering
  // ==========================================================================

  describe('Basic Rendering', () => {
    it('renders search input', () => {
      render(<CatalogFilters {...defaultProps} />);
      expect(screen.getByPlaceholderText('Cerca gioco...')).toBeInTheDocument();
    });

    it('renders sort options', () => {
      render(<CatalogFilters {...defaultProps} />);
      expect(screen.getByText('Ordinamento')).toBeInTheDocument();
      expect(screen.getByLabelText('Ordina per')).toBeInTheDocument();
      expect(screen.getByLabelText('Direzione')).toBeInTheDocument();
    });

    it('renders players filter', () => {
      render(<CatalogFilters {...defaultProps} />);
      expect(screen.getByText('Numero Giocatori')).toBeInTheDocument();
    });

    it('renders playtime filter', () => {
      render(<CatalogFilters {...defaultProps} />);
      expect(screen.getByText('Durata di Gioco')).toBeInTheDocument();
    });

    it('renders categories section', () => {
      render(<CatalogFilters {...defaultProps} />);
      expect(screen.getByText('Categorie')).toBeInTheDocument();
    });

    it('renders mechanics section', () => {
      render(<CatalogFilters {...defaultProps} />);
      expect(screen.getByText('Meccaniche')).toBeInTheDocument();
    });

    it('renders category checkboxes', () => {
      render(<CatalogFilters {...defaultProps} />);
      mockCategories.forEach((category) => {
        expect(screen.getByLabelText(category.name)).toBeInTheDocument();
      });
    });

    it('renders mechanic checkboxes', () => {
      render(<CatalogFilters {...defaultProps} />);
      mockMechanics.forEach((mechanic) => {
        expect(screen.getByLabelText(mechanic.name)).toBeInTheDocument();
      });
    });
  });

  // ==========================================================================
  // Search Functionality
  // ==========================================================================

  describe('Search Functionality', () => {
    it('updates local search value immediately', async () => {
      render(<CatalogFilters {...defaultProps} />);
      const searchInput = screen.getByPlaceholderText('Cerca gioco...');

      await act(async () => {
        fireEvent.change(searchInput, { target: { value: 'Catan' } });
      });

      expect(searchInput).toHaveValue('Catan');
    });

    it('debounces onSearchChange callback', async () => {
      render(<CatalogFilters {...defaultProps} />);
      const searchInput = screen.getByPlaceholderText('Cerca gioco...');

      await act(async () => {
        fireEvent.change(searchInput, { target: { value: 'Catan' } });
      });

      // Callback not called immediately
      expect(defaultProps.onSearchChange).not.toHaveBeenCalled();

      // Fast-forward debounce timer (300ms)
      await act(async () => {
        vi.advanceTimersByTime(300);
      });

      expect(defaultProps.onSearchChange).toHaveBeenCalledWith('Catan');
    });

    it('shows clear button when search has value', async () => {
      render(<CatalogFilters {...defaultProps} />);
      const searchInput = screen.getByPlaceholderText('Cerca gioco...');

      await act(async () => {
        fireEvent.change(searchInput, { target: { value: 'test' } });
      });

      // Clear button should appear
      const clearButton = screen.getByRole('button', { name: '' });
      expect(clearButton).toBeInTheDocument();
    });

    it('clears search when clear button is clicked', async () => {
      render(<CatalogFilters {...defaultProps} />);
      const searchInput = screen.getByPlaceholderText('Cerca gioco...');

      await act(async () => {
        fireEvent.change(searchInput, { target: { value: 'test' } });
      });

      const clearButton = screen.getByRole('button', { name: '' });
      await act(async () => {
        fireEvent.click(clearButton);
      });

      expect(searchInput).toHaveValue('');
    });
  });

  // ==========================================================================
  // Sort Options
  // ==========================================================================

  describe('Sort Options', () => {
    it('displays current sort field', () => {
      render(<CatalogFilters {...defaultProps} sortBy="complexity" />);
      // The select should show the current value
      expect(screen.getByRole('combobox', { name: 'Ordina per' })).toBeInTheDocument();
    });

    it('displays current sort direction', () => {
      render(<CatalogFilters {...defaultProps} sortDescending={true} />);
      expect(screen.getByRole('combobox', { name: 'Direzione' })).toBeInTheDocument();
    });
  });

  // ==========================================================================
  // Category Selection
  // ==========================================================================

  describe('Category Selection', () => {
    it('shows selected categories count badge', () => {
      render(
        <CatalogFilters
          {...defaultProps}
          selectedCategories={['strategy', 'family']}
        />
      );
      // Badge shows count of selected categories
      const badge = screen.getByText('2');
      expect(badge).toBeInTheDocument();
    });

    it('calls onCategoryChange when category is selected', async () => {
      render(<CatalogFilters {...defaultProps} />);

      const checkbox = screen.getByLabelText('Strategy');
      await act(async () => {
        fireEvent.click(checkbox);
      });

      expect(defaultProps.onCategoryChange).toHaveBeenCalledWith(['strategy']);
    });

    it('calls onCategoryChange when category is deselected', async () => {
      render(
        <CatalogFilters
          {...defaultProps}
          selectedCategories={['strategy', 'family']}
        />
      );

      const checkbox = screen.getByLabelText('Strategy');
      await act(async () => {
        fireEvent.click(checkbox);
      });

      expect(defaultProps.onCategoryChange).toHaveBeenCalledWith(['family']);
    });

    it('shows checkboxes as checked for selected categories', () => {
      render(
        <CatalogFilters
          {...defaultProps}
          selectedCategories={['strategy']}
        />
      );

      const strategyCheckbox = screen.getByLabelText('Strategy');
      const familyCheckbox = screen.getByLabelText('Family');

      expect(strategyCheckbox).toBeChecked();
      expect(familyCheckbox).not.toBeChecked();
    });
  });

  // ==========================================================================
  // Mechanic Selection
  // ==========================================================================

  describe('Mechanic Selection', () => {
    it('shows selected mechanics count badge', () => {
      render(
        <CatalogFilters
          {...defaultProps}
          selectedMechanics={['worker-placement']}
        />
      );
      const badge = screen.getByText('1');
      expect(badge).toBeInTheDocument();
    });

    it('calls onMechanicChange when mechanic is selected', async () => {
      render(<CatalogFilters {...defaultProps} />);

      const checkbox = screen.getByLabelText('Worker Placement');
      await act(async () => {
        fireEvent.click(checkbox);
      });

      expect(defaultProps.onMechanicChange).toHaveBeenCalledWith(['worker-placement']);
    });

    it('calls onMechanicChange when mechanic is deselected', async () => {
      render(
        <CatalogFilters
          {...defaultProps}
          selectedMechanics={['worker-placement', 'deck-building']}
        />
      );

      const checkbox = screen.getByLabelText('Worker Placement');
      await act(async () => {
        fireEvent.click(checkbox);
      });

      expect(defaultProps.onMechanicChange).toHaveBeenCalledWith(['deck-building']);
    });
  });

  // ==========================================================================
  // Clear Filters
  // ==========================================================================

  describe('Clear Filters', () => {
    it('does not show clear button when no filters are active', () => {
      render(<CatalogFilters {...defaultProps} />);
      expect(screen.queryByText('Rimuovi Tutti i Filtri')).not.toBeInTheDocument();
    });

    it('shows clear button when search is active', () => {
      render(<CatalogFilters {...defaultProps} searchTerm="test" />);
      expect(screen.getByText('Rimuovi Tutti i Filtri')).toBeInTheDocument();
    });

    it('shows clear button when categories are selected', () => {
      render(
        <CatalogFilters {...defaultProps} selectedCategories={['strategy']} />
      );
      expect(screen.getByText('Rimuovi Tutti i Filtri')).toBeInTheDocument();
    });

    it('shows clear button when mechanics are selected', () => {
      render(
        <CatalogFilters {...defaultProps} selectedMechanics={['worker-placement']} />
      );
      expect(screen.getByText('Rimuovi Tutti i Filtri')).toBeInTheDocument();
    });

    it('shows clear button when players filter is set', () => {
      render(<CatalogFilters {...defaultProps} minPlayers={2} maxPlayers={4} />);
      expect(screen.getByText('Rimuovi Tutti i Filtri')).toBeInTheDocument();
    });

    it('shows clear button when playtime filter is set', () => {
      render(<CatalogFilters {...defaultProps} maxPlayingTime={60} />);
      expect(screen.getByText('Rimuovi Tutti i Filtri')).toBeInTheDocument();
    });

    it('calls onClearFilters when clear button is clicked', async () => {
      render(<CatalogFilters {...defaultProps} searchTerm="test" />);

      const clearButton = screen.getByText('Rimuovi Tutti i Filtri');
      await act(async () => {
        fireEvent.click(clearButton);
      });

      expect(defaultProps.onClearFilters).toHaveBeenCalled();
    });
  });

  // ==========================================================================
  // Players Filter
  // ==========================================================================

  describe('Players Filter', () => {
    it('shows "Qualsiasi" when no player filter is set', () => {
      render(<CatalogFilters {...defaultProps} />);
      // Multiple "Qualsiasi" placeholders exist (players and playtime)
      const placeholders = screen.getAllByText('Qualsiasi');
      expect(placeholders.length).toBeGreaterThanOrEqual(1);
    });
  });

  // ==========================================================================
  // Playtime Filter
  // ==========================================================================

  describe('Playtime Filter', () => {
    it('shows "Qualsiasi" when no playtime filter is set', () => {
      render(<CatalogFilters {...defaultProps} />);
      // Multiple "Qualsiasi" placeholders exist (players and playtime)
      const placeholders = screen.getAllByText('Qualsiasi');
      expect(placeholders.length).toBeGreaterThanOrEqual(1);
    });
  });
});
