/**
 * KbStatusPanel Tests
 * Issue #4948: Agent config page redesign — KB status panel
 *
 * Test Coverage:
 * - Loading state: spinner displayed
 * - Error state: error message displayed
 * - Empty state (no PDF linked): empty state + upload CTA
 * - Document indexed: teal card, StatusBadge, chunk count
 * - Document processing/pending: amber card, progress bar
 * - Document failed: red card, error message
 */

import type { ReactNode } from 'react';

import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { KbStatusPanel } from '../KbStatusPanel';

// ============================================================================
// Mocks
// ============================================================================

vi.mock('@/hooks/queries/usePdfProcessingStatus', () => ({
  usePdfProcessingStatus: vi.fn(),
}));

vi.mock('next/link', () => ({
  default: ({
    children,
    href,
    className,
    'data-testid': testId,
  }: {
    children: ReactNode;
    href: string;
    className?: string;
    'data-testid'?: string;
  }) => (
    <a href={href} className={className} data-testid={testId}>
      {children}
    </a>
  ),
}));

// eslint-disable-next-line @typescript-eslint/no-explicit-any
const { usePdfProcessingStatus } = await import('@/hooks/queries/usePdfProcessingStatus') as any;

// ============================================================================
// Helpers
// ============================================================================

function mockQuery(overrides: Record<string, unknown>) {
  usePdfProcessingStatus.mockReturnValue({
    data: undefined,
    isLoading: false,
    isError: false,
    ...overrides,
  });
}

// ============================================================================
// Tests
// ============================================================================

describe('KbStatusPanel', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Loading State', () => {
    it('shows loading spinner while fetching status', () => {
      mockQuery({ isLoading: true });

      render(<KbStatusPanel gameId="game-1" />);

      expect(screen.getByText(/Controllo stato KB/i)).toBeInTheDocument();
    });

    it('does not show document card while loading', () => {
      mockQuery({ isLoading: true });

      render(<KbStatusPanel gameId="game-1" />);

      expect(screen.queryByTestId('kb-status-document')).not.toBeInTheDocument();
    });
  });

  describe('Error State', () => {
    it('shows error message when fetch fails', () => {
      mockQuery({ isError: true });

      render(<KbStatusPanel gameId="game-1" />);

      expect(screen.getByText(/Errore nel caricare lo stato KB/i)).toBeInTheDocument();
    });

    it('shows retry hint in error state', () => {
      mockQuery({ isError: true });

      render(<KbStatusPanel gameId="game-1" />);

      expect(screen.getByText(/Riprova tra qualche secondo/i)).toBeInTheDocument();
    });

    it('does not show document card in error state', () => {
      mockQuery({ isError: true });

      render(<KbStatusPanel gameId="game-1" />);

      expect(screen.queryByTestId('kb-status-document')).not.toBeInTheDocument();
    });
  });

  describe('Empty State (No PDF Linked)', () => {
    it('shows empty state message when no data', () => {
      mockQuery({ data: undefined });

      render(<KbStatusPanel gameId="game-1" />);

      expect(screen.getByTestId('kb-status-no-pdf')).toBeInTheDocument();
      expect(screen.getByText(/Nessun documento collegato/i)).toBeInTheDocument();
    });

    it('shows upload CTA linking to add-pdf flow', () => {
      mockQuery({ data: undefined });

      render(<KbStatusPanel gameId="game-1" />);

      const cta = screen.getByTestId('kb-status-upload-cta');
      expect(cta).toBeInTheDocument();
      expect(cta).toHaveAttribute('href', '/library/private/add?gameId=game-1&step=2');
    });

    it('shows upload description text', () => {
      mockQuery({ data: undefined });

      render(<KbStatusPanel gameId="game-1" />);

      expect(
        screen.getByText(/Carica un PDF del regolamento/i)
      ).toBeInTheDocument();
    });
  });

  describe('Indexed Document', () => {
    it('renders document card when status is indexed', () => {
      mockQuery({
        data: { status: 'indexed', chunkCount: 128 },
      });

      render(<KbStatusPanel gameId="game-1" />);

      expect(screen.getByTestId('kb-status-document')).toBeInTheDocument();
    });

    it('shows "Indicizzato" status badge', () => {
      mockQuery({
        data: { status: 'indexed', chunkCount: 64 },
      });

      render(<KbStatusPanel gameId="game-1" />);

      expect(screen.getByText('Indicizzato')).toBeInTheDocument();
    });

    it('shows chunk count when indexed', () => {
      mockQuery({
        data: { status: 'indexed', chunkCount: 256 },
      });

      render(<KbStatusPanel gameId="game-1" />);

      const chunkEl = screen.getByTestId('kb-status-chunk-count');
      expect(chunkEl).toBeInTheDocument();
      expect(chunkEl).toHaveTextContent('256');
    });

    it('does not show chunk count when chunkCount is null', () => {
      mockQuery({
        data: { status: 'indexed', chunkCount: null },
      });

      render(<KbStatusPanel gameId="game-1" />);

      expect(screen.queryByTestId('kb-status-chunk-count')).not.toBeInTheDocument();
    });

    it('does not show progress bar when indexed', () => {
      mockQuery({
        data: { status: 'indexed', chunkCount: 10 },
      });

      render(<KbStatusPanel gameId="game-1" />);

      expect(screen.queryByTestId('kb-status-progress')).not.toBeInTheDocument();
    });

    it('shows add-another-document link when document exists', () => {
      mockQuery({
        data: { status: 'indexed', chunkCount: 10 },
      });

      render(<KbStatusPanel gameId="game-1" />);

      const addLink = screen.getByTestId('kb-status-add-doc');
      expect(addLink).toHaveAttribute('href', '/library/private/add?gameId=game-1&step=2');
    });
  });

  describe('Processing Document', () => {
    it('shows "In elaborazione" badge when processing', () => {
      mockQuery({
        data: { status: 'processing', progress: 45 },
      });

      render(<KbStatusPanel gameId="game-1" />);

      expect(screen.getByText('In elaborazione')).toBeInTheDocument();
    });

    it('shows progress bar when processing', () => {
      mockQuery({
        data: { status: 'processing', progress: 60 },
      });

      render(<KbStatusPanel gameId="game-1" />);

      expect(screen.getByTestId('kb-status-progress')).toBeInTheDocument();
    });

    it('shows progress percentage in progress bar', () => {
      mockQuery({
        data: { status: 'processing', progress: 75 },
      });

      render(<KbStatusPanel gameId="game-1" />);

      const progressBar = screen.getByRole('progressbar');
      expect(progressBar).toHaveAttribute('aria-valuenow', '75');
    });

    it('uses default progress when progress is null', () => {
      mockQuery({
        data: { status: 'processing', progress: null },
      });

      render(<KbStatusPanel gameId="game-1" />);

      const progressBar = screen.getByRole('progressbar');
      expect(progressBar).toHaveAttribute('aria-valuenow', '15');
    });
  });

  describe('Pending Document', () => {
    it('shows "In attesa" badge when pending', () => {
      mockQuery({
        data: { status: 'pending' },
      });

      render(<KbStatusPanel gameId="game-1" />);

      expect(screen.getByText('In attesa')).toBeInTheDocument();
    });

    it('shows progress bar when pending', () => {
      mockQuery({
        data: { status: 'pending' },
      });

      render(<KbStatusPanel gameId="game-1" />);

      expect(screen.getByTestId('kb-status-progress')).toBeInTheDocument();
    });
  });

  describe('Failed Document', () => {
    it('shows "Fallito" badge when failed', () => {
      mockQuery({
        data: { status: 'failed', errorMessage: 'Parse error on page 5' },
      });

      render(<KbStatusPanel gameId="game-1" />);

      expect(screen.getByText('Fallito')).toBeInTheDocument();
    });

    it('shows error message when failed', () => {
      mockQuery({
        data: { status: 'failed', errorMessage: 'Parse error on page 5' },
      });

      render(<KbStatusPanel gameId="game-1" />);

      const errorEl = screen.getByTestId('kb-status-error');
      expect(errorEl).toHaveTextContent('Parse error on page 5');
    });

    it('does not show error message when errorMessage is null', () => {
      mockQuery({
        data: { status: 'failed', errorMessage: null },
      });

      render(<KbStatusPanel gameId="game-1" />);

      expect(screen.queryByTestId('kb-status-error')).not.toBeInTheDocument();
    });

    it('does not show progress bar when failed', () => {
      mockQuery({
        data: { status: 'failed' },
      });

      render(<KbStatusPanel gameId="game-1" />);

      expect(screen.queryByTestId('kb-status-progress')).not.toBeInTheDocument();
    });
  });

  describe('Panel Structure', () => {
    it('always renders panel container with testid', () => {
      mockQuery({ isLoading: true });

      render(<KbStatusPanel gameId="game-1" />);

      expect(screen.getByTestId('kb-status-panel')).toBeInTheDocument();
    });

    it('always shows Knowledge Base header', () => {
      mockQuery({ isLoading: false, data: undefined });

      render(<KbStatusPanel gameId="game-1" />);

      expect(screen.getByText('Knowledge Base')).toBeInTheDocument();
    });

    it('passes gameId to usePdfProcessingStatus', async () => {
      mockQuery({ isLoading: true });

      render(<KbStatusPanel gameId="abc-123" />);

      expect(usePdfProcessingStatus).toHaveBeenCalledWith('abc-123');
    });
  });
});
