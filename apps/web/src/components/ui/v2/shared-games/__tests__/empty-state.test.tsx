/**
 * EmptyState — variant rendering + ARIA semantics tests.
 *
 * Wave A.3b — V2 Migration `/shared-games` (Issue #596).
 *
 * Covered:
 *   - Each variant renders title / description / optional action button.
 *   - `api-error` uses role="alert" + aria-live="assertive";
 *     other variants use role="status" + aria-live="polite".
 *   - Action button is omitted when `onAction` or `actionLabel` is missing.
 *   - Action button click invokes `onAction`.
 */
import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { EmptyState } from '../empty-state';

describe('EmptyState', () => {
  describe('variant: empty-search', () => {
    it('renders title + description with role="status" / aria-live="polite"', () => {
      render(
        <EmptyState
          variant="empty-search"
          title="Nessun gioco trovato"
          description="Prova con un altro titolo."
        />
      );

      const root = screen.getByTestId('shared-games-empty-empty-search');
      expect(root).toHaveAttribute('role', 'status');
      expect(root).toHaveAttribute('aria-live', 'polite');
      expect(screen.getByText('Nessun gioco trovato')).toBeInTheDocument();
      expect(screen.getByText('Prova con un altro titolo.')).toBeInTheDocument();
    });

    it('does not render an action button when omitted', () => {
      render(<EmptyState variant="empty-search" title="Empty" />);

      expect(screen.queryByRole('button')).not.toBeInTheDocument();
    });
  });

  describe('variant: filtered-empty', () => {
    it('renders the action button and calls onAction on click', () => {
      const onAction = vi.fn();
      render(
        <EmptyState
          variant="filtered-empty"
          title="Filtri attivi"
          actionLabel="Cancella filtri"
          onAction={onAction}
        />
      );

      const button = screen.getByRole('button', { name: 'Cancella filtri' });
      fireEvent.click(button);

      expect(onAction).toHaveBeenCalledTimes(1);
    });
  });

  describe('variant: api-error', () => {
    it('uses role="alert" + aria-live="assertive" and supports retry action', () => {
      const onAction = vi.fn();
      render(
        <EmptyState
          variant="api-error"
          title="Errore"
          description="Riprova tra un istante."
          actionLabel="Riprova"
          onAction={onAction}
        />
      );

      const root = screen.getByTestId('shared-games-empty-api-error');
      expect(root).toHaveAttribute('role', 'alert');
      expect(root).toHaveAttribute('aria-live', 'assertive');

      fireEvent.click(screen.getByRole('button', { name: 'Riprova' }));
      expect(onAction).toHaveBeenCalled();
    });
  });

  it('omits the action button if actionLabel is given but onAction is missing', () => {
    render(<EmptyState variant="filtered-empty" title="Empty" actionLabel="Clear filters" />);

    expect(screen.queryByRole('button')).not.toBeInTheDocument();
  });
});
