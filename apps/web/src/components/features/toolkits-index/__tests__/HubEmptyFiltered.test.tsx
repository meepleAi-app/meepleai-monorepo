/**
 * HubEmptyFiltered - Unit tests (Issue #1480).
 *
 * Pure presentational empty-state shown when filters produce no results.
 * Maps from sp4-hub-toolkits.jsx:387-420 (function HubEmptyFiltered).
 * Grid-spanning (1/-1) dashed card with 🔎 icon + title + body + reset CTA.
 *
 * Test matrix (Crispin):
 *   T1. data-slot on root.
 *   T2. Renders title + body from labels.
 *   T3. Renders reset CTA with label.
 *   T4. Reset button fires onReset callback.
 *   T5. DS-15 dashed border + entity-toolkit accent.
 *   T6. className composition.
 *   T7. Passes axe a11y scan.
 */

import { fireEvent, render, screen } from '@testing-library/react';
import { axe } from 'jest-axe';
import { describe, expect, it, vi } from 'vitest';

import { HubEmptyFiltered } from '../HubEmptyFiltered';

const labels = {
  title: 'Nessun toolkit trovato',
  body: 'Nessun toolkit corrisponde ai filtri attuali. Prova a modificare la ricerca.',
  reset: 'Azzera filtri',
  resetAriaLabel: 'Azzera tutti i filtri di ricerca',
};

describe('HubEmptyFiltered (Issue #1480)', () => {
  // T1
  it('exposes a data-slot on the root', () => {
    const { container } = render(<HubEmptyFiltered labels={labels} onReset={() => {}} />);
    expect(
      container.querySelector('[data-slot="toolkits-index-empty-filtered"]')
    ).toBeInTheDocument();
  });

  // T2
  it('renders title and body from labels', () => {
    render(<HubEmptyFiltered labels={labels} onReset={() => {}} />);
    expect(screen.getByRole('heading', { name: labels.title })).toBeInTheDocument();
    expect(screen.getByText(labels.body)).toBeInTheDocument();
  });

  // T3
  it('renders the reset CTA with its label', () => {
    render(<HubEmptyFiltered labels={labels} onReset={() => {}} />);
    expect(screen.getByRole('button', { name: labels.resetAriaLabel })).toBeInTheDocument();
  });

  // T4
  it('fires onReset when the reset button is clicked', () => {
    const onReset = vi.fn();
    render(<HubEmptyFiltered labels={labels} onReset={onReset} />);
    fireEvent.click(screen.getByRole('button', { name: labels.resetAriaLabel }));
    expect(onReset).toHaveBeenCalledTimes(1);
  });

  // T5
  it('uses dashed border-strong + entity-toolkit accent', () => {
    const { container } = render(<HubEmptyFiltered labels={labels} onReset={() => {}} />);
    const root = container.querySelector('[data-slot="toolkits-index-empty-filtered"]');
    expect(root).toHaveClass('border-dashed');
    expect(root).toHaveClass('border-border-strong');
  });

  // T6
  it('composes custom className with base classes', () => {
    const { container } = render(
      <HubEmptyFiltered labels={labels} onReset={() => {}} className="extra" />
    );
    const root = container.querySelector('[data-slot="toolkits-index-empty-filtered"]');
    expect(root).toHaveClass('extra');
  });

  // T7
  it('passes axe a11y scan', async () => {
    const { container } = render(<HubEmptyFiltered labels={labels} onReset={() => {}} />);
    expect(await axe(container)).toHaveNoViolations();
  });
});
