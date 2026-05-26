/**
 * ErrorState - Unit tests (Issue #1480).
 *
 * Pure presentational error banner. Maps from sp4-hub-toolkits.jsx:422-452
 * (function ErrorState). Grid-spanning (1/-1) error card with icon + title +
 * subtitle + retry CTA. Used when the hub catalog fetch fails.
 *
 * Test matrix (Crispin):
 *   T1. data-slot on root.
 *   T2. Renders title + subtitle from labels.
 *   T3. Renders retry CTA with label.
 *   T4. Retry button fires onRetry callback.
 *   T5. DS-15 danger tokens on root.
 *   T6. className composition.
 *   T7. Passes axe a11y scan.
 */

import { fireEvent, render, screen } from '@testing-library/react';
import { axe } from 'jest-axe';
import { describe, expect, it, vi } from 'vitest';

import { ErrorState } from '../ErrorState';

const labels = {
  title: 'Impossibile caricare il catalogo',
  body: 'Si è verificato un errore di rete. Verifica la connessione e riprova.',
  retry: '↻ Riprova',
  retryAriaLabel: 'Riprova a caricare il catalogo',
};

describe('ErrorState (Issue #1480)', () => {
  // T1
  it('exposes a data-slot on the root', () => {
    const { container } = render(<ErrorState labels={labels} onRetry={() => {}} />);
    expect(container.querySelector('[data-slot="toolkits-index-error-state"]')).toBeInTheDocument();
  });

  // T2
  it('renders title and subtitle from labels', () => {
    render(<ErrorState labels={labels} onRetry={() => {}} />);
    expect(screen.getByRole('heading', { name: labels.title })).toBeInTheDocument();
    expect(screen.getByText(labels.body)).toBeInTheDocument();
  });

  // T3
  it('renders the retry CTA with its label', () => {
    render(<ErrorState labels={labels} onRetry={() => {}} />);
    expect(screen.getByRole('button', { name: labels.retryAriaLabel })).toBeInTheDocument();
  });

  // T4
  it('fires onRetry when the retry button is clicked', () => {
    const onRetry = vi.fn();
    render(<ErrorState labels={labels} onRetry={onRetry} />);
    fireEvent.click(screen.getByRole('button', { name: labels.retryAriaLabel }));
    expect(onRetry).toHaveBeenCalledTimes(1);
  });

  // T5
  it('uses DS-15 destructive tokens on the root', () => {
    const { container } = render(<ErrorState labels={labels} onRetry={() => {}} />);
    const root = container.querySelector('[data-slot="toolkits-index-error-state"]');
    expect(root).toHaveClass('border-destructive/30');
  });

  // T6
  it('composes custom className with base classes', () => {
    const { container } = render(
      <ErrorState labels={labels} onRetry={() => {}} className="extra" />
    );
    const root = container.querySelector('[data-slot="toolkits-index-error-state"]');
    expect(root).toHaveClass('extra');
  });

  // T7
  it('passes axe a11y scan', async () => {
    const { container } = render(<ErrorState labels={labels} onRetry={() => {}} />);
    expect(await axe(container)).toHaveNoViolations();
  });
});
