/**
 * Wave B.2 (Issue #634) — EmptyAgents v2 component tests.
 *
 * Pure component (mirror Wave B.1 GamesEmptyState): labels via prop, no
 * `useTranslation`.
 *
 * Contract under test (spec §3.2 + plan §4.5):
 *   - 4 discriminated `kind` states: 'empty' | 'filtered-empty' | 'error' | 'loading'
 *   - Each non-loading kind renders title/subtitle/cta from `labels`
 *   - 'empty' CTA → onCreateAgent()
 *   - 'filtered-empty' CTA → onClearFilters()
 *   - 'error' CTA → onRetry()
 *   - 'loading' renders 6 skeleton cards desktop, 3 mobile (compact=true)
 *   - Root carries data-slot="agents-empty-state" + data-kind for E2E scoping
 *   - 'error' surfaces via role="alert" (a11y), others via role="status"
 */

import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { EmptyAgents, type EmptyAgentsLabels } from '../EmptyAgents';

const labels: EmptyAgentsLabels = {
  empty: {
    title: 'Nessun agente ancora',
    subtitle: 'Crea il tuo primo agente AI per iniziare a giocare con la knowledge base.',
    cta: 'Crea il tuo primo agente',
  },
  filteredEmpty: {
    title: 'Nessun agente trovato',
    subtitle: 'Prova a modificare i filtri o la ricerca.',
    cta: 'Azzera filtri',
  },
  error: {
    title: 'Impossibile caricare gli agenti',
    subtitle: 'Si è verificato un errore. Riprova tra qualche istante.',
    cta: 'Riprova',
  },
};

describe('EmptyAgents (Wave B.2)', () => {
  it('renders kind="empty" with title, subtitle, and CTA from labels', () => {
    render(<EmptyAgents kind="empty" labels={labels} />);
    expect(screen.getByText('Nessun agente ancora')).toBeInTheDocument();
    expect(
      screen.getByText('Crea il tuo primo agente AI per iniziare a giocare con la knowledge base.')
    ).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Crea il tuo primo agente' })).toBeInTheDocument();
  });

  it('fires onCreateAgent when the empty-state CTA is clicked', () => {
    const onCreateAgent = vi.fn();
    render(<EmptyAgents kind="empty" labels={labels} onCreateAgent={onCreateAgent} />);
    fireEvent.click(screen.getByRole('button', { name: 'Crea il tuo primo agente' }));
    expect(onCreateAgent).toHaveBeenCalledTimes(1);
  });

  it('renders kind="filtered-empty" with its own title, subtitle, CTA', () => {
    render(<EmptyAgents kind="filtered-empty" labels={labels} onClearFilters={vi.fn()} />);
    expect(screen.getByText('Nessun agente trovato')).toBeInTheDocument();
    expect(screen.getByText('Prova a modificare i filtri o la ricerca.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Azzera filtri' })).toBeInTheDocument();
  });

  it('fires onClearFilters when the filtered-empty CTA is clicked', () => {
    const onClearFilters = vi.fn();
    render(<EmptyAgents kind="filtered-empty" labels={labels} onClearFilters={onClearFilters} />);
    fireEvent.click(screen.getByRole('button', { name: 'Azzera filtri' }));
    expect(onClearFilters).toHaveBeenCalledTimes(1);
  });

  it('renders kind="error" with its own title, subtitle, CTA', () => {
    render(<EmptyAgents kind="error" labels={labels} onRetry={vi.fn()} />);
    expect(screen.getByText('Impossibile caricare gli agenti')).toBeInTheDocument();
    expect(
      screen.getByText('Si è verificato un errore. Riprova tra qualche istante.')
    ).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Riprova' })).toBeInTheDocument();
  });

  it('fires onRetry when the error CTA is clicked', () => {
    const onRetry = vi.fn();
    render(<EmptyAgents kind="error" labels={labels} onRetry={onRetry} />);
    fireEvent.click(screen.getByRole('button', { name: 'Riprova' }));
    expect(onRetry).toHaveBeenCalledTimes(1);
  });

  it('renders 6 skeleton cards when kind="loading" (desktop)', () => {
    const { container } = render(<EmptyAgents kind="loading" labels={labels} />);
    const skeletons = container.querySelectorAll('[data-slot="agents-empty-skeleton"]');
    expect(skeletons).toHaveLength(6);
  });

  it('renders 3 skeleton cards when kind="loading" + compact=true (mobile)', () => {
    const { container } = render(<EmptyAgents kind="loading" labels={labels} compact />);
    const skeletons = container.querySelectorAll('[data-slot="agents-empty-skeleton"]');
    expect(skeletons).toHaveLength(3);
  });

  it('exposes data-slot="agents-empty-state" + data-kind on root for spec scoping', () => {
    const { container, rerender } = render(<EmptyAgents kind="empty" labels={labels} />);
    let root = container.querySelector('[data-slot="agents-empty-state"]');
    expect(root).not.toBeNull();
    expect(root?.getAttribute('data-kind')).toBe('empty');

    rerender(<EmptyAgents kind="filtered-empty" labels={labels} onClearFilters={vi.fn()} />);
    root = container.querySelector('[data-slot="agents-empty-state"]');
    expect(root?.getAttribute('data-kind')).toBe('filtered-empty');

    rerender(<EmptyAgents kind="error" labels={labels} onRetry={vi.fn()} />);
    root = container.querySelector('[data-slot="agents-empty-state"]');
    expect(root?.getAttribute('data-kind')).toBe('error');

    rerender(<EmptyAgents kind="loading" labels={labels} />);
    root = container.querySelector('[data-slot="agents-empty-state"]');
    expect(root?.getAttribute('data-kind')).toBe('loading');
  });

  it('uses role="alert" for kind="error" and role="status" for non-error states', () => {
    const { container, rerender } = render(<EmptyAgents kind="empty" labels={labels} />);
    let root = container.querySelector('[data-slot="agents-empty-state"]');
    expect(root?.getAttribute('role')).toBe('status');

    rerender(<EmptyAgents kind="filtered-empty" labels={labels} onClearFilters={vi.fn()} />);
    root = container.querySelector('[data-slot="agents-empty-state"]');
    expect(root?.getAttribute('role')).toBe('status');

    rerender(<EmptyAgents kind="error" labels={labels} onRetry={vi.fn()} />);
    root = container.querySelector('[data-slot="agents-empty-state"]');
    expect(root?.getAttribute('role')).toBe('alert');
  });

  it('marks loading container with aria-busy and aria-live for screen readers', () => {
    const { container } = render(<EmptyAgents kind="loading" labels={labels} />);
    const root = container.querySelector('[data-slot="agents-empty-state"]');
    expect(root?.getAttribute('aria-busy')).toBe('true');
    expect(root?.getAttribute('aria-live')).toBe('polite');
  });
});
