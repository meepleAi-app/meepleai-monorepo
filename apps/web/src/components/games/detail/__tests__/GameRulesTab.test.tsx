/**
 * GameRulesTab Component Tests
 *
 * Tests for GameRulesTab component that displays PDF rulebooks and documents.
 * Tests cover rendering, document states (7-state processing), empty states, and loading.
 *
 * Issue M3: Connect KB documents to game detail
 */

import { render, screen } from '@testing-library/react';
import { GameRulesTab } from '../GameRulesTab';
import type { PdfDocumentDto } from '@/lib/api/schemas/pdf.schemas';

const createMockDocument = (overrides?: Partial<PdfDocumentDto>): PdfDocumentDto => ({
  id: 'pdf-1',
  gameId: 'game-1',
  fileName: 'rulebook.pdf',
  filePath: '/uploads/rulebook.pdf',
  fileSizeBytes: 1024000,
  uploadedAt: new Date().toISOString(),
  processingStatus: 'Completed',
  processedAt: null,
  pageCount: 12,
  documentType: 'base',
  isPublic: false,
  processingState: 'Ready',
  progressPercentage: 100,
  retryCount: 0,
  maxRetries: 3,
  canRetry: false,
  errorCategory: null,
  processingError: null,
  documentCategory: 'Rulebook',
  baseDocumentId: null,
  isActiveForRag: true,
  hasAcceptedDisclaimer: false,
  versionLabel: null,
  ...overrides,
});

describe('GameRulesTab', () => {
  describe('Rendering', () => {
    it('renders ready documents section title', () => {
      const documents: PdfDocumentDto[] = [createMockDocument()];
      render(<GameRulesTab gameId="game-1" documents={documents} />);
      expect(screen.getByText('Regolamenti Disponibili')).toBeInTheDocument();
    });

    it('shows document count badge', () => {
      const documents = [
        createMockDocument(),
        createMockDocument({ id: 'pdf-2', fileName: 'expansion.pdf' }),
      ];
      render(<GameRulesTab gameId="game-1" documents={documents} />);
      expect(screen.getByText('2')).toBeInTheDocument();
    });

    it('renders empty state when no documents', () => {
      render(<GameRulesTab gameId="game-1" documents={[]} />);
      expect(screen.getByText('Nessun documento')).toBeInTheDocument();
      expect(
        screen.getByText(/Carica le regole del gioco per consultarle con l'assistente AI/)
      ).toBeInTheDocument();
    });
  });

  describe('Document Display', () => {
    it('displays ready document with details', () => {
      const doc = createMockDocument({
        fileName: 'catan-rules.pdf',
        fileSizeBytes: 2048000,
        pageCount: 24,
      });
      render(<GameRulesTab gameId="game-1" documents={[doc]} />);
      expect(screen.getByText('catan-rules.pdf')).toBeInTheDocument();
      expect(screen.getByText(/2.0 MB/)).toBeInTheDocument();
      expect(screen.getByText(/24 pagine/)).toBeInTheDocument();
    });

    it('shows processing state for active documents', () => {
      const doc = createMockDocument({
        fileName: 'processing.pdf',
        processingState: 'Extracting',
        processingStatus: 'Processing',
      });
      render(<GameRulesTab gameId="game-1" documents={[doc]} />);
      expect(screen.getByText('processing.pdf')).toBeInTheDocument();
      expect(screen.getByText('Extracting')).toBeInTheDocument();
    });

    it('shows failed status for failed documents', () => {
      const doc = createMockDocument({
        fileName: 'failed.pdf',
        processingState: 'Failed',
        processingStatus: 'Failed',
      });
      render(<GameRulesTab gameId="game-1" documents={[doc]} />);
      expect(screen.getByText('failed.pdf')).toBeInTheDocument();
      expect(screen.getByText('Fallito')).toBeInTheDocument();
    });

    it('groups documents by processing state', () => {
      const documents = [
        createMockDocument({
          id: '1',
          fileName: 'ready.pdf',
          processingState: 'Ready',
        }),
        createMockDocument({
          id: '2',
          fileName: 'extracting.pdf',
          processingState: 'Extracting',
          processingStatus: 'Processing',
        }),
        createMockDocument({
          id: '3',
          fileName: 'failed.pdf',
          processingState: 'Failed',
          processingStatus: 'Failed',
        }),
      ];
      render(<GameRulesTab gameId="game-1" documents={documents} />);
      expect(screen.getByText('Regolamenti Disponibili')).toBeInTheDocument();
      expect(screen.getByText('In Elaborazione')).toBeInTheDocument();
      expect(screen.getByText('Falliti')).toBeInTheDocument();
    });
  });

  describe('Loading State', () => {
    it('shows skeleton when loading', () => {
      const { container } = render(
        <GameRulesTab gameId="game-1" documents={[]} isLoading={true} />
      );
      // Skeleton elements should be present
      expect(container.querySelectorAll('.animate-pulse').length).toBeGreaterThan(0);
    });
  });

  describe('Accessibility', () => {
    it('renders document actions as buttons', () => {
      const documents = [createMockDocument()];
      render(<GameRulesTab gameId="game-1" documents={documents} />);
      expect(screen.getByRole('button', { name: /vedi/i })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /scarica/i })).toBeInTheDocument();
    });
  });
});
