/**
 * KbExtraMeepleCard Tests
 * Issue #5028 - KbExtraMeepleCard — detail card con tab (Epic #5023)
 */

import React from 'react';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { KbExtraMeepleCard } from '../EntityExtraMeepleCard';
import type { KbDetailData } from '../types';

// ============================================================================
// Mocks
// ============================================================================

vi.mock('next/image', () => ({
  default: ({ src, alt }: { src: string; alt: string }) => (
    // eslint-disable-next-line @next/next/no-img-element
    <img src={src} alt={alt} />
  ),
}));

vi.mock('framer-motion', () => ({
  motion: {
    div: React.forwardRef(
      (
        { children, style, ...props }: React.PropsWithChildren<Record<string, unknown>>,
        ref: React.Ref<HTMLDivElement>
      ) => (
        <div ref={ref} style={style as React.CSSProperties} {...props}>
          {children}
        </div>
      )
    ),
  },
}));

vi.stubGlobal('matchMedia', (query: string) => ({
  matches: false,
  media: query,
  onchange: null,
  addListener: vi.fn(),
  removeListener: vi.fn(),
  addEventListener: vi.fn(),
  removeEventListener: vi.fn(),
  dispatchEvent: vi.fn(),
}));

// ============================================================================
// Helpers
// ============================================================================

const MOCK_DATE = '2026-01-10T08:00:00Z';
const MOCK_PROCESSED_DATE = '2026-01-10T08:30:00Z';

function makeData(overrides: Partial<KbDetailData> = {}): KbDetailData {
  return {
    id: 'doc-1',
    fileName: 'catan-rules.pdf',
    status: 'indexed',
    gameId: 'game-1',
    gameName: 'Catan',
    fileSize: 204800,       // 200 KB
    pageCount: 12,
    uploadedAt: MOCK_DATE,
    processedAt: MOCK_PROCESSED_DATE,
    extractedContent: 'Il gioco di Catan è un gioco da tavolo...',
    hasMoreContent: false,
    ...overrides,
  };
}

// ============================================================================
// Tests
// ============================================================================

describe('KbExtraMeepleCard', () => {

  // --------------------------------------------------------------------------
  // Loading / Error states
  // --------------------------------------------------------------------------

  describe('loading and error states', () => {
    it('renders loading state when loading is true', () => {
      render(<KbExtraMeepleCard data={makeData()} loading />);
      expect(screen.getByText('Loading...')).toBeInTheDocument();
    });

    it('renders error state when error is provided', () => {
      render(<KbExtraMeepleCard data={makeData()} error="Documento non trovato" />);
      expect(screen.getByText('Documento non trovato')).toBeInTheDocument();
    });

    it('does not render tabs in loading state', () => {
      render(<KbExtraMeepleCard data={makeData()} loading />);
      expect(screen.queryByRole('tab')).not.toBeInTheDocument();
    });
  });

  // --------------------------------------------------------------------------
  // Overview tab (default)
  // --------------------------------------------------------------------------

  describe('Overview tab', () => {
    it('renders the fileName in the header', () => {
      render(<KbExtraMeepleCard data={makeData()} />);
      // The <h2> heading in EntityHeader shows the fileName
      expect(screen.getByRole('heading', { name: 'catan-rules.pdf' })).toBeInTheDocument();
    });

    it('renders "Indicizzata" badge in header when status is indexed', () => {
      render(<KbExtraMeepleCard data={makeData({ status: 'indexed' })} />);
      // The header badge shows "Indicizzata"
      expect(screen.getAllByText('Indicizzata').length).toBeGreaterThan(0);
    });

    it('renders DocumentStatusBadge for indexed status', () => {
      render(<KbExtraMeepleCard data={makeData({ status: 'indexed' })} />);
      expect(screen.getByTestId('document-status-indexed')).toBeInTheDocument();
    });

    it('renders DocumentStatusBadge for processing status', () => {
      render(<KbExtraMeepleCard data={makeData({ status: 'processing' })} />);
      expect(screen.getByTestId('document-status-processing')).toBeInTheDocument();
    });

    it('renders DocumentStatusBadge for failed status', () => {
      render(<KbExtraMeepleCard data={makeData({ status: 'failed' })} />);
      expect(screen.getByTestId('document-status-failed')).toBeInTheDocument();
    });

    it('renders DocumentStatusBadge for none status', () => {
      render(<KbExtraMeepleCard data={makeData({ status: 'none' })} />);
      expect(screen.getByTestId('document-status-none')).toBeInTheDocument();
    });

    it('renders the fileName chip', () => {
      render(<KbExtraMeepleCard data={makeData()} />);
      // The filename chip shows the filename (may appear multiple times — header + chip)
      expect(screen.getAllByText('catan-rules.pdf').length).toBeGreaterThan(0);
    });

    it('renders the game chip when gameName is set', () => {
      render(<KbExtraMeepleCard data={makeData()} />);
      expect(screen.getByText('Catan')).toBeInTheDocument();
      expect(screen.getByText('Gioco')).toBeInTheDocument();
    });

    it('does not render game chip when gameName is absent', () => {
      render(<KbExtraMeepleCard data={makeData({ gameName: undefined })} />);
      expect(screen.queryByText('Gioco')).not.toBeInTheDocument();
    });

    it('renders fileSize stat card when fileSize is set', () => {
      render(<KbExtraMeepleCard data={makeData({ fileSize: 204800 })} />);
      expect(screen.getByText('200.0 KB')).toBeInTheDocument();
    });

    it('renders pageCount stat card when pageCount is set', () => {
      render(<KbExtraMeepleCard data={makeData({ pageCount: 12 })} />);
      expect(screen.getByText('12')).toBeInTheDocument();
    });

    it('does not render fileSize stat card when fileSize is absent', () => {
      render(<KbExtraMeepleCard data={makeData({ fileSize: undefined })} />);
      // The stat card label "Dimensione" should not appear
      expect(screen.queryByText('Dimensione')).not.toBeInTheDocument();
    });

    it('renders processedAt stat card when status is indexed and processedAt is set', () => {
      render(<KbExtraMeepleCard data={makeData({ status: 'indexed', processedAt: MOCK_PROCESSED_DATE })} />);
      expect(screen.getByText('Indicizzato')).toBeInTheDocument();
    });

    it('does not render processedAt stat card when status is not indexed', () => {
      render(<KbExtraMeepleCard data={makeData({ status: 'processing', processedAt: MOCK_PROCESSED_DATE })} />);
      expect(screen.queryByText('Indicizzato')).not.toBeInTheDocument();
    });
  });

  // --------------------------------------------------------------------------
  // Delete action
  // --------------------------------------------------------------------------

  describe('delete action', () => {
    it('renders delete button when onDelete is provided and status is indexed', () => {
      const onDelete = vi.fn();
      render(<KbExtraMeepleCard data={makeData({ status: 'indexed' })} onDelete={onDelete} />);
      expect(screen.getByTestId('kb-action-delete')).toBeInTheDocument();
    });

    it('does not render delete button when onDelete is absent', () => {
      render(<KbExtraMeepleCard data={makeData()} />);
      expect(screen.queryByTestId('kb-action-delete')).not.toBeInTheDocument();
    });

    it('does not render delete button when status is processing', () => {
      const onDelete = vi.fn();
      render(<KbExtraMeepleCard data={makeData({ status: 'processing' })} onDelete={onDelete} />);
      expect(screen.queryByTestId('kb-action-delete')).not.toBeInTheDocument();
    });

    it('clicking delete shows confirmation dialog', async () => {
      const user = userEvent.setup();
      const onDelete = vi.fn();
      render(<KbExtraMeepleCard data={makeData()} onDelete={onDelete} />);

      await user.click(screen.getByTestId('kb-action-delete'));

      expect(screen.getByText('Eliminare definitivamente?')).toBeInTheDocument();
      expect(screen.getByTestId('kb-action-delete-confirm')).toBeInTheDocument();
      expect(screen.getByTestId('kb-action-delete-cancel')).toBeInTheDocument();
    });

    it('clicking confirm calls onDelete', async () => {
      const user = userEvent.setup();
      const onDelete = vi.fn();
      render(<KbExtraMeepleCard data={makeData()} onDelete={onDelete} />);

      await user.click(screen.getByTestId('kb-action-delete'));
      await user.click(screen.getByTestId('kb-action-delete-confirm'));

      expect(onDelete).toHaveBeenCalledTimes(1);
    });

    it('clicking confirm hides confirmation dialog', async () => {
      const user = userEvent.setup();
      const onDelete = vi.fn();
      render(<KbExtraMeepleCard data={makeData()} onDelete={onDelete} />);

      await user.click(screen.getByTestId('kb-action-delete'));
      await user.click(screen.getByTestId('kb-action-delete-confirm'));

      expect(screen.queryByText('Eliminare definitivamente?')).not.toBeInTheDocument();
    });

    it('clicking cancel hides confirmation dialog without calling onDelete', async () => {
      const user = userEvent.setup();
      const onDelete = vi.fn();
      render(<KbExtraMeepleCard data={makeData()} onDelete={onDelete} />);

      await user.click(screen.getByTestId('kb-action-delete'));
      await user.click(screen.getByTestId('kb-action-delete-cancel'));

      expect(onDelete).not.toHaveBeenCalled();
      expect(screen.queryByText('Eliminare definitivamente?')).not.toBeInTheDocument();
    });
  });

  // --------------------------------------------------------------------------
  // Content tab
  // --------------------------------------------------------------------------

  describe('Content tab', () => {
    beforeEach(() => {
      vi.clearAllMocks();
    });

    it('switches to content tab on click', async () => {
      const user = userEvent.setup();
      render(<KbExtraMeepleCard data={makeData()} />);

      await user.click(screen.getByRole('tab', { name: /contenuto/i }));

      expect(screen.getByTestId('kb-extracted-content')).toBeInTheDocument();
    });

    it('shows "Il contenuto sarà disponibile" when status is processing', async () => {
      const user = userEvent.setup();
      render(<KbExtraMeepleCard data={makeData({ status: 'processing' })} />);

      await user.click(screen.getByRole('tab', { name: /contenuto/i }));

      expect(screen.getByText(/Il contenuto sarà disponibile/)).toBeInTheDocument();
    });

    it('shows "Il contenuto sarà disponibile" when status is none', async () => {
      const user = userEvent.setup();
      render(<KbExtraMeepleCard data={makeData({ status: 'none', extractedContent: undefined })} />);

      await user.click(screen.getByRole('tab', { name: /contenuto/i }));

      expect(screen.getByText(/Il contenuto sarà disponibile/)).toBeInTheDocument();
    });

    it('shows "Nessun testo estratto" when indexed but no extractedContent', async () => {
      const user = userEvent.setup();
      render(<KbExtraMeepleCard data={makeData({ status: 'indexed', extractedContent: undefined })} />);

      await user.click(screen.getByRole('tab', { name: /contenuto/i }));

      expect(screen.getByText('Nessun testo estratto')).toBeInTheDocument();
    });

    it('renders extracted content when indexed and extractedContent is present', async () => {
      const user = userEvent.setup();
      render(<KbExtraMeepleCard data={makeData({ status: 'indexed', extractedContent: 'Regole del gioco...' })} />);

      await user.click(screen.getByRole('tab', { name: /contenuto/i }));

      expect(screen.getByTestId('kb-extracted-content')).toHaveTextContent('Regole del gioco...');
    });

    it('shows "Vedi documento completo" CTA when hasMoreContent is true', async () => {
      const user = userEvent.setup();
      render(<KbExtraMeepleCard data={makeData({ status: 'indexed', extractedContent: 'Testo...', hasMoreContent: true })} />);

      await user.click(screen.getByRole('tab', { name: /contenuto/i }));

      const cta = screen.getByTestId('kb-action-view-full');
      expect(cta).toBeInTheDocument();
      expect(cta).toHaveAttribute('href', '/library/documents/doc-1');
    });

    it('does not show "Vedi documento completo" CTA when hasMoreContent is false', async () => {
      const user = userEvent.setup();
      render(<KbExtraMeepleCard data={makeData({ status: 'indexed', extractedContent: 'Testo...', hasMoreContent: false })} />);

      await user.click(screen.getByRole('tab', { name: /contenuto/i }));

      expect(screen.queryByTestId('kb-action-view-full')).not.toBeInTheDocument();
    });
  });

  // --------------------------------------------------------------------------
  // Status / Timeline tab
  // --------------------------------------------------------------------------

  describe('Status tab', () => {
    beforeEach(() => {
      vi.clearAllMocks();
    });

    it('switches to status tab on click', async () => {
      const user = userEvent.setup();
      render(<KbExtraMeepleCard data={makeData()} />);

      await user.click(screen.getByRole('tab', { name: /stato/i }));

      expect(screen.getByText('Caricato')).toBeInTheDocument();
    });

    it('renders all 4 timeline step labels', async () => {
      const user = userEvent.setup();
      render(<KbExtraMeepleCard data={makeData({ status: 'indexed' })} />);

      await user.click(screen.getByRole('tab', { name: /stato/i }));

      expect(screen.getByText('Caricato')).toBeInTheDocument();
      expect(screen.getByText('Estrazione testo')).toBeInTheDocument();
      expect(screen.getByText('Indicizzazione')).toBeInTheDocument();
      expect(screen.getByText('Completato')).toBeInTheDocument();
    });

    it('renders "Errore" as last step label when status is failed', async () => {
      const user = userEvent.setup();
      render(<KbExtraMeepleCard data={makeData({ status: 'failed' })} />);

      await user.click(screen.getByRole('tab', { name: /stato/i }));

      expect(screen.getByText('Errore')).toBeInTheDocument();
    });

    it('shows progress bar (role=progressbar) when status is processing', async () => {
      const user = userEvent.setup();
      render(<KbExtraMeepleCard data={makeData({ status: 'processing' })} />);

      await user.click(screen.getByRole('tab', { name: /stato/i }));

      expect(screen.getByRole('progressbar')).toBeInTheDocument();
      expect(screen.getByText('Elaborazione in corso…')).toBeInTheDocument();
    });

    it('does not show progress bar when status is indexed', async () => {
      const user = userEvent.setup();
      render(<KbExtraMeepleCard data={makeData({ status: 'indexed' })} />);

      await user.click(screen.getByRole('tab', { name: /stato/i }));

      expect(screen.queryByRole('progressbar')).not.toBeInTheDocument();
    });

    it('shows error panel with default message when status is failed and no errorMessage', async () => {
      const user = userEvent.setup();
      render(<KbExtraMeepleCard data={makeData({ status: 'failed', errorMessage: undefined })} />);

      await user.click(screen.getByRole('tab', { name: /stato/i }));

      expect(screen.getByText(/Si è verificato un errore durante l'indicizzazione/)).toBeInTheDocument();
    });

    it('shows error panel with custom errorMessage when failed', async () => {
      const user = userEvent.setup();
      render(<KbExtraMeepleCard data={makeData({ status: 'failed', errorMessage: 'Formato PDF non supportato' })} />);

      await user.click(screen.getByRole('tab', { name: /stato/i }));

      expect(screen.getByText('Formato PDF non supportato')).toBeInTheDocument();
    });

    it('shows retry button when status is failed and onRetryIndexing is provided', async () => {
      const user = userEvent.setup();
      const onRetry = vi.fn();
      render(<KbExtraMeepleCard data={makeData({ status: 'failed' })} onRetryIndexing={onRetry} />);

      await user.click(screen.getByRole('tab', { name: /stato/i }));

      expect(screen.getByTestId('kb-action-retry-indexing')).toBeInTheDocument();
    });

    it('does not show retry button when status is failed but onRetryIndexing is absent', async () => {
      const user = userEvent.setup();
      render(<KbExtraMeepleCard data={makeData({ status: 'failed' })} />);

      await user.click(screen.getByRole('tab', { name: /stato/i }));

      expect(screen.queryByTestId('kb-action-retry-indexing')).not.toBeInTheDocument();
    });

    it('calls onRetryIndexing when retry button is clicked', async () => {
      const user = userEvent.setup();
      const onRetry = vi.fn();
      render(<KbExtraMeepleCard data={makeData({ status: 'failed' })} onRetryIndexing={onRetry} />);

      await user.click(screen.getByRole('tab', { name: /stato/i }));
      await user.click(screen.getByTestId('kb-action-retry-indexing'));

      expect(onRetry).toHaveBeenCalledTimes(1);
    });

    it('does not show error panel when status is not failed', async () => {
      const user = userEvent.setup();
      render(<KbExtraMeepleCard data={makeData({ status: 'processing' })} />);

      await user.click(screen.getByRole('tab', { name: /stato/i }));

      expect(screen.queryByTestId('kb-action-retry-indexing')).not.toBeInTheDocument();
    });
  });

  // --------------------------------------------------------------------------
  // Status variants — DocumentStatusBadge
  // --------------------------------------------------------------------------

  describe('DocumentStatusBadge status variants', () => {
    it.each([
      ['processing', 'document-status-processing'],
      ['indexed',    'document-status-indexed'],
      ['failed',     'document-status-failed'],
      ['none',       'document-status-none'],
    ] as const)('renders %s status badge', (status, testId) => {
      render(<KbExtraMeepleCard data={makeData({ status })} />);
      expect(screen.getByTestId(testId)).toBeInTheDocument();
    });
  });

  // --------------------------------------------------------------------------
  // data-testid passthrough
  // --------------------------------------------------------------------------

  it('forwards data-testid to root element', () => {
    render(<KbExtraMeepleCard data={makeData()} data-testid="my-kb-card" />);
    expect(screen.getByTestId('my-kb-card')).toBeInTheDocument();
  });

  // --------------------------------------------------------------------------
  // formatBytes helper (through rendered stat cards)
  // --------------------------------------------------------------------------

  describe('formatBytes formatting', () => {
    it('displays bytes for small files', () => {
      render(<KbExtraMeepleCard data={makeData({ fileSize: 512 })} />);
      expect(screen.getByText('512 B')).toBeInTheDocument();
    });

    it('displays KB for medium files', () => {
      render(<KbExtraMeepleCard data={makeData({ fileSize: 2048 })} />);
      expect(screen.getByText('2.0 KB')).toBeInTheDocument();
    });

    it('displays MB for large files', () => {
      render(<KbExtraMeepleCard data={makeData({ fileSize: 3 * 1024 * 1024 })} />);
      expect(screen.getByText('3.0 MB')).toBeInTheDocument();
    });
  });
});
