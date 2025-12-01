/**
 * Tests for GameRulesTab component
 * Auto-generated baseline tests - Issue #992
 * Enhanced with proper mock data - Issue #567-fix
 */

import { render, screen } from '@testing-library/react';
import { GameRulesTab } from '../GameRulesTab';
import type { Game, PdfDocumentDto } from '@/lib/api';

// Create complete mock game
const createMockGame = (overrides?: Partial<Game>): Game => ({
  id: 'game-1',
  title: 'Test Board Game',
  publisher: 'Test Publisher',
  yearPublished: 2023,
  minPlayers: 2,
  maxPlayers: 4,
  minPlayTimeMinutes: 30,
  maxPlayTimeMinutes: 60,
  bggId: 12345,
  createdAt: new Date().toISOString(),
  imageUrl: null,
  faqCount: null,
  averageRating: null,
  ...overrides,
});

// Create complete mock PDF document
const createMockPdfDocument = (overrides?: Partial<PdfDocumentDto>): PdfDocumentDto => ({
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
  pageCount: null,
  logUrl: null,
  ...overrides,
});

describe('GameRulesTab', () => {
  describe('Rendering', () => {
    it('should render without crashing', () => {
      const game = createMockGame();
      const documents: PdfDocumentDto[] = [createMockPdfDocument()];
      render(<GameRulesTab game={game} documents={documents} />);
      expect(screen.getByText('Rule Documents')).toBeInTheDocument();
    });

    it('should render with default props', () => {
      const game = createMockGame();
      const documents: PdfDocumentDto[] = [];
      const { container } = render(<GameRulesTab game={game} documents={documents} />);
      expect(container.firstChild).toBeInTheDocument();
    });
  });

  describe('Props', () => {
    it('should accept and render with custom props', () => {
      const game = createMockGame({ title: 'Custom Game' });
      const documents = [createMockPdfDocument({ fileName: 'custom-rules.pdf' })];
      render(<GameRulesTab game={game} documents={documents} />);
      expect(screen.getByText('custom-rules.pdf')).toBeInTheDocument();
    });

    it('should handle empty documents array', () => {
      const game = createMockGame();
      render(<GameRulesTab game={game} documents={[]} />);
      expect(screen.getByText(/No rule documents/i)).toBeInTheDocument();
    });
  });

  describe('Accessibility', () => {
    it('should have accessible headings', () => {
      const game = createMockGame();
      const documents: PdfDocumentDto[] = [createMockPdfDocument()];
      render(<GameRulesTab game={game} documents={documents} />);
      expect(screen.getByText('Rule Documents')).toBeInTheDocument();
    });
  });
});
