/**
 * Wave B.1 (Issue #633) — GamesEmptyState v2 component tests.
 *
 * Pure component (mirror Wave A.4): labels via prop, no `useTranslation`.
 *
 * Contract under test (spec §3.2 + plan §4.4):
 *   - 4 discriminated `kind` states: 'empty' | 'filtered-empty' | 'error' | 'loading'
 *   - Each kind renders its own title/subtitle/cta from `labels` (except loading)
 *   - 'empty' CTA → onAddGame()
 *   - 'filtered-empty' CTA → onClearFilters()
 *   - 'error' CTA → onRetry()
 *   - 'loading' renders 8 skeleton cards desktop, 4 mobile (compact=true)
 *   - Root carries data-slot="games-empty-state" + data-kind for spec scoping
 */

import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { GamesEmptyState, type GamesEmptyStateLabels } from '../GamesEmptyState';

const labels: GamesEmptyStateLabels = {
  empty: {
    title: 'La tua libreria è vuota',
    subtitle: 'Aggiungi il tuo primo gioco per iniziare a costruire la collezione.',
    cta: 'Aggiungi gioco',
  },
  filteredEmpty: {
    title: 'Nessun risultato',
    subtitle: 'Prova a modificare i filtri o la ricerca.',
    cta: 'Cancella filtri',
  },
  error: {
    title: 'Impossibile caricare la libreria',
    subtitle: 'Si è verificato un errore. Riprova tra qualche istante.',
    cta: 'Riprova',
  },
};

describe('GamesEmptyState (Wave B.1)', () => {
  it('renders kind="empty" with title, subtitle, and CTA from labels', () => {
    render(<GamesEmptyState kind="empty" labels={labels} />);
    expect(screen.getByText('La tua libreria è vuota')).toBeInTheDocument();
    expect(
      screen.getByText('Aggiungi il tuo primo gioco per iniziare a costruire la collezione.')
    ).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Aggiungi gioco' })).toBeInTheDocument();
  });

  it('fires onAddGame when the empty-state CTA is clicked', () => {
    const onAddGame = vi.fn();
    render(<GamesEmptyState kind="empty" labels={labels} onAddGame={onAddGame} />);
    fireEvent.click(screen.getByRole('button', { name: 'Aggiungi gioco' }));
    expect(onAddGame).toHaveBeenCalledTimes(1);
  });

  it('renders kind="filtered-empty" with its own title, subtitle, CTA', () => {
    render(<GamesEmptyState kind="filtered-empty" labels={labels} onClearFilters={vi.fn()} />);
    expect(screen.getByText('Nessun risultato')).toBeInTheDocument();
    expect(screen.getByText('Prova a modificare i filtri o la ricerca.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Cancella filtri' })).toBeInTheDocument();
  });

  it('fires onClearFilters when the filtered-empty CTA is clicked', () => {
    const onClearFilters = vi.fn();
    render(
      <GamesEmptyState kind="filtered-empty" labels={labels} onClearFilters={onClearFilters} />
    );
    fireEvent.click(screen.getByRole('button', { name: 'Cancella filtri' }));
    expect(onClearFilters).toHaveBeenCalledTimes(1);
  });

  it('renders kind="error" with its own title, subtitle, CTA', () => {
    render(<GamesEmptyState kind="error" labels={labels} onRetry={vi.fn()} />);
    expect(screen.getByText('Impossibile caricare la libreria')).toBeInTheDocument();
    expect(
      screen.getByText('Si è verificato un errore. Riprova tra qualche istante.')
    ).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Riprova' })).toBeInTheDocument();
  });

  it('fires onRetry when the error CTA is clicked', () => {
    const onRetry = vi.fn();
    render(<GamesEmptyState kind="error" labels={labels} onRetry={onRetry} />);
    fireEvent.click(screen.getByRole('button', { name: 'Riprova' }));
    expect(onRetry).toHaveBeenCalledTimes(1);
  });

  it('renders 8 skeleton cards when kind="loading" (desktop)', () => {
    const { container } = render(<GamesEmptyState kind="loading" labels={labels} />);
    const skeletons = container.querySelectorAll('[data-slot="games-empty-skeleton"]');
    expect(skeletons).toHaveLength(8);
  });

  it('renders 4 skeleton cards when kind="loading" + compact=true (mobile)', () => {
    const { container } = render(<GamesEmptyState kind="loading" labels={labels} compact />);
    const skeletons = container.querySelectorAll('[data-slot="games-empty-skeleton"]');
    expect(skeletons).toHaveLength(4);
  });

  it('exposes data-slot="games-empty-state" + data-kind on root for spec scoping', () => {
    const { container, rerender } = render(<GamesEmptyState kind="empty" labels={labels} />);
    let root = container.querySelector('[data-slot="games-empty-state"]');
    expect(root).not.toBeNull();
    expect(root?.getAttribute('data-kind')).toBe('empty');

    rerender(<GamesEmptyState kind="filtered-empty" labels={labels} onClearFilters={vi.fn()} />);
    root = container.querySelector('[data-slot="games-empty-state"]');
    expect(root?.getAttribute('data-kind')).toBe('filtered-empty');

    rerender(<GamesEmptyState kind="error" labels={labels} onRetry={vi.fn()} />);
    root = container.querySelector('[data-slot="games-empty-state"]');
    expect(root?.getAttribute('data-kind')).toBe('error');

    rerender(<GamesEmptyState kind="loading" labels={labels} />);
    root = container.querySelector('[data-slot="games-empty-state"]');
    expect(root?.getAttribute('data-kind')).toBe('loading');
  });
});
