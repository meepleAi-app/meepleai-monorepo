/**
 * KbDocList unit tests — Wave C.2 Task 2
 *
 * 6 tests: 5 kinds (loading/error/empty/standalone/success) + render shape.
 * CRITICAL: standalone (Cell 10) vs empty (Cell 8) must render DIFFERENT copy.
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { KbDocList } from '../KbDocList';

const LABELS = {
  title: 'Documenti Knowledge Base',
  subtitle: 'Documenti indicizzati per questo agente.',
  loadingLabel: 'Caricamento documenti...',
  errorLabel: 'Impossibile caricare i documenti.',
  retryLabel: 'Riprova',
  empty: 'Nessun documento indicizzato.',
  emptySubtitle: 'Carica un PDF del regolamento per abilitare la chat contestuale.',
  uploadCta: '↑ Carica documento',
  standaloneTitle: 'Agente standalone',
  standaloneSubtitle:
    'Questo agente non è associato a un gioco. Non ha una Knowledge Base dedicata.',
  standaloneCta: 'Associa un gioco',
  docsCount: '{count} documenti',
  statusIndexed: 'INDICIZZATO',
  statusProcessing: 'ELABORAZIONE',
  statusFailed: 'ERRORE',
};

const SAMPLE_DOCS = [
  {
    id: 'k1',
    title: 'Regolamento.pdf',
    status: 'indexed' as const,
    sizeFormatted: '4.2 MB',
    pages: 24,
    chunks: 142,
  },
];

describe('KbDocList', () => {
  it('renders data-slot attribute', () => {
    render(<KbDocList state={{ kind: 'loading' }} labels={LABELS} />);
    expect(document.querySelector('[data-slot="agent-detail-kb-doc-list"]')).toBeTruthy();
  });

  it('loading kind: renders loading state', () => {
    render(<KbDocList state={{ kind: 'loading' }} labels={LABELS} />);
    // Should show some loading indicator (shimmer or label)
    expect(document.querySelector('[data-slot="agent-detail-kb-doc-list"]')).toBeTruthy();
    // No docs rendered
    expect(screen.queryByText('Regolamento.pdf')).not.toBeInTheDocument();
  });

  it('error kind: renders error with retry button', () => {
    const retry = vi.fn();
    render(<KbDocList state={{ kind: 'error', retry }} labels={LABELS} />);
    expect(screen.getByRole('button', { name: /riprova/i })).toBeInTheDocument();
  });

  it('empty kind (Cell 8): renders empty state with upload CTA — NOT standalone copy', () => {
    render(<KbDocList state={{ kind: 'empty' }} labels={LABELS} />);
    expect(screen.getByText(/nessun documento indicizzato/i)).toBeInTheDocument();
    // Must NOT show standalone copy
    expect(screen.queryByText(/agente standalone/i)).not.toBeInTheDocument();
  });

  it('standalone kind (Cell 10): renders DEDICATED standalone empty state — DISTINCT from empty', () => {
    render(<KbDocList state={{ kind: 'standalone' }} labels={LABELS} />);
    expect(screen.getByText(/agente standalone/i)).toBeInTheDocument();
    // Must NOT show generic empty copy
    expect(screen.queryByText(/nessun documento indicizzato/i)).not.toBeInTheDocument();
  });

  it('success kind: renders doc list entries', () => {
    render(<KbDocList state={{ kind: 'success', docs: SAMPLE_DOCS }} labels={LABELS} />);
    expect(screen.getByText('Regolamento.pdf')).toBeInTheDocument();
  });
});
