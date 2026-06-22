/**
 * GamebookCard unit tests — SP6 Phase B Task 2 (Issue #788).
 *
 * Coverage:
 *   - data-slot + data-status + data-gamebook-id
 *   - title + meta render
 *   - Cover + emoji fallback render with pages label
 *   - StatusPill renders correct variant per status (ready/indexing/error)
 *   - PipStrip only renders for ready cards, with non-zero counts
 *   - Indexing progress bar visible only when status='indexing'
 *   - Error message + retry CTA visible only when status='error'
 *   - onClick fires only when status='ready'
 *   - Retry CTA fires onClick (orchestrator-routed)
 *   - aria-label only on ready button cards
 *   - aria-busy='true' for indexing
 *   - role='alert' on error message panel
 *   - Ready cards render <button>, others render <article>
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { GamebookCard } from '../GamebookCard';
import type { GamebookCardProps } from '../GamebookCard';

import type { GamebookCardData } from '@/lib/gamebook-index/schemas';

const LABELS: GamebookCardProps['labels'] = {
  statusReady: 'Pronto',
  statusIndexing: 'Indicizzazione…',
  statusError: 'Errore · Riprova',
  pagesCount: '142 pagine',
  chunksCount: '142 chunks',
  qaCount: '12 domande',
  sessionsCount: '3 sessioni',
  indexingProgress: '18 di 45 pagine',
  errorRetry: 'Riprova',
  openGamebook: 'Apri manuale',
};

const READY_GAMEBOOK: GamebookCardData = {
  id: '00000000-0000-4000-8000-000000000001',
  gameId: '00000000-0000-4000-8000-0000000c0001',
  title: 'Tainted Grail: The Fall of Avalon',
  publisher: 'Awaken Realms',
  year: 2019,
  pages: 47,
  totalPages: 47,
  chunks: 142,
  status: 'ready',
  cover: null,
  emoji: '⚔️',
  qaCount: 12,
  sessionsCount: 3,
  errorMsg: null,
};

const INDEXING_GAMEBOOK: GamebookCardData = {
  ...READY_GAMEBOOK,
  id: '00000000-0000-4000-8000-000000000002',
  title: 'ISS Vanguard',
  pages: 18,
  totalPages: 45,
  chunks: 0,
  status: 'indexing',
  emoji: '🚀',
  qaCount: 0,
  sessionsCount: 0,
};

const ERROR_GAMEBOOK: GamebookCardData = {
  ...READY_GAMEBOOK,
  id: '00000000-0000-4000-8000-000000000003',
  title: 'Wingspan',
  pages: 12,
  totalPages: 42,
  chunks: 0,
  status: 'error',
  emoji: '🦅',
  qaCount: 0,
  sessionsCount: 0,
  errorMsg: 'OCR failed at page 23',
};

describe('GamebookCard', () => {
  it('renders data-slot="gamebook-card" with data-status and data-gamebook-id', () => {
    render(<GamebookCard gamebook={READY_GAMEBOOK} onClick={vi.fn()} labels={LABELS} />);
    const card = document.querySelector('[data-slot="gamebook-card"]');
    expect(card).not.toBeNull();
    expect(card!.getAttribute('data-status')).toBe('ready');
    expect(card!.getAttribute('data-gamebook-id')).toBe(READY_GAMEBOOK.id);
  });

  it('ready card renders as <button> with aria-label', () => {
    render(<GamebookCard gamebook={READY_GAMEBOOK} onClick={vi.fn()} labels={LABELS} />);
    const card = document.querySelector('[data-slot="gamebook-card"]')!;
    expect(card.tagName).toBe('BUTTON');
    expect(card.getAttribute('aria-label')).toBe('Apri manuale: Tainted Grail: The Fall of Avalon');
  });

  it('indexing card renders as <article> with aria-busy="true"', () => {
    render(<GamebookCard gamebook={INDEXING_GAMEBOOK} onClick={vi.fn()} labels={LABELS} />);
    const card = document.querySelector('[data-slot="gamebook-card"]')!;
    expect(card.tagName).toBe('ARTICLE');
    expect(card.getAttribute('aria-busy')).toBe('true');
  });

  it('error card renders as <article>', () => {
    render(<GamebookCard gamebook={ERROR_GAMEBOOK} onClick={vi.fn()} labels={LABELS} />);
    const card = document.querySelector('[data-slot="gamebook-card"]')!;
    expect(card.tagName).toBe('ARTICLE');
  });

  it('renders title and meta (publisher · year)', () => {
    render(<GamebookCard gamebook={READY_GAMEBOOK} onClick={vi.fn()} labels={LABELS} />);
    expect(screen.getByText('Tainted Grail: The Fall of Avalon')).toBeTruthy();
    const meta = document.querySelector('[data-slot="gamebook-card-meta"]');
    expect(meta!.textContent).toContain('Awaken Realms');
    expect(meta!.textContent).toContain('2019');
  });

  it('renders cover with emoji fallback and "X/Y pag" label', () => {
    render(<GamebookCard gamebook={READY_GAMEBOOK} onClick={vi.fn()} labels={LABELS} />);
    const cover = document.querySelector('[data-slot="gamebook-card-cover"]');
    expect(cover).not.toBeNull();
    expect(cover!.textContent).toContain('⚔️');
    const pagesLabel = document.querySelector('[data-slot="gamebook-card-cover-pages"]');
    expect(pagesLabel!.textContent).toBe('47/47');
  });

  it('ready status pill: green palette, "Pronto" label', () => {
    render(<GamebookCard gamebook={READY_GAMEBOOK} onClick={vi.fn()} labels={LABELS} />);
    const pill = document.querySelector('[data-slot="gamebook-card-status"]');
    expect(pill).not.toBeNull();
    expect(pill!.getAttribute('data-status')).toBe('ready');
    expect(pill!.textContent).toContain('Pronto');
  });

  it('indexing status pill: animated spinner + label', () => {
    render(<GamebookCard gamebook={INDEXING_GAMEBOOK} onClick={vi.fn()} labels={LABELS} />);
    const pill = document.querySelector('[data-slot="gamebook-card-status"]');
    expect(pill).not.toBeNull();
    expect(pill!.getAttribute('data-status')).toBe('indexing');
    expect(pill!.textContent).toContain('Indicizzazione…');
  });

  it('error status pill: warning icon + label', () => {
    render(<GamebookCard gamebook={ERROR_GAMEBOOK} onClick={vi.fn()} labels={LABELS} />);
    const pill = document.querySelector('[data-slot="gamebook-card-status"]');
    expect(pill).not.toBeNull();
    expect(pill!.getAttribute('data-status')).toBe('error');
    expect(pill!.textContent).toContain('Errore · Riprova');
  });

  it('ready card: shows chunks counter when chunks > 0', () => {
    render(<GamebookCard gamebook={READY_GAMEBOOK} onClick={vi.fn()} labels={LABELS} />);
    const counter = document.querySelector('[data-slot="gamebook-card-chunks-counter"]');
    expect(counter).not.toBeNull();
    expect(counter!.textContent).toBe('142 chunks');
  });

  it('ready card: hides chunks counter when chunks === 0', () => {
    const noChunks: GamebookCardData = { ...READY_GAMEBOOK, chunks: 0 };
    render(<GamebookCard gamebook={noChunks} onClick={vi.fn()} labels={LABELS} />);
    expect(document.querySelector('[data-slot="gamebook-card-chunks-counter"]')).toBeNull();
  });

  it('ready card: pip strip renders 3 chips (chunks/qa/sessions)', () => {
    render(<GamebookCard gamebook={READY_GAMEBOOK} onClick={vi.fn()} labels={LABELS} />);
    const strip = document.querySelector('[data-slot="gamebook-card-pip-strip"]');
    expect(strip).not.toBeNull();
    expect(document.querySelector('[data-slot="gamebook-card-pip-chunks"]')).not.toBeNull();
    expect(document.querySelector('[data-slot="gamebook-card-pip-qa"]')).not.toBeNull();
    expect(document.querySelector('[data-slot="gamebook-card-pip-sessions"]')).not.toBeNull();
  });

  it('ready card with all zero counts hides entire pip strip', () => {
    const noPips: GamebookCardData = {
      ...READY_GAMEBOOK,
      chunks: 0,
      qaCount: 0,
      sessionsCount: 0,
    };
    render(<GamebookCard gamebook={noPips} onClick={vi.fn()} labels={LABELS} />);
    expect(document.querySelector('[data-slot="gamebook-card-pip-strip"]')).toBeNull();
  });

  it('indexing card: shows progress bar and indexing label', () => {
    render(<GamebookCard gamebook={INDEXING_GAMEBOOK} onClick={vi.fn()} labels={LABELS} />);
    const progress = document.querySelector('[data-slot="gamebook-card-indexing-progress"]');
    expect(progress).not.toBeNull();
    expect(screen.getByText('18 di 45 pagine')).toBeTruthy();
  });

  it('indexing card: no pip strip rendered', () => {
    render(<GamebookCard gamebook={INDEXING_GAMEBOOK} onClick={vi.fn()} labels={LABELS} />);
    expect(document.querySelector('[data-slot="gamebook-card-pip-strip"]')).toBeNull();
  });

  it('error card: error message panel renders with role="alert"', () => {
    render(<GamebookCard gamebook={ERROR_GAMEBOOK} onClick={vi.fn()} labels={LABELS} />);
    const panel = document.querySelector('[data-slot="gamebook-card-error-msg"]');
    expect(panel).not.toBeNull();
    expect(panel!.getAttribute('role')).toBe('alert');
    expect(panel!.textContent).toContain('OCR failed at page 23');
  });

  it('error card: retry CTA fires onClick with id', () => {
    const onClick = vi.fn();
    render(<GamebookCard gamebook={ERROR_GAMEBOOK} onClick={onClick} labels={LABELS} />);
    const retry = document.querySelector('[data-slot="gamebook-card-retry-cta"]');
    expect(retry).not.toBeNull();
    fireEvent.click(retry!);
    expect(onClick).toHaveBeenCalledWith(ERROR_GAMEBOOK.id);
  });

  it('ready card: outer button click fires onClick with id', () => {
    const onClick = vi.fn();
    render(<GamebookCard gamebook={READY_GAMEBOOK} onClick={onClick} labels={LABELS} />);
    const card = document.querySelector('[data-slot="gamebook-card"]') as HTMLButtonElement;
    fireEvent.click(card);
    expect(onClick).toHaveBeenCalledWith(READY_GAMEBOOK.id);
  });

  it('indexing card: outer click does NOT fire onClick (no navigation)', () => {
    const onClick = vi.fn();
    render(<GamebookCard gamebook={INDEXING_GAMEBOOK} onClick={onClick} labels={LABELS} />);
    const card = document.querySelector('[data-slot="gamebook-card"]')!;
    fireEvent.click(card);
    expect(onClick).not.toHaveBeenCalled();
  });

  it('handles null publisher and year gracefully', () => {
    const noMeta: GamebookCardData = {
      ...READY_GAMEBOOK,
      publisher: null,
      year: null,
    };
    render(<GamebookCard gamebook={noMeta} onClick={vi.fn()} labels={LABELS} />);
    const meta = document.querySelector('[data-slot="gamebook-card-meta"]');
    expect(meta!.textContent).toContain('—');
  });

  it('applies custom className to outer card', () => {
    render(
      <GamebookCard
        gamebook={READY_GAMEBOOK}
        onClick={vi.fn()}
        labels={LABELS}
        className="my-extra-class"
      />
    );
    const card = document.querySelector('[data-slot="gamebook-card"]')!;
    expect(card.classList.contains('my-extra-class')).toBe(true);
  });
});
