/**
 * GameRulesTab Component Tests
 *
 * Tests for GameRulesTab component that displays PDF rulebooks and documents.
 * Tests cover rendering, document states (completed/processing/failed), and accessibility.
 *
 * Issue #1887 - Batch 14: Test rewrite with correct component text expectations
 */

import { render, screen } from '@testing-library/react';
import { GameRulesTab } from '../GameRulesTab';
import type { Game, PdfDocumentDto } from '@/lib/api';

const createMockGame = (overrides?: Partial<Game>): Game => ({
  id: 'game-1',
  title: 'Catan',
  publisher: 'Catan Studio',
  yearPublished: 1995,
  minPlayers: 3,
  maxPlayers: 4,
  minPlayTimeMinutes: 60,
  maxPlayTimeMinutes: 120,
  bggId: 13,
  createdAt: new Date().toISOString(),
  imageUrl: null,
  faqCount: null,
  averageRating: null,
  ...overrides,
});

const createMockDocument = (overrides?: Partial<PdfDocumentDto>): PdfDocumentDto => ({
  id: 'pdf-1',
  gameId: 'game-1',
  fileName: 'rulebook.pdf',
  fileSizeBytes: 1024000,
  uploadedAt: new Date().toISOString(),
  uploadedByUserId: 'user-1',
  processingStatus: 'Completed',
  processingError: null,
  qualityScore: null,
  textExtractionMethod: null,
  pageCount: 12,
  logUrl: null,
  ...overrides,
});

describe('GameRulesTab', () => {
  describe('Rendering', () => {
    it('renders card title', () => {
      const game = createMockGame();
      const documents: PdfDocumentDto[] = [createMockDocument()];

      render(<GameRulesTab game={game} documents={documents} />);

      expect(screen.getByText('Rulebooks & Documents')).toBeInTheDocument();
    });

    it('shows document count badge', () => {
      const game = createMockGame();
      const documents = [createMockDocument(), createMockDocument({ id: 'pdf-2' })];

      render(<GameRulesTab game={game} documents={documents} />);

      expect(screen.getByText('2 total')).toBeInTheDocument();
    });

    it('renders empty state when no documents', () => {
      const game = createMockGame();

      render(<GameRulesTab game={game} documents={[]} />);

      expect(screen.getByText(/no rulebooks have been uploaded/i)).toBeInTheDocument();
    });
  });

  describe('Document Display', () => {
    it('displays completed document with details', () => {
      const game = createMockGame();
      const doc = createMockDocument({
        fileName: 'catan-rules.pdf',
        fileSizeBytes: 2048000,
        pageCount: 24,
      });

      render(<GameRulesTab game={game} documents={[doc]} />);

      expect(screen.getByText('catan-rules.pdf')).toBeInTheDocument();
      expect(screen.getByText(/2.0 MB/)).toBeInTheDocument();
      expect(screen.getByText(/24 pages/)).toBeInTheDocument();
    });

    it('shows processing status for documents being processed', () => {
      const game = createMockGame();
      const doc = createMockDocument({
        fileName: 'processing.pdf',
        processingStatus: 'Processing',
      });

      render(<GameRulesTab game={game} documents={[doc]} />);

      expect(screen.getByText('processing.pdf')).toBeInTheDocument();
      expect(screen.getAllByText('Processing').length).toBeGreaterThan(0);
    });

    it('shows failed status for failed documents', () => {
      const game = createMockGame();
      const doc = createMockDocument({
        fileName: 'failed.pdf',
        processingStatus: 'Failed',
      });

      render(<GameRulesTab game={game} documents={[doc]} />);

      expect(screen.getByText('failed.pdf')).toBeInTheDocument();
      expect(screen.getAllByText('Failed').length).toBeGreaterThan(0);
    });

    it('groups documents by status', () => {
      const game = createMockGame();
      const documents = [
        createMockDocument({ id: '1', fileName: 'completed.pdf', processingStatus: 'Completed' }),
        createMockDocument({ id: '2', fileName: 'processing.pdf', processingStatus: 'Processing' }),
        createMockDocument({ id: '3', fileName: 'failed.pdf', processingStatus: 'Failed' }),
      ];

      render(<GameRulesTab game={game} documents={documents} />);

      expect(screen.getByText('Ready to Use')).toBeInTheDocument();
      expect(screen.getAllByText('Processing').length).toBeGreaterThan(0);
      expect(screen.getAllByText('Failed').length).toBeGreaterThan(0);
    });
  });

  describe('Placeholder Sections', () => {
    it('shows RuleSpec placeholder', () => {
      const game = createMockGame();

      render(<GameRulesTab game={game} documents={[]} />);

      expect(screen.getByText('RuleSpec Versions')).toBeInTheDocument();
      expect(screen.getByText(/rulespec integration coming soon/i)).toBeInTheDocument();
    });

    it('shows upload placeholder', () => {
      const game = createMockGame();

      render(<GameRulesTab game={game} documents={[]} />);

      expect(screen.getByText('Upload New Rulebook')).toBeInTheDocument();
      expect(screen.getByText(/document upload functionality coming soon/i)).toBeInTheDocument();
    });
  });

  describe('Accessibility', () => {
    it('has accessible card structure', () => {
      const game = createMockGame();
      const documents: PdfDocumentDto[] = [createMockDocument()];

      render(<GameRulesTab game={game} documents={documents} />);

      expect(screen.getByText('Rulebooks & Documents')).toBeInTheDocument();
      expect(screen.getByText('Uploaded PDF rulebooks and documentation')).toBeInTheDocument();
    });

    it('renders document actions as buttons', () => {
      const game = createMockGame();
      const documents = [createMockDocument()];

      render(<GameRulesTab game={game} documents={documents} />);

      expect(screen.getByRole('button', { name: /view/i })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /download/i })).toBeInTheDocument();
    });
  });
});
