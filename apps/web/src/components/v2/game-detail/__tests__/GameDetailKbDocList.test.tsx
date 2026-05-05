/**
 * Wave C.1 (Issue #581) — GameDetailKbDocList unit tests.
 */

import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import {
  GameDetailKbDocList,
  type GameDetailKbDocEntry,
  type GameDetailKbDocListLabels,
} from '../GameDetailKbDocList';

const labels: GameDetailKbDocListLabels = {
  title: 'Documenti',
  subtitle: 'PDF caricati.',
  empty: 'Nessun documento',
  emptySubtitle: 'Carica un PDF per abilitare la chat.',
  uploadCta: '↑ Carica',
  openCta: 'Apri ↗',
  openAriaLabel: 'Apri documento {name}',
  statusIndexed: 'INDICIZZATO',
  statusProcessing: 'IN ELABORAZIONE',
  statusFailed: 'ERRORE',
  statsLineTemplate: '{size} · {pages} pag · {chunks} chunks',
};

const docs: ReadonlyArray<GameDetailKbDocEntry> = [
  {
    id: 'd1',
    title: 'wingspan-rules.pdf',
    status: 'indexed',
    sizeFormatted: '2.4 MB',
    pages: 14,
    chunks: 38,
    href: '/kb/d1',
  },
  {
    id: 'd2',
    title: 'expansion-asia.pdf',
    status: 'processing',
    sizeFormatted: '1.2 MB',
    pages: 6,
    chunks: 0,
  },
  {
    id: 'd3',
    title: 'failed-doc.pdf',
    status: 'failed',
    sizeFormatted: '900 KB',
    pages: 0,
    chunks: 0,
  },
];

describe('GameDetailKbDocList (Wave C.1)', () => {
  it('renders empty state when docs is empty', () => {
    render(<GameDetailKbDocList docs={[]} labels={labels} />);
    expect(screen.getByText('Nessun documento')).toBeInTheDocument();
  });

  it('renders one row per document', () => {
    const { container } = render(<GameDetailKbDocList docs={docs} labels={labels} />);
    expect(container.querySelectorAll('[data-slot="game-detail-kb-row"]')).toHaveLength(3);
  });

  it('shows the right status label for each status', () => {
    render(<GameDetailKbDocList docs={docs} labels={labels} />);
    expect(screen.getByText('INDICIZZATO')).toBeInTheDocument();
    expect(screen.getByText('IN ELABORAZIONE')).toBeInTheDocument();
    expect(screen.getByText('ERRORE')).toBeInTheDocument();
  });

  it('substitutes {size}/{pages}/{chunks} in stats line', () => {
    render(<GameDetailKbDocList docs={docs} labels={labels} />);
    expect(screen.getByText(/2\.4 MB · 14 pag · 38 chunks/)).toBeInTheDocument();
  });

  it('renders open link only when href is provided', () => {
    render(<GameDetailKbDocList docs={docs} labels={labels} />);
    expect(screen.getByRole('link', { name: 'Apri documento wingspan-rules.pdf' })).toHaveAttribute(
      'href',
      '/kb/d1'
    );
    expect(
      screen.queryByRole('link', { name: 'Apri documento expansion-asia.pdf' })
    ).not.toBeInTheDocument();
  });

  it('calls onUpload when the upload button is clicked', () => {
    const onUpload = vi.fn();
    render(<GameDetailKbDocList docs={docs} labels={labels} onUpload={onUpload} />);
    fireEvent.click(screen.getByRole('button', { name: '↑ Carica' }));
    expect(onUpload).toHaveBeenCalledTimes(1);
  });

  it('exposes data-status per row', () => {
    const { container } = render(<GameDetailKbDocList docs={docs} labels={labels} />);
    const rows = container.querySelectorAll('[data-slot="game-detail-kb-row"]');
    expect(rows[0]).toHaveAttribute('data-status', 'indexed');
    expect(rows[1]).toHaveAttribute('data-status', 'processing');
    expect(rows[2]).toHaveAttribute('data-status', 'failed');
  });

  it('exposes data-slot="game-detail-kb-doc-list" for E2E selector', () => {
    const { container } = render(<GameDetailKbDocList docs={docs} labels={labels} />);
    expect(container.querySelector('[data-slot="game-detail-kb-doc-list"]')).toBeInTheDocument();
  });
});
