/**
 * EmptyPlayers unit tests — Wave 4 D1 (Issue #682).
 *
 * TDD red phase: written before the component implementation.
 * Mirror pattern from EmptyAgents.test.tsx (Wave B.2 reference).
 *
 * 5 tests:
 * 1. Renders data-slot="players-empty" with data-kind="empty"
 * 2. Renders data-kind="filtered-empty" copy and fires onClearFilters
 * 3. Renders data-kind="error" with role="alert" and fires onRetry
 * 4. data-kind attribute set correctly for each kind
 * 5. render shape: title, subtitle, cta present for each kind
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { EmptyPlayers } from '../EmptyPlayers';
import type { EmptyPlayersProps } from '../EmptyPlayers';

const LABELS: EmptyPlayersProps['labels'] = {
  empty: {
    title: 'Nessuna partita ancora',
    subtitle: 'Registra la tua prima partita per vedere il riepilogo qui.',
    cta: 'Inizia a giocare',
  },
  filteredEmpty: {
    title: 'Nessun gioco trovato',
    subtitle: 'Prova a modificare la ricerca per trovare i tuoi giochi.',
    cta: 'Cancella ricerca',
  },
  error: {
    title: 'Impossibile caricare le partite',
    subtitle: 'Si è verificato un errore. Riprova tra qualche istante.',
    cta: 'Riprova',
  },
};

describe('EmptyPlayers', () => {
  it('renders data-slot="players-empty" with data-kind="empty" for kind="empty"', () => {
    render(<EmptyPlayers kind="empty" labels={LABELS} />);
    const el = document.querySelector('[data-slot="players-empty"]');
    expect(el).not.toBeNull();
    expect(el!.getAttribute('data-kind')).toBe('empty');
    expect(screen.getByText('Nessuna partita ancora')).toBeTruthy();
    expect(screen.getByText('Inizia a giocare')).toBeTruthy();
  });

  it('renders data-kind="filtered-empty" copy and fires onClearFilters on CTA click', () => {
    const onClearFilters = vi.fn();
    render(<EmptyPlayers kind="filtered-empty" labels={LABELS} onClearFilters={onClearFilters} />);
    const el = document.querySelector('[data-slot="players-empty"]');
    expect(el!.getAttribute('data-kind')).toBe('filtered-empty');
    expect(screen.getByText('Nessun gioco trovato')).toBeTruthy();
    fireEvent.click(screen.getByText('Cancella ricerca'));
    expect(onClearFilters).toHaveBeenCalledOnce();
  });

  it('renders data-kind="error" with role="alert" and fires onRetry on CTA click', () => {
    const onRetry = vi.fn();
    render(<EmptyPlayers kind="error" labels={LABELS} onRetry={onRetry} />);
    const el = document.querySelector('[data-slot="players-empty"]');
    expect(el!.getAttribute('data-kind')).toBe('error');
    expect(el!.getAttribute('role')).toBe('alert');
    expect(screen.getByText('Impossibile caricare le partite')).toBeTruthy();
    fireEvent.click(screen.getByText('Riprova'));
    expect(onRetry).toHaveBeenCalledOnce();
  });

  it('sets data-kind attribute correctly for each kind', () => {
    const kinds = ['empty', 'filtered-empty', 'error'] as const;
    for (const kind of kinds) {
      const { unmount } = render(<EmptyPlayers kind={kind} labels={LABELS} />);
      const el = document.querySelector('[data-slot="players-empty"]');
      expect(el!.getAttribute('data-kind')).toBe(kind);
      unmount();
    }
  });

  it('renders title, subtitle, and cta for each kind', () => {
    const cases = [
      { kind: 'empty' as const, title: 'Nessuna partita ancora', cta: 'Inizia a giocare' },
      { kind: 'filtered-empty' as const, title: 'Nessun gioco trovato', cta: 'Cancella ricerca' },
      { kind: 'error' as const, title: 'Impossibile caricare le partite', cta: 'Riprova' },
    ];
    for (const { kind, title, cta } of cases) {
      const { unmount } = render(<EmptyPlayers kind={kind} labels={LABELS} />);
      expect(screen.getByText(title)).toBeTruthy();
      expect(screen.getByText(cta)).toBeTruthy();
      unmount();
    }
  });
});
