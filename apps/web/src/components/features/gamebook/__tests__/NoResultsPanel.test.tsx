/**
 * NoResultsPanel unit tests — SP6 Phase C.1.B Task B (Issue #789).
 *
 * Coverage:
 *   - data-slot identity
 *   - title (pre-resolved with query interpolated by orchestrator)
 *   - description text
 *   - 3 ActionCards rendered
 *   - each ActionCard fires its respective handler
 *   - illustration is aria-hidden
 *   - <section aria-label="No search results">
 */

import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { NoResultsPanel } from '../NoResultsPanel';
import type { NoResultsPanelProps } from '../NoResultsPanel';

const LABELS: NoResultsPanelProps['labels'] = {
  title: '"Zaardam" non trovato',
  description: 'Tre modi per continuare:',
  actionCardCreate: {
    title: 'Crea gioco nuovo',
    description: 'Manuale che non esiste in nessun database',
  },
  actionCardBgg: {
    title: 'Cerca su BoardGameGeek',
    description: 'Fonte ufficiale con metadati completi',
  },
  actionCardPrivate: {
    title: 'Indicizza solo per me',
    description: 'Privato — non condiviso con la community',
  },
};

const DEFAULT_PROPS: NoResultsPanelProps = {
  query: 'Zaardam',
  onCreateNew: vi.fn(),
  onSearchBgg: vi.fn(),
  onAddPrivate: vi.fn(),
  labels: LABELS,
};

describe('NoResultsPanel', () => {
  it('renders data-slot="no-results-panel"', () => {
    render(<NoResultsPanel {...DEFAULT_PROPS} />);
    expect(document.querySelector('[data-slot="no-results-panel"]')).not.toBeNull();
  });

  it('renders title from labels', () => {
    render(<NoResultsPanel {...DEFAULT_PROPS} />);
    expect(screen.getByText('"Zaardam" non trovato')).toBeTruthy();
  });

  it('renders description from labels', () => {
    render(<NoResultsPanel {...DEFAULT_PROPS} />);
    expect(screen.getByText('Tre modi per continuare:')).toBeTruthy();
  });

  it('exposes section aria-label', () => {
    render(<NoResultsPanel {...DEFAULT_PROPS} />);
    const section = document.querySelector('[data-slot="no-results-panel"]');
    expect(section?.tagName).toBe('SECTION');
    expect(section?.getAttribute('aria-label')).toBeTruthy();
  });

  it('illustration is aria-hidden', () => {
    render(<NoResultsPanel {...DEFAULT_PROPS} />);
    const illu = document.querySelector('[data-slot="no-results-panel-illustration"]');
    expect(illu?.getAttribute('aria-hidden')).toBe('true');
  });

  it('renders 2 ActionCards by default (BGG hidden for non-admin)', () => {
    render(<NoResultsPanel {...DEFAULT_PROPS} />);
    const cards = document.querySelectorAll('[data-slot="action-card"]');
    expect(cards).toHaveLength(2);
  });

  it('renders 3 ActionCards when showBggCard=true (admin flow)', () => {
    render(<NoResultsPanel {...DEFAULT_PROPS} showBggCard />);
    const cards = document.querySelectorAll('[data-slot="action-card"]');
    expect(cards).toHaveLength(3);
  });

  it('renders Create New action card with correct title and description', () => {
    render(<NoResultsPanel {...DEFAULT_PROPS} />);
    expect(screen.getByText('Crea gioco nuovo')).toBeTruthy();
    expect(screen.getByText('Manuale che non esiste in nessun database')).toBeTruthy();
  });

  it('renders Search BGG action card only when showBggCard=true', () => {
    const { rerender } = render(<NoResultsPanel {...DEFAULT_PROPS} />);
    expect(screen.queryByText('Cerca su BoardGameGeek')).toBeNull();

    rerender(<NoResultsPanel {...DEFAULT_PROPS} showBggCard />);
    expect(screen.getByText('Cerca su BoardGameGeek')).toBeTruthy();
    expect(screen.getByText('Fonte ufficiale con metadati completi')).toBeTruthy();
  });

  it('renders Add Private action card with correct title and description', () => {
    render(<NoResultsPanel {...DEFAULT_PROPS} />);
    expect(screen.getByText('Indicizza solo per me')).toBeTruthy();
    expect(screen.getByText('Privato — non condiviso con la community')).toBeTruthy();
  });

  it('fires onCreateNew when first action clicked', () => {
    const onCreateNew = vi.fn();
    render(<NoResultsPanel {...DEFAULT_PROPS} onCreateNew={onCreateNew} />);
    const card = screen.getByLabelText('Crea gioco nuovo');
    fireEvent.click(card);
    expect(onCreateNew).toHaveBeenCalledOnce();
  });

  it('fires onSearchBgg when BGG action clicked (with showBggCard=true)', () => {
    const onSearchBgg = vi.fn();
    render(<NoResultsPanel {...DEFAULT_PROPS} onSearchBgg={onSearchBgg} showBggCard />);
    const card = screen.getByLabelText('Cerca su BoardGameGeek');
    fireEvent.click(card);
    expect(onSearchBgg).toHaveBeenCalledOnce();
  });

  it('fires onAddPrivate when Private action clicked', () => {
    const onAddPrivate = vi.fn();
    render(<NoResultsPanel {...DEFAULT_PROPS} onAddPrivate={onAddPrivate} />);
    const card = screen.getByLabelText('Indicizza solo per me');
    fireEvent.click(card);
    expect(onAddPrivate).toHaveBeenCalledOnce();
  });

  it('uses query prop for data attribute', () => {
    render(<NoResultsPanel {...DEFAULT_PROPS} query="Test Query" />);
    const root = document.querySelector('[data-slot="no-results-panel"]');
    expect(root?.getAttribute('data-query')).toBe('Test Query');
  });

  it('applies custom className to root', () => {
    render(<NoResultsPanel {...DEFAULT_PROPS} className="extra-class" />);
    const section = document.querySelector('[data-slot="no-results-panel"]');
    expect(section?.classList.contains('extra-class')).toBe(true);
  });
});
