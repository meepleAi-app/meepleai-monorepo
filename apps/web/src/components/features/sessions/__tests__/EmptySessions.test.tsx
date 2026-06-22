/**
 * EmptySessions unit tests — Wave D.1 (Issue #735).
 *
 * 10 tests:
 * 1. empty kind: renders data-slot="sessions-empty-empty", role="status"
 * 2. empty kind: renders empty title, description, CTA
 * 3. empty kind: CTA fires onPrimaryAction
 * 4. filtered-empty kind: renders data-slot="sessions-empty-filtered-empty"
 * 5. filtered-empty kind: renders filteredEmpty title + CTA
 * 6. error kind: renders data-slot="sessions-empty-error" with role="alert"
 * 7. error kind: renders error title + CTA, fires onPrimaryAction
 * 8. loading kind: renders data-slot="sessions-empty-loading" with aria-busy
 * 9. loading kind: renders 8 skeleton placeholders
 * 10. data-slot selector matches kind for all 4 kinds
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { EmptySessions } from '../EmptySessions';
import type { EmptySessionsProps } from '../EmptySessions';

const LABELS: EmptySessionsProps['labels'] = {
  emptyTitle: 'Nessuna partita ancora',
  emptyDescription: 'Registra la tua prima partita.',
  emptyCta: 'Inizia la prima sessione',
  filteredEmptyTitle: 'Nessun risultato',
  filteredEmptyDescription: 'Prova a modificare il filtro.',
  filteredEmptyCta: 'Rimuovi filtro',
  loadingAriaLabel: 'Caricamento sessioni...',
  errorTitle: 'Impossibile caricare',
  errorDescription: 'Si è verificato un errore.',
  errorCta: 'Riprova',
};

describe('EmptySessions', () => {
  it('empty kind renders data-slot="sessions-empty-empty" with role="status"', () => {
    render(<EmptySessions kind="empty" labels={LABELS} />);
    const el = document.querySelector('[data-slot="sessions-empty-empty"]');
    expect(el).not.toBeNull();
    expect(el!.getAttribute('role')).toBe('status');
  });

  it('empty kind renders title, description, and CTA when onPrimaryAction provided', () => {
    render(<EmptySessions kind="empty" labels={LABELS} onPrimaryAction={vi.fn()} />);
    expect(screen.getByText('Nessuna partita ancora')).toBeTruthy();
    expect(screen.getByText('Registra la tua prima partita.')).toBeTruthy();
    expect(screen.getByText('Inizia la prima sessione')).toBeTruthy();
  });

  it('empty kind CTA fires onPrimaryAction on click', () => {
    const onPrimaryAction = vi.fn();
    render(<EmptySessions kind="empty" labels={LABELS} onPrimaryAction={onPrimaryAction} />);
    fireEvent.click(screen.getByText('Inizia la prima sessione'));
    expect(onPrimaryAction).toHaveBeenCalledOnce();
  });

  it('filtered-empty kind renders data-slot="sessions-empty-filtered-empty"', () => {
    render(<EmptySessions kind="filtered-empty" labels={LABELS} />);
    const el = document.querySelector('[data-slot="sessions-empty-filtered-empty"]');
    expect(el).not.toBeNull();
  });

  it('filtered-empty kind renders title and CTA text when onPrimaryAction provided', () => {
    render(<EmptySessions kind="filtered-empty" labels={LABELS} onPrimaryAction={vi.fn()} />);
    expect(screen.getByText('Nessun risultato')).toBeTruthy();
    expect(screen.getByText('Rimuovi filtro')).toBeTruthy();
  });

  it('CTA button is hidden when onPrimaryAction is undefined (avoids no-op click)', () => {
    render(<EmptySessions kind="empty" labels={LABELS} />);
    expect(screen.queryByText('Inizia la prima sessione')).toBeNull();
    expect(document.querySelector('[data-slot="sessions-empty-cta"]')).toBeNull();
  });

  it('error kind renders data-slot="sessions-empty-error" with role="alert"', () => {
    render(<EmptySessions kind="error" labels={LABELS} />);
    const el = document.querySelector('[data-slot="sessions-empty-error"]');
    expect(el).not.toBeNull();
    expect(el!.getAttribute('role')).toBe('alert');
  });

  it('error kind renders title and CTA, fires onPrimaryAction on click', () => {
    const onPrimaryAction = vi.fn();
    render(<EmptySessions kind="error" labels={LABELS} onPrimaryAction={onPrimaryAction} />);
    expect(screen.getByText('Impossibile caricare')).toBeTruthy();
    fireEvent.click(screen.getByText('Riprova'));
    expect(onPrimaryAction).toHaveBeenCalledOnce();
  });

  it('loading kind renders data-slot="sessions-empty-loading" with aria-busy="true"', () => {
    render(<EmptySessions kind="loading" labels={LABELS} />);
    const el = document.querySelector('[data-slot="sessions-empty-loading"]');
    expect(el).not.toBeNull();
    expect(el!.getAttribute('aria-busy')).toBe('true');
  });

  it('loading kind renders 8 skeleton placeholder rows', () => {
    render(<EmptySessions kind="loading" labels={LABELS} />);
    const skeletons = document.querySelectorAll('.animate-pulse');
    expect(skeletons.length).toBeGreaterThanOrEqual(8);
  });

  it('data-slot selector matches kind for all 4 kinds', () => {
    const kinds = ['empty', 'filtered-empty', 'loading', 'error'] as const;
    for (const kind of kinds) {
      const { unmount } = render(<EmptySessions kind={kind} labels={LABELS} />);
      const el = document.querySelector(`[data-slot="sessions-empty-${kind}"]`);
      expect(el, `data-slot="sessions-empty-${kind}" missing`).not.toBeNull();
      unmount();
    }
  });
});
