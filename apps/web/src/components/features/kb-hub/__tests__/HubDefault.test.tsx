import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { HubDefault } from '../HubDefault';
import type { KbPdf } from '../PdfRow';

const baseLabels = {
  headerSubtitle: 'Knowledge Base',
  uploadCta: '+ Carica PDF',
  reindexAllCta: '⟳ Re-index all',
  statsStrip: {
    docs: '{count} documenti',
    chunks: '{count} chunks',
    embeddings: '{count} embeddings',
    lastReindex: 'ultima reindex {relative}',
    coverage: 'Copertura: {level}',
  },
  coverage: {
    None: 'Nessuna',
    Basic: 'Base',
    Standard: 'Standard',
    Complete: 'Completa',
  },
  columnHeaders: {
    document: 'Documento',
    status: 'Stato',
    uploaded: 'Caricato',
  },
  pdfRow: {
    openCta: 'Apri',
    openAria: 'Apri dettaglio {pdfName}',
    chunksLabel: '{count} chunks',
    status: {
      ready: 'Ready',
      indexing: 'Indexing',
      stale: 'Stale',
      failed: 'Failed',
    },
  },
};

const basePdfs: KbPdf[] = [
  { id: 'p1', name: 'Rulebook v2', sizeFormatted: '45 MB', uploadedAtRelative: '2 gg fa' },
  { id: 'p2', name: 'Scenario Book', sizeFormatted: '62 MB', uploadedAtRelative: '5 gg fa' },
];

const baseGame = { title: 'Gloomhaven', emoji: '⚔️' };

describe('HubDefault (Issue #1481)', () => {
  it('renders game title with " · KB" suffix and header subtitle', () => {
    render(
      <HubDefault
        game={baseGame}
        documentCount={12}
        coverageLevel="Standard"
        pdfs={basePdfs}
        labels={baseLabels}
        onUpload={() => {}}
        onReindexAll={() => {}}
        onPdfAction={() => {}}
      />
    );
    expect(screen.getByText('Gloomhaven · KB')).toBeInTheDocument();
    expect(screen.getByText('Knowledge Base')).toBeInTheDocument();
  });

  it('renders stats strip with docs + coverage by default (P83 hides others)', () => {
    const { container } = render(
      <HubDefault
        game={baseGame}
        documentCount={12}
        coverageLevel="Standard"
        pdfs={basePdfs}
        labels={baseLabels}
        onUpload={() => {}}
        onReindexAll={() => {}}
        onPdfAction={() => {}}
      />
    );
    expect(container.querySelector('[data-slot="kb-hub-default-stats-strip"]')).toBeInTheDocument();
    expect(screen.getByText('12 documenti')).toBeInTheDocument();
    expect(screen.getByText('Copertura: Standard')).toBeInTheDocument();
    expect(screen.queryByText(/chunks$/)).not.toBeInTheDocument();
    expect(screen.queryByText(/embeddings$/)).not.toBeInTheDocument();
  });

  it('renders deferred stats strip metrics when chunks/embeddings/lastReindex provided', () => {
    render(
      <HubDefault
        game={baseGame}
        documentCount={12}
        coverageLevel="Complete"
        pdfs={basePdfs}
        labels={baseLabels}
        chunks={1247}
        embeddings={4891}
        lastReindexRelative="3 gg fa"
        onUpload={() => {}}
        onReindexAll={() => {}}
        onPdfAction={() => {}}
      />
    );
    // Locale-tolerant: jsdom may render "1247" instead of "1.247" (it-IT thousand sep).
    expect(screen.getByText(/^1[.,]?247 chunks$/)).toBeInTheDocument();
    expect(screen.getByText(/^4[.,]?891 embeddings$/)).toBeInTheDocument();
    expect(screen.getByText('ultima reindex 3 gg fa')).toBeInTheDocument();
  });

  it('renders PDF list with one row per pdf', () => {
    const { container } = render(
      <HubDefault
        game={baseGame}
        documentCount={2}
        coverageLevel="Basic"
        pdfs={basePdfs}
        labels={baseLabels}
        onUpload={() => {}}
        onReindexAll={() => {}}
        onPdfAction={() => {}}
      />
    );
    expect(container.querySelectorAll('[data-slot="kb-hub-pdf-row"]')).toHaveLength(2);
    expect(screen.getByText('Rulebook v2')).toBeInTheDocument();
    expect(screen.getByText('Scenario Book')).toBeInTheDocument();
  });

  it('invokes onUpload + onReindexAll + onPdfAction handlers', () => {
    const onUpload = vi.fn();
    const onReindexAll = vi.fn();
    const onPdfAction = vi.fn();
    render(
      <HubDefault
        game={baseGame}
        documentCount={2}
        coverageLevel="Standard"
        pdfs={basePdfs}
        labels={baseLabels}
        onUpload={onUpload}
        onReindexAll={onReindexAll}
        onPdfAction={onPdfAction}
      />
    );
    fireEvent.click(screen.getByRole('button', { name: '+ Carica PDF' }));
    expect(onUpload).toHaveBeenCalledTimes(1);
    fireEvent.click(screen.getByRole('button', { name: '⟳ Re-index all' }));
    expect(onReindexAll).toHaveBeenCalledTimes(1);
    fireEvent.click(screen.getByRole('button', { name: /Apri dettaglio Rulebook v2/ }));
    expect(onPdfAction).toHaveBeenCalledWith('p1');
  });
});
