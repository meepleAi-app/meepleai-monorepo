/**
 * PrivateGamePdfSection Component Tests
 *
 * Issue #3664: Private game PDF support — PDF section state machine.
 *
 * Test Coverage:
 * - Shows upload form when no PDF (hasPdf=false, no upload in session)
 * - Shows PdfProcessingStatus when hasPdf=true
 * - Shows disabled chat button while processing
 * - Shows enabled chat button (as Link) when status is Completed
 * - Upload completion flips to status view
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { PrivateGamePdfSection } from '../PrivateGamePdfSection';

// ============================================================================
// Mocks
// ============================================================================

vi.mock('@/hooks/queries/usePrivateGameKbStatus', () => ({
  usePrivateGameKbStatus: vi.fn(),
}));

vi.mock('../PdfProcessingStatus', () => ({
  PdfProcessingStatus: ({ privateGameId }: { privateGameId: string }) => (
    <div data-testid="pdf-processing-status" data-game-id={privateGameId}>
      PDF Processing Status
    </div>
  ),
}));

vi.mock('../PdfUploadForm', () => ({
  PdfUploadForm: ({
    privateGameId,
    onUploadComplete,
  }: {
    privateGameId: string;
    onUploadComplete?: (docId: string) => void;
  }) => (
    <div data-testid="pdf-upload-form" data-game-id={privateGameId}>
      <button data-testid="mock-upload-trigger" onClick={() => onUploadComplete?.('doc-123')}>
        Upload PDF
      </button>
    </div>
  ),
}));

// eslint-disable-next-line @typescript-eslint/no-explicit-any
const { usePrivateGameKbStatus } = (await import('@/hooks/queries/usePrivateGameKbStatus')) as any;

// ============================================================================
// Helpers
// ============================================================================

function mockKbStatus(overrides: Record<string, unknown> = {}) {
  usePrivateGameKbStatus.mockReturnValue({
    data: undefined,
    isLoading: false,
    isError: false,
    ...overrides,
  });
}

// ============================================================================
// Tests
// ============================================================================

describe('PrivateGamePdfSection', () => {
  const TEST_GAME_ID = 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee';

  beforeEach(() => {
    vi.clearAllMocks();
    mockKbStatus();
  });

  describe('No PDF state (hasPdf=false)', () => {
    it('shows the PDF upload form when hasPdf is false', () => {
      render(<PrivateGamePdfSection privateGameId={TEST_GAME_ID} hasPdf={false} />);

      expect(screen.getByTestId('pdf-upload-form')).toBeInTheDocument();
      expect(screen.queryByTestId('pdf-processing-status')).not.toBeInTheDocument();
    });

    it('shows the PDF upload form by default (hasPdf not provided)', () => {
      render(<PrivateGamePdfSection privateGameId={TEST_GAME_ID} />);

      expect(screen.getByTestId('pdf-upload-form')).toBeInTheDocument();
    });

    it('passes privateGameId to PdfUploadForm', () => {
      render(<PrivateGamePdfSection privateGameId={TEST_GAME_ID} hasPdf={false} />);

      expect(screen.getByTestId('pdf-upload-form')).toHaveAttribute('data-game-id', TEST_GAME_ID);
    });

    it('does not render the chat button when no PDF', () => {
      render(<PrivateGamePdfSection privateGameId={TEST_GAME_ID} hasPdf={false} />);

      expect(screen.queryByTestId('chat-button-enabled')).not.toBeInTheDocument();
      expect(screen.queryByTestId('chat-button-disabled')).not.toBeInTheDocument();
    });
  });

  describe('PDF exists state (hasPdf=true)', () => {
    it('shows PdfProcessingStatus when hasPdf is true', () => {
      render(<PrivateGamePdfSection privateGameId={TEST_GAME_ID} hasPdf={true} />);

      expect(screen.getByTestId('pdf-processing-status')).toBeInTheDocument();
      expect(screen.queryByTestId('pdf-upload-form')).not.toBeInTheDocument();
    });

    it('passes privateGameId to PdfProcessingStatus', () => {
      render(<PrivateGamePdfSection privateGameId={TEST_GAME_ID} hasPdf={true} />);

      expect(screen.getByTestId('pdf-processing-status')).toHaveAttribute(
        'data-game-id',
        TEST_GAME_ID
      );
    });

    it('shows disabled chat button when KB status is not Completed', () => {
      mockKbStatus({
        data: { status: 'Extracting', progress: 30, totalChunks: 100, processedChunks: 30 },
      });

      render(<PrivateGamePdfSection privateGameId={TEST_GAME_ID} hasPdf={true} />);

      expect(screen.getByTestId('chat-button-disabled')).toBeInTheDocument();
      expect(screen.getByTestId('chat-button-disabled')).toBeDisabled();
      expect(screen.queryByTestId('chat-button-enabled')).not.toBeInTheDocument();
    });

    it('shows disabled chat button when KB status is Pending', () => {
      mockKbStatus({
        data: { status: 'Pending', progress: 0, totalChunks: 0, processedChunks: 0 },
      });

      render(<PrivateGamePdfSection privateGameId={TEST_GAME_ID} hasPdf={true} />);

      expect(screen.getByTestId('chat-button-disabled')).toBeDisabled();
    });

    it('shows enabled chat button (as link) when KB status is Completed', () => {
      mockKbStatus({
        data: { status: 'Completed', progress: 100, totalChunks: 128, processedChunks: 128 },
      });

      render(<PrivateGamePdfSection privateGameId={TEST_GAME_ID} hasPdf={true} />);

      const chatButton = screen.getByTestId('chat-button-enabled');
      expect(chatButton).toBeInTheDocument();
      expect(screen.queryByTestId('chat-button-disabled')).not.toBeInTheDocument();
    });

    it('chat button links to /chat?gameId=... when Completed', () => {
      mockKbStatus({
        data: { status: 'Completed', progress: 100, totalChunks: 128, processedChunks: 128 },
      });

      render(<PrivateGamePdfSection privateGameId={TEST_GAME_ID} hasPdf={true} />);

      const link = screen.getByTestId('chat-button-enabled').closest('a');
      expect(link).toHaveAttribute('href', `/chat?gameId=${TEST_GAME_ID}`);
    });
  });

  describe('Upload flow', () => {
    it('flips from upload form to status view after successful upload', async () => {
      const user = userEvent.setup();

      render(<PrivateGamePdfSection privateGameId={TEST_GAME_ID} hasPdf={false} />);

      // Initially shows upload form
      expect(screen.getByTestId('pdf-upload-form')).toBeInTheDocument();

      // Trigger upload completion via mock
      await user.click(screen.getByTestId('mock-upload-trigger'));

      // Now shows processing status
      expect(screen.getByTestId('pdf-processing-status')).toBeInTheDocument();
      expect(screen.queryByTestId('pdf-upload-form')).not.toBeInTheDocument();
    });
  });

  describe('Section structure', () => {
    it('renders section element with accessible label', () => {
      render(<PrivateGamePdfSection privateGameId={TEST_GAME_ID} />);

      expect(
        screen.getByRole('region', { name: /Sezione PDF e Knowledge Base/i })
      ).toBeInTheDocument();
    });

    it('renders "Manuale di gioco" heading', () => {
      render(<PrivateGamePdfSection privateGameId={TEST_GAME_ID} />);

      expect(screen.getByText('Manuale di gioco')).toBeInTheDocument();
    });
  });
});
