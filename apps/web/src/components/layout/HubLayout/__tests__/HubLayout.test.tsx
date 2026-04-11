/**
 * Tests for HubLayout component.
 */
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { HubLayout } from '../HubLayout';
import type { FilterChip } from '../HubLayout';

const CHIPS: FilterChip[] = [
  { id: 'all', label: 'Tutti', count: 10 },
  { id: 'active', label: 'Attivi', count: 3 },
  { id: 'archived', label: 'Archiviati' },
];

describe('HubLayout', () => {
  it('renders children without any optional props', () => {
    render(
      <HubLayout>
        <div data-testid="child-content">Content</div>
      </HubLayout>
    );
    expect(screen.getByTestId('child-content')).toBeInTheDocument();
  });

  it('renders the search bar with default placeholder', () => {
    render(<HubLayout>child</HubLayout>);
    expect(screen.getByRole('searchbox', { name: /cerca/i })).toBeInTheDocument();
  });

  it('renders the search bar with a custom placeholder', () => {
    render(<HubLayout searchPlaceholder="Cerca giochi...">child</HubLayout>);
    expect(screen.getByRole('searchbox', { name: /cerca giochi/i })).toBeInTheDocument();
  });

  it('calls onSearchChange with the typed value', () => {
    const onSearchChange = vi.fn();
    render(
      <HubLayout searchValue="" onSearchChange={onSearchChange}>
        child
      </HubLayout>
    );
    const input = screen.getByRole('searchbox');
    fireEvent.change(input, { target: { value: 'puerto rico' } });
    expect(onSearchChange).toHaveBeenCalledWith('puerto rico');
  });

  it('reflects the controlled searchValue prop', () => {
    render(<HubLayout searchValue="catan">child</HubLayout>);
    const input = screen.getByRole('searchbox') as HTMLInputElement;
    expect(input.value).toBe('catan');
  });

  describe('FilterChips', () => {
    it('renders all chips when filterChips prop is provided', () => {
      render(
        <HubLayout filterChips={CHIPS} activeFilterId="all">
          child
        </HubLayout>
      );
      expect(screen.getByRole('button', { name: /tutti/i })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /attivi/i })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /archiviati/i })).toBeInTheDocument();
    });

    it('does not render filter chips area when filterChips is empty', () => {
      render(
        <HubLayout filterChips={[]} activeFilterId="all">
          child
        </HubLayout>
      );
      expect(screen.queryByRole('list', { name: /filtri/i })).not.toBeInTheDocument();
    });

    it('calls onFilterChange with the chip id when clicked', () => {
      const onFilterChange = vi.fn();
      render(
        <HubLayout filterChips={CHIPS} activeFilterId="all" onFilterChange={onFilterChange}>
          child
        </HubLayout>
      );
      fireEvent.click(screen.getByRole('button', { name: /attivi/i }));
      expect(onFilterChange).toHaveBeenCalledWith('active');
    });

    it('marks the active chip with aria-pressed=true', () => {
      render(
        <HubLayout filterChips={CHIPS} activeFilterId="active">
          child
        </HubLayout>
      );
      const activeChip = screen.getByRole('button', { name: /attivi/i });
      const inactiveChip = screen.getByRole('button', { name: /tutti/i });
      expect(activeChip).toHaveAttribute('aria-pressed', 'true');
      expect(inactiveChip).toHaveAttribute('aria-pressed', 'false');
    });

    it('renders the count badge when count is defined', () => {
      render(
        <HubLayout filterChips={CHIPS} activeFilterId="all">
          child
        </HubLayout>
      );
      // "Tutti" chip has count=10; "Archiviati" has no count
      expect(screen.getByText('10')).toBeInTheDocument();
      expect(screen.getByText('3')).toBeInTheDocument();
    });
  });

  describe('ViewModeToggle', () => {
    it('does NOT render view mode buttons when showViewToggle is false (default)', () => {
      render(<HubLayout>child</HubLayout>);
      expect(screen.queryByRole('button', { name: /vista griglia/i })).not.toBeInTheDocument();
    });

    it('does NOT render view mode buttons when showViewToggle={false}', () => {
      render(<HubLayout showViewToggle={false}>child</HubLayout>);
      expect(screen.queryByRole('button', { name: /vista griglia/i })).not.toBeInTheDocument();
    });

    it('renders grid, list, and carousel buttons when showViewToggle={true}', () => {
      render(
        <HubLayout showViewToggle viewMode="grid">
          child
        </HubLayout>
      );
      expect(screen.getByRole('button', { name: /vista griglia/i })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /vista lista/i })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /vista carosello/i })).toBeInTheDocument();
    });

    it('calls onViewModeChange with the correct mode when a view button is clicked', () => {
      const onViewModeChange = vi.fn();
      render(
        <HubLayout showViewToggle viewMode="grid" onViewModeChange={onViewModeChange}>
          child
        </HubLayout>
      );
      fireEvent.click(screen.getByRole('button', { name: /vista lista/i }));
      expect(onViewModeChange).toHaveBeenCalledWith('list');

      fireEvent.click(screen.getByRole('button', { name: /vista carosello/i }));
      expect(onViewModeChange).toHaveBeenCalledWith('carousel');
    });

    it('marks the active view mode button with aria-pressed=true', () => {
      render(
        <HubLayout showViewToggle viewMode="list">
          child
        </HubLayout>
      );
      const listBtn = screen.getByRole('button', { name: /vista lista/i });
      const gridBtn = screen.getByRole('button', { name: /vista griglia/i });
      expect(listBtn).toHaveAttribute('aria-pressed', 'true');
      expect(gridBtn).toHaveAttribute('aria-pressed', 'false');
    });
  });

  describe('topActions', () => {
    it('renders topActions slot when provided', () => {
      render(<HubLayout topActions={<button>Nuova sessione</button>}>child</HubLayout>);
      expect(screen.getByRole('button', { name: /nuova sessione/i })).toBeInTheDocument();
    });

    it('does not render topActions area when not provided', () => {
      render(<HubLayout>child</HubLayout>);
      expect(screen.queryByRole('button', { name: /nuova sessione/i })).not.toBeInTheDocument();
    });
  });
});
