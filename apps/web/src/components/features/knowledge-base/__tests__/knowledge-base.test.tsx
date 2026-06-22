import { fireEvent, render, waitFor } from '@testing-library/react';
import { act } from 'react';
import { describe, expect, it, vi } from 'vitest';

import type { KbChunkDetail, KbChunkSummary, KbDocument } from '@/lib/api/kb-detail-api';

import {
  ChunkSearchBox,
  KbChunkListPanel,
  KbChunkPreview,
  KbHeader,
  MarkdownRenderBlock,
} from '../index';

const DOC: KbDocument = {
  id: 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
  title: 'Azul rulebook',
  docType: 'rulebook',
  gameId: 'cccccccc-cccc-4ccc-8ccc-cccccccccccc',
  gameName: 'Azul',
  uploaderName: 'Marco',
  uploadedAt: '2026-01-12T00:00:00Z',
  lastIngestedAt: '2026-03-08T00:00:00Z',
  processingStatus: 'ready',
  chunkCount: 42,
  pageCount: 24,
  language: 'it',
  tags: [],
  fileSize: 12_345,
  indexerVersion: 'v3',
};

const chunkSummary = (n: number, extra: Partial<KbChunkSummary> = {}): KbChunkSummary => ({
  id: `00000000-0000-4000-8000-${n.toString().padStart(12, '0')}`,
  position: n,
  headingPath: [`Sezione ${n}`],
  snippet: `Snippet di prova ${n}`,
  pageNumber: n,
  vectorId: `vec-${n}`,
  usedInChats: 0,
  ...extra,
});

describe('KbHeader (#2311 FE-3)', () => {
  it('renders title + docType pill + game name + size + uploader', () => {
    const { container } = render(<KbHeader document={DOC} />);
    expect(container.textContent).toContain('Azul rulebook');
    expect(container.textContent).toContain('rulebook');
    expect(container.textContent).toContain('Azul');
    expect(container.textContent).toContain('Marco');
    // Size: 12345 bytes ≈ 12.1 KB (12345/102.4/10 rounded)
    expect(container.textContent).toMatch(/12(\.\d+)?\s*KB/);
  });

  it('omits the gameName row when null', () => {
    const { container } = render(<KbHeader document={{ ...DOC, gameName: null }} />);
    const dts = Array.from(container.querySelectorAll('dt')).map(dt => dt.textContent);
    expect(dts).not.toContain('Gioco');
  });
});

describe('MarkdownRenderBlock (#2311 FE-3, DEC-D4)', () => {
  it('renders markdown headings and GFM tables', () => {
    const { container } = render(
      <MarkdownRenderBlock content={`# Title\n\n| A | B |\n|---|---|\n| 1 | 2 |`} />
    );
    expect(container.querySelector('h1')?.textContent).toBe('Title');
    expect(container.querySelector('table')).toBeInTheDocument();
    expect(container.querySelector('td')?.textContent).toBe('1');
  });
});

describe('ChunkSearchBox (#2311 FE-3)', () => {
  it('debounces input and commits the trimmed value', async () => {
    vi.useFakeTimers();
    const onCommit = vi.fn();
    const { container } = render(<ChunkSearchBox onCommit={onCommit} debounceMs={250} />);
    const input = container.querySelector('input')!;

    fireEvent.change(input, { target: { value: '  piazette  ' } });
    expect(onCommit).not.toHaveBeenCalled();

    await act(async () => {
      vi.advanceTimersByTime(260);
    });

    expect(onCommit).toHaveBeenCalledWith('piazette');
    vi.useRealTimers();
  });

  it('rearms the timer on rapid input without firing the intermediate value', async () => {
    vi.useFakeTimers();
    const onCommit = vi.fn();
    const { container } = render(<ChunkSearchBox onCommit={onCommit} debounceMs={100} />);
    const input = container.querySelector('input')!;

    fireEvent.change(input, { target: { value: 'a' } });
    await act(async () => {
      vi.advanceTimersByTime(50);
    });
    fireEvent.change(input, { target: { value: 'ab' } });
    await act(async () => {
      vi.advanceTimersByTime(50);
    });
    fireEvent.change(input, { target: { value: 'abc' } });
    await act(async () => {
      vi.advanceTimersByTime(120);
    });

    expect(onCommit).toHaveBeenCalledTimes(1);
    expect(onCommit).toHaveBeenCalledWith('abc');
    vi.useRealTimers();
  });
});

describe('KbChunkListPanel (#2311 FE-3)', () => {
  it('renders a row per chunk with the usedInChats pill when > 0', () => {
    const chunks = [chunkSummary(0), chunkSummary(1, { usedInChats: 5 })];
    const { container } = render(
      <KbChunkListPanel chunks={chunks} activeChunkId={null} onSelect={vi.fn()} />
    );
    const rows = container.querySelectorAll('[data-slot="kb-chunk-row"]');
    expect(rows).toHaveLength(2);
    expect(container.textContent).toContain('5× chat');
  });

  it('shows the empty state and skips list rendering when chunks is empty', () => {
    const { container } = render(
      <KbChunkListPanel chunks={[]} activeChunkId={null} onSelect={vi.fn()} />
    );
    expect(container.querySelector('[data-slot="kb-chunk-list-empty"]')).toBeInTheDocument();
    expect(container.querySelector('[data-slot="kb-chunk-row"]')).toBeNull();
  });

  it('marks the row matching activeChunkId with data-active=true + aria-pressed=true', () => {
    const chunks = [chunkSummary(0), chunkSummary(1)];
    const { container } = render(
      <KbChunkListPanel chunks={chunks} activeChunkId={chunks[1].id} onSelect={vi.fn()} />
    );
    const rows = container.querySelectorAll('[data-slot="kb-chunk-row"]');
    expect(rows[0].getAttribute('data-active')).toBe('false');
    expect(rows[1].getAttribute('data-active')).toBe('true');
    expect(rows[1].getAttribute('aria-pressed')).toBe('true');
  });

  it('invokes onSelect with the chunk id on click', () => {
    const chunks = [chunkSummary(0)];
    const onSelect = vi.fn();
    const { container } = render(
      <KbChunkListPanel chunks={chunks} activeChunkId={null} onSelect={onSelect} />
    );
    fireEvent.click(container.querySelector('[data-slot="kb-chunk-row"]')!);
    expect(onSelect).toHaveBeenCalledWith(chunks[0].id);
  });
});

describe('KbChunkPreview (#2311 FE-3)', () => {
  const sampleChunk: KbChunkDetail = {
    id: '00000000-0000-4000-8000-000000000001',
    docId: DOC.id,
    position: 1,
    headingPath: ['Intro', 'Benvenuto'],
    content: '# Hello\n\nbody text',
    pageNumber: 2,
    prevChunkId: null,
    nextChunkId: null,
    metadata: {},
  };

  it('renders the empty state by default', () => {
    const { container } = render(<KbChunkPreview state={{ kind: 'empty' }} />);
    expect(container.querySelector('[data-slot="kb-chunk-preview-empty"]')).toBeInTheDocument();
  });

  it('renders the loading state with aria-busy', () => {
    const { container } = render(<KbChunkPreview state={{ kind: 'loading' }} />);
    const node = container.querySelector('[data-slot="kb-chunk-preview-loading"]')!;
    expect(node.getAttribute('aria-busy')).toBe('true');
  });

  it('renders the error state with role=alert', () => {
    const { container } = render(<KbChunkPreview state={{ kind: 'error', message: 'boom' }} />);
    const node = container.querySelector('[data-slot="kb-chunk-preview-error"]')!;
    expect(node.getAttribute('role')).toBe('alert');
    expect(node.textContent).toContain('boom');
  });

  it('renders Markdown by default and switches to Raw on tab click', async () => {
    const { container } = render(<KbChunkPreview state={{ kind: 'ready', chunk: sampleChunk }} />);
    // Markdown view: <h1>
    expect(container.querySelector('h1')?.textContent).toBe('Hello');
    // Tab switch to Raw → <pre> with full content
    const rawTab = container.querySelector('#kb-preview-tab-raw') as HTMLElement;
    expect(rawTab).not.toBeNull();
    fireEvent.click(rawTab);
    await waitFor(() => {
      expect(container.querySelector('pre')?.textContent).toContain('# Hello');
    });
  });
});
