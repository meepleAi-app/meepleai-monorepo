/**
 * Tests for SharedGameExtraMeepleCard
 *
 * Covers: loading state, error state, tab navigation, documents tab,
 * KB cards tab, callbacks (onUploadPdf, onCreateAgent).
 */

import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import type { SharedGameDetailData } from '@/components/ui/data-display/extra-meeple-card/types';

import { SharedGameExtraMeepleCard } from '../SharedGameExtraMeepleCard';

// ============================================================================
// Mock PdfIndexingStatus (uses useQuery internally — not relevant to these tests)
// ============================================================================

vi.mock('../PdfIndexingStatus', () => ({
  PdfIndexingStatus: ({ pdfId, compact }: { pdfId: string; compact?: boolean }) => (
    <div data-testid={`pdf-status-${pdfId}`} data-compact={compact ? 'true' : 'false'}>
      PdfIndexingStatus:{pdfId}
    </div>
  ),
}));

// ============================================================================
// Test data
// ============================================================================

const baseData: SharedGameDetailData = {
  id: 'game-1',
  title: 'Catan',
  imageUrl: undefined,
  publisher: 'Kosmos',
  yearPublished: 1995,
  minPlayers: 3,
  maxPlayers: 4,
  playTimeMinutes: 90,
  description: 'Trade, build, settle.',
  averageRating: 7.8,
  totalPlays: undefined,
  faqCount: 2,
  rulesDocumentCount: undefined,
  status: 'Published',
  documents: [
    {
      id: 'doc-1',
      pdfDocumentId: 'pdf-1',
      documentType: 0, // Rulebook
      version: '1.0',
      isActive: true,
      tags: [],
      createdAt: '2026-01-01T00:00:00Z',
    },
    {
      id: 'doc-2',
      pdfDocumentId: 'pdf-2',
      documentType: 1, // Errata
      version: '2.1',
      isActive: false,
      tags: ['errata'],
      createdAt: '2026-01-15T00:00:00Z',
    },
  ],
  kbCards: [
    {
      id: 'kb-1',
      pdfDocumentId: 'pdf-1',
      fileName: 'catan-rules.pdf',
      indexingStatus: 'completed',
      chunkCount: 42,
      indexedAt: '2026-01-05T12:00:00Z',
      documentType: 'Rulebook',
      version: '1.0',
      isActive: true,
    },
    {
      id: 'kb-2',
      pdfDocumentId: 'pdf-1',
      fileName: 'catan-rules.pdf',
      indexingStatus: 'failed',
      chunkCount: 0,
      indexedAt: null,
      documentType: null,
      version: null,
      isActive: false,
    },
    {
      id: 'kb-3',
      pdfDocumentId: 'pdf-2',
      fileName: 'catan-errata.pdf',
      indexingStatus: 'processing',
      chunkCount: 5,
      indexedAt: null,
      documentType: 'Errata',
      version: '2.1',
      isActive: false,
    },
  ],
  linkedAgent: {
    id: 'agent-1',
    name: 'Catan Arbitro',
    isActive: true,
  },
};

const emptyData: SharedGameDetailData = {
  ...baseData,
  documents: [],
  kbCards: [],
  linkedAgent: null,
};

// ============================================================================
// Tests
// ============================================================================

describe('SharedGameExtraMeepleCard', () => {
  // ---------- Loading / Error states ----------

  it('renders loading state', () => {
    render(<SharedGameExtraMeepleCard data={baseData} loading data-testid="sgcard" />);

    expect(screen.getByText('Caricamento...')).toBeInTheDocument();
  });

  it('renders error state', () => {
    render(
      <SharedGameExtraMeepleCard data={baseData} error="Gioco non trovato" data-testid="sgcard" />,
    );

    expect(screen.getByText('Gioco non trovato')).toBeInTheDocument();
  });

  // ---------- Header ----------

  it('renders game title and publisher in header', () => {
    render(<SharedGameExtraMeepleCard data={baseData} data-testid="sgcard" />);

    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('Kosmos (1995)')).toBeInTheDocument();
  });

  it('renders document count badge in header', () => {
    render(<SharedGameExtraMeepleCard data={baseData} />);

    expect(screen.getByText('2 doc')).toBeInTheDocument();
  });

  it('renders tabs', () => {
    render(<SharedGameExtraMeepleCard data={baseData} />);

    expect(screen.getByRole('tab', { name: /dettagli/i })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /documenti/i })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /kb cards/i })).toBeInTheDocument();
  });

  it('starts on details tab by default', () => {
    render(<SharedGameExtraMeepleCard data={baseData} />);

    expect(screen.getByRole('tab', { name: /dettagli/i })).toHaveAttribute('data-state', 'active');
  });

  // ---------- Details tab ----------

  it('renders game stats on details tab', () => {
    render(<SharedGameExtraMeepleCard data={baseData} />);

    expect(screen.getByText('3-4')).toBeInTheDocument();
    expect(screen.getByText('90m')).toBeInTheDocument();
    expect(screen.getByText('7.8')).toBeInTheDocument();
    expect(screen.getByText('Trade, build, settle.')).toBeInTheDocument();
  });

  it('shows linked agent on details tab', () => {
    render(<SharedGameExtraMeepleCard data={baseData} />);

    expect(screen.getByText('Catan Arbitro')).toBeInTheDocument();
    expect(screen.getByText('Attivo')).toBeInTheDocument();
  });

  it('does not show linked agent section when no agent', () => {
    render(<SharedGameExtraMeepleCard data={emptyData} />);

    expect(screen.queryByText('Agente Collegato')).not.toBeInTheDocument();
  });

  // ---------- Documents tab ----------

  it('switches to documents tab and shows documents', async () => {
    const user = userEvent.setup();
    render(<SharedGameExtraMeepleCard data={baseData} />);

    await user.click(screen.getByRole('tab', { name: /documenti/i }));

    expect(screen.getByText('Rulebook')).toBeInTheDocument();
    expect(screen.getByText('Errata')).toBeInTheDocument();
    expect(screen.getByText('v1.0')).toBeInTheDocument();
    expect(screen.getByText('v2.1')).toBeInTheDocument();
  });

  it('shows active badge for active document', async () => {
    const user = userEvent.setup();
    render(<SharedGameExtraMeepleCard data={baseData} />);

    await user.click(screen.getByRole('tab', { name: /documenti/i }));

    expect(screen.getByText('Attivo')).toBeInTheDocument();
  });

  it('renders PdfIndexingStatus for each document', async () => {
    const user = userEvent.setup();
    render(<SharedGameExtraMeepleCard data={baseData} />);

    await user.click(screen.getByRole('tab', { name: /documenti/i }));

    expect(screen.getByTestId('pdf-status-pdf-1')).toBeInTheDocument();
    expect(screen.getByTestId('pdf-status-pdf-2')).toBeInTheDocument();
  });

  it('calls onUploadPdf when Upload PDF button is clicked', async () => {
    const user = userEvent.setup();
    const onUploadPdf = vi.fn();
    render(<SharedGameExtraMeepleCard data={baseData} onUploadPdf={onUploadPdf} />);

    await user.click(screen.getByRole('tab', { name: /documenti/i }));
    await user.click(screen.getByRole('button', { name: /upload pdf/i }));

    expect(onUploadPdf).toHaveBeenCalledTimes(1);
  });

  it('shows empty state when no documents', async () => {
    const user = userEvent.setup();
    render(<SharedGameExtraMeepleCard data={emptyData} />);

    await user.click(screen.getByRole('tab', { name: /documenti/i }));

    expect(screen.getByText('Nessun documento caricato')).toBeInTheDocument();
  });

  it('shows "Vedi coda" link in documents tab', async () => {
    const user = userEvent.setup();
    render(<SharedGameExtraMeepleCard data={baseData} />);

    await user.click(screen.getByRole('tab', { name: /documenti/i }));

    expect(screen.getByRole('link', { name: /vedi coda/i })).toHaveAttribute(
      'href',
      '/admin/knowledge-base/queue',
    );
  });

  // ---------- KB Cards tab ----------

  it('switches to KB cards tab and shows cards grouped by PDF', async () => {
    const user = userEvent.setup();
    render(<SharedGameExtraMeepleCard data={baseData} />);

    await user.click(screen.getByRole('tab', { name: /kb cards/i }));

    // File names as group headers
    expect(screen.getAllByText('catan-rules.pdf').length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText('catan-errata.pdf').length).toBeGreaterThanOrEqual(1);
  });

  it('shows KB card status badges', async () => {
    const user = userEvent.setup();
    render(<SharedGameExtraMeepleCard data={baseData} />);

    await user.click(screen.getByRole('tab', { name: /kb cards/i }));

    expect(screen.getByText('completed')).toBeInTheDocument();
    expect(screen.getByText('failed')).toBeInTheDocument();
    expect(screen.getByText('processing')).toBeInTheDocument();
  });

  it('shows chunk counts for KB cards', async () => {
    const user = userEvent.setup();
    render(<SharedGameExtraMeepleCard data={baseData} />);

    await user.click(screen.getByRole('tab', { name: /kb cards/i }));

    expect(screen.getByText('42 chunk')).toBeInTheDocument();
  });

  it('enables "Crea Agente" button when completed cards exist', async () => {
    const user = userEvent.setup();
    render(<SharedGameExtraMeepleCard data={baseData} />);

    await user.click(screen.getByRole('tab', { name: /kb cards/i }));

    const btn = screen.getByRole('button', { name: /crea agente/i });
    expect(btn).not.toBeDisabled();
  });

  it('disables "Crea Agente" button when no completed cards', async () => {
    const user = userEvent.setup();
    const dataNoCompleted: SharedGameDetailData = {
      ...baseData,
      kbCards: baseData.kbCards.filter((c) => c.indexingStatus !== 'completed'),
    };
    render(<SharedGameExtraMeepleCard data={dataNoCompleted} />);

    await user.click(screen.getByRole('tab', { name: /kb cards/i }));

    const btn = screen.getByRole('button', { name: /crea agente/i });
    expect(btn).toBeDisabled();
  });

  it('calls onCreateAgent when "Crea Agente" button is clicked', async () => {
    const user = userEvent.setup();
    const onCreateAgent = vi.fn();
    render(<SharedGameExtraMeepleCard data={baseData} onCreateAgent={onCreateAgent} />);

    await user.click(screen.getByRole('tab', { name: /kb cards/i }));
    await user.click(screen.getByRole('button', { name: /crea agente/i }));

    expect(onCreateAgent).toHaveBeenCalledTimes(1);
  });

  it('shows empty KB cards state when no cards', async () => {
    const user = userEvent.setup();
    render(<SharedGameExtraMeepleCard data={emptyData} />);

    await user.click(screen.getByRole('tab', { name: /kb cards/i }));

    expect(screen.getByText('Nessuna KB card generata')).toBeInTheDocument();
  });

  // ---------- Misc ----------

  it('applies custom className', () => {
    render(<SharedGameExtraMeepleCard data={baseData} className="my-custom" data-testid="sgcard" />);

    expect(screen.getByTestId('sgcard')).toHaveClass('my-custom');
  });
});
