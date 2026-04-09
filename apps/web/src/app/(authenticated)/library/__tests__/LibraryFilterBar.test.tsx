import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';

import { LibraryFilterBar } from '../sections/LibraryFilterBar';

describe('LibraryFilterBar', () => {
  const defaultProps = {
    activeFilter: 'all' as const,
    onFilterChange: () => {},
    activeView: 'carousels' as const,
    onViewChange: () => {},
    sortLabel: 'Ultimo giocato',
    onSortClick: () => {},
  };

  it('renders filter chips', () => {
    render(<LibraryFilterBar {...defaultProps} />);
    expect(screen.getByText('Tutti')).toBeInTheDocument();
    expect(screen.getByText('Strategici')).toBeInTheDocument();
    expect(screen.getByText('Familiari')).toBeInTheDocument();
    expect(screen.getByText('Cooperativi')).toBeInTheDocument();
    expect(screen.getByText('Party')).toBeInTheDocument();
  });

  it('marks the active filter', () => {
    render(<LibraryFilterBar {...defaultProps} activeFilter="strategici" />);
    const strategici = screen.getByRole('button', { name: 'Strategici' });
    expect(strategici).toHaveAttribute('aria-pressed', 'true');
  });

  it('calls onFilterChange when a chip is clicked', async () => {
    const onFilterChange = vi.fn();
    const user = userEvent.setup();
    render(<LibraryFilterBar {...defaultProps} onFilterChange={onFilterChange} />);
    await user.click(screen.getByRole('button', { name: 'Familiari' }));
    expect(onFilterChange).toHaveBeenCalledWith('familiari');
  });

  it('renders sort button with label', () => {
    render(<LibraryFilterBar {...defaultProps} sortLabel="Rating" />);
    expect(screen.getByRole('button', { name: /Rating/i })).toBeInTheDocument();
  });

  it('renders view toggle buttons (carousels/grid/list)', () => {
    render(<LibraryFilterBar {...defaultProps} />);
    expect(screen.getByRole('button', { name: /carousels/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /grid/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /list/i })).toBeInTheDocument();
  });

  it('calls onViewChange when a view button is clicked', async () => {
    const onViewChange = vi.fn();
    const user = userEvent.setup();
    render(<LibraryFilterBar {...defaultProps} onViewChange={onViewChange} />);
    await user.click(screen.getByRole('button', { name: /list/i }));
    expect(onViewChange).toHaveBeenCalledWith('list');
  });
});
