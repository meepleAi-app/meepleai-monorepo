/**
 * Wave B.3 (Issue #574) — EmptyLibrary v2 component tests.
 *
 * Discriminated 4-kind UI mirror of Wave B.2 EmptyAgents:
 *   - 'loading' → 8 skeleton desktop / 4 mobile
 *   - 'empty'   → 📚 + onAddGame CTA
 *   - 'filtered-empty' → 🔎 + onClearFilters CTA
 *   - 'error'   → ⚠️ + onRetry CTA
 *
 * Pure component (labels prop, no useTranslation internal).
 */

import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { EmptyLibrary, type EmptyLibraryLabels } from '../EmptyLibrary';

const baseLabels: EmptyLibraryLabels = {
  empty: {
    title: 'La tua libreria è vuota',
    subtitle: 'Aggiungi il primo gioco per iniziare.',
    cta: 'Aggiungi un gioco',
  },
  filteredEmpty: {
    title: 'Nessun risultato',
    subtitle: 'Nessun elemento corrisponde ai filtri attuali.',
    cta: 'Azzera filtri',
  },
  error: {
    title: 'Impossibile caricare la libreria',
    subtitle: 'Si è verificato un errore di rete.',
    cta: 'Riprova',
  },
};

describe('EmptyLibrary (Wave B.3)', () => {
  describe('kind="loading"', () => {
    it('renders 8 skeleton cards on desktop (compact=false)', () => {
      const { container } = render(<EmptyLibrary kind="loading" labels={baseLabels} />);
      const skeletons = container.querySelectorAll('[data-slot="library-empty-skeleton"]');
      expect(skeletons).toHaveLength(8);
    });

    it('renders 4 skeleton cards on mobile (compact=true)', () => {
      const { container } = render(<EmptyLibrary kind="loading" labels={baseLabels} compact />);
      const skeletons = container.querySelectorAll('[data-slot="library-empty-skeleton"]');
      expect(skeletons).toHaveLength(4);
    });

    it('exposes aria-busy and data-kind="loading" on root', () => {
      const { container } = render(<EmptyLibrary kind="loading" labels={baseLabels} />);
      const root = container.querySelector('[data-slot="library-empty-state"]');
      expect(root).toHaveAttribute('aria-busy', 'true');
      expect(root).toHaveAttribute('data-kind', 'loading');
    });
  });

  describe('kind="empty"', () => {
    it('renders empty title + subtitle + CTA from labels.empty', () => {
      render(<EmptyLibrary kind="empty" labels={baseLabels} />);
      expect(screen.getByText('La tua libreria è vuota')).toBeInTheDocument();
      expect(screen.getByText('Aggiungi il primo gioco per iniziare.')).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Aggiungi un gioco' })).toBeInTheDocument();
    });

    it('calls onAddGame on CTA click', () => {
      const onAddGame = vi.fn();
      render(<EmptyLibrary kind="empty" labels={baseLabels} onAddGame={onAddGame} />);
      fireEvent.click(screen.getByRole('button', { name: 'Aggiungi un gioco' }));
      expect(onAddGame).toHaveBeenCalledTimes(1);
    });

    it('uses role="status" for non-error empty state', () => {
      const { container } = render(<EmptyLibrary kind="empty" labels={baseLabels} />);
      const root = container.querySelector('[data-slot="library-empty-state"]');
      expect(root).toHaveAttribute('role', 'status');
    });
  });

  describe('kind="filtered-empty"', () => {
    it('renders filtered-empty title + subtitle + CTA from labels.filteredEmpty', () => {
      render(<EmptyLibrary kind="filtered-empty" labels={baseLabels} />);
      expect(screen.getByText('Nessun risultato')).toBeInTheDocument();
      expect(
        screen.getByText('Nessun elemento corrisponde ai filtri attuali.')
      ).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Azzera filtri' })).toBeInTheDocument();
    });

    it('calls onClearFilters on CTA click', () => {
      const onClearFilters = vi.fn();
      render(
        <EmptyLibrary kind="filtered-empty" labels={baseLabels} onClearFilters={onClearFilters} />
      );
      fireEvent.click(screen.getByRole('button', { name: 'Azzera filtri' }));
      expect(onClearFilters).toHaveBeenCalledTimes(1);
    });
  });

  describe('kind="error"', () => {
    it('renders error title + subtitle + CTA from labels.error', () => {
      render(<EmptyLibrary kind="error" labels={baseLabels} />);
      expect(screen.getByText('Impossibile caricare la libreria')).toBeInTheDocument();
      expect(screen.getByText('Si è verificato un errore di rete.')).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Riprova' })).toBeInTheDocument();
    });

    it('calls onRetry on CTA click', () => {
      const onRetry = vi.fn();
      render(<EmptyLibrary kind="error" labels={baseLabels} onRetry={onRetry} />);
      fireEvent.click(screen.getByRole('button', { name: 'Riprova' }));
      expect(onRetry).toHaveBeenCalledTimes(1);
    });

    it('uses role="alert" for error surface', () => {
      const { container } = render(<EmptyLibrary kind="error" labels={baseLabels} />);
      const root = container.querySelector('[data-slot="library-empty-state"]');
      expect(root).toHaveAttribute('role', 'alert');
    });
  });

  it('exposes data-slot="library-empty-state" on root for spec scoping', () => {
    const { container } = render(<EmptyLibrary kind="empty" labels={baseLabels} />);
    expect(container.querySelector('[data-slot="library-empty-state"]')).not.toBeNull();
  });
});
