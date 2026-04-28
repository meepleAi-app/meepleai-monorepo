import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { EmptyState } from './empty-state';

describe('EmptyState (v2)', () => {
  const defaults = {
    title: 'Nessun gioco trovato',
    description: 'Prova a cambiare la ricerca.',
    resetLabel: '↺ Reset filtri',
    onReset: vi.fn(),
  };

  it('renders title and description for empty-search kind', () => {
    render(<EmptyState kind="empty-search" {...defaults} />);
    expect(screen.getByText('Nessun gioco trovato')).toBeInTheDocument();
    expect(screen.getByText('Prova a cambiare la ricerca.')).toBeInTheDocument();
  });

  it('uses 🔍 icon for empty-search kind', () => {
    render(<EmptyState kind="empty-search" {...defaults} />);
    expect(screen.getByText('🔍')).toBeInTheDocument();
  });

  it('uses ⚠️ icon for filtered-empty kind', () => {
    render(<EmptyState kind="filtered-empty" {...defaults} />);
    expect(screen.getByText('⚠️')).toBeInTheDocument();
  });

  it('emits data-kind attribute for visual-test selection', () => {
    const { container } = render(<EmptyState kind="filtered-empty" {...defaults} />);
    expect(container.querySelector('[data-slot="shared-games-empty-state"]')).toHaveAttribute(
      'data-kind',
      'filtered-empty'
    );
  });

  it('exposes role=status with aria-live=polite', () => {
    render(<EmptyState kind="empty-search" {...defaults} />);
    const region = screen.getByRole('status');
    expect(region).toHaveAttribute('aria-live', 'polite');
  });

  it('omits description paragraph when description is undefined', () => {
    render(
      <EmptyState
        kind="empty-search"
        title={defaults.title}
        resetLabel={defaults.resetLabel}
        onReset={defaults.onReset}
      />
    );
    expect(screen.queryByText('Prova a cambiare la ricerca.')).not.toBeInTheDocument();
  });

  it('calls onReset when reset button clicked', () => {
    const onReset = vi.fn();
    render(<EmptyState kind="empty-search" {...defaults} onReset={onReset} />);
    fireEvent.click(screen.getByRole('button', { name: '↺ Reset filtri' }));
    expect(onReset).toHaveBeenCalledTimes(1);
  });

  it('icon is aria-hidden decoration', () => {
    render(<EmptyState kind="empty-search" {...defaults} />);
    expect(screen.getByText('🔍')).toHaveAttribute('aria-hidden', 'true');
  });
});
