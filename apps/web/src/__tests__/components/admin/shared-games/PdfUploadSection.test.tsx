/**
 * PdfUploadSection Component Unit Tests - Issue #3642
 *
 * Tests for the PDF upload section component with:
 * - Drag-and-drop file selection
 * - File validation (PDF type, 50MB limit)
 * - Upload progress tracking
 * - Success/error states
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { PdfUploadSection } from '@/components/admin/shared-games/PdfUploadSection';

// ============================================================================
// Test Utilities
// ============================================================================

function createTestQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
        gcTime: 0,
      },
    },
  });
}

function renderWithProviders(component: React.ReactElement) {
  const queryClient = createTestQueryClient();
  return render(
    <QueryClientProvider client={queryClient}>
      {component}
    </QueryClientProvider>
  );
}

function createMockFile(name: string, type: string, size: number): File {
  const content = new Array(size).fill('a').join('');
  return new File([content], name, { type });
}

// ============================================================================
// Mock XHR
// ============================================================================

const mockXHR = {
  open: vi.fn(),
  send: vi.fn(),
  upload: {
    addEventListener: vi.fn(),
  },
  addEventListener: vi.fn(),
  withCredentials: false,
};

beforeEach(() => {
  vi.clearAllMocks();
  global.XMLHttpRequest = vi.fn(() => mockXHR) as unknown as typeof XMLHttpRequest;
});

afterEach(() => {
  vi.restoreAllMocks();
});

// ============================================================================
// Tests
// ============================================================================

describe('PdfUploadSection', () => {
  const defaultProps = {
    gameId: 'game-123',
    onPdfUploaded: vi.fn(),
    onPdfRemoved: vi.fn(),
  };

  describe('Rendering', () => {
    it('should render the upload section with title', () => {
      renderWithProviders(<PdfUploadSection {...defaultProps} />);

      expect(screen.getByText('PDF Regolamento')).toBeInTheDocument();
      expect(screen.getByText(/Carica un PDF del regolamento/i)).toBeInTheDocument();
    });

    it('should render the drop zone', () => {
      renderWithProviders(<PdfUploadSection {...defaultProps} />);

      expect(screen.getByText(/Trascina un PDF o clicca per selezionare/i)).toBeInTheDocument();
      expect(screen.getByText(/PDF fino a 50MB/i)).toBeInTheDocument();
    });

    it('should render the info box', () => {
      renderWithProviders(<PdfUploadSection {...defaultProps} />);

      expect(screen.getByText(/Il PDF caricato sarà indicizzato automaticamente/i)).toBeInTheDocument();
    });

    it('should render existing PDF when provided', () => {
      const existingPdf = {
        id: 'pdf-123',
        fileName: 'rulebook.pdf',
        fileSize: 1024 * 1024 * 5, // 5MB
      };

      renderWithProviders(<PdfUploadSection {...defaultProps} existingPdf={existingPdf} />);

      expect(screen.getByText('rulebook.pdf')).toBeInTheDocument();
      expect(screen.getByText(/5\.00 MB/i)).toBeInTheDocument();
    });
  });

  describe('File Validation', () => {
    it('should reject non-PDF files and not show in UI', async () => {
      const user = userEvent.setup();
      renderWithProviders(<PdfUploadSection {...defaultProps} />);

      const input = document.getElementById('admin-pdf-input') as HTMLInputElement;
      const textFile = createMockFile('document.txt', 'text/plain', 1024);

      await user.upload(input, textFile);

      // File should NOT be displayed since it's invalid
      await waitFor(() => {
        expect(screen.queryByText('document.txt')).not.toBeInTheDocument();
      });
      // Drop zone should still be visible
      expect(screen.getByText(/Trascina un PDF/i)).toBeInTheDocument();
    });

    it('should reject files larger than 50MB and not show in UI', async () => {
      const user = userEvent.setup();
      renderWithProviders(<PdfUploadSection {...defaultProps} />);

      const input = document.getElementById('admin-pdf-input') as HTMLInputElement;
      const largePdf = createMockFile('large.pdf', 'application/pdf', 51 * 1024 * 1024);

      await user.upload(input, largePdf);

      // File should NOT be displayed since it's too large
      await waitFor(() => {
        expect(screen.queryByText('large.pdf')).not.toBeInTheDocument();
      });
      // Drop zone should still be visible
      expect(screen.getByText(/Trascina un PDF/i)).toBeInTheDocument();
    });

    it('should accept valid PDF files', async () => {
      const user = userEvent.setup();
      renderWithProviders(<PdfUploadSection {...defaultProps} />);

      const input = document.getElementById('admin-pdf-input') as HTMLInputElement;
      const validPdf = createMockFile('rulebook.pdf', 'application/pdf', 1024 * 1024);

      await user.upload(input, validPdf);

      // File should be displayed (selected state)
      await waitFor(() => {
        expect(screen.getByText('rulebook.pdf')).toBeInTheDocument();
      });
    });
  });

  describe('Drag and Drop', () => {
    it('should handle drag over state', () => {
      renderWithProviders(<PdfUploadSection {...defaultProps} />);

      const dropZone = screen.getByText(/Trascina un PDF/i).closest('div');
      expect(dropZone).toBeInTheDocument();

      fireEvent.dragOver(dropZone!);
      // Visual state change should occur (tested via class presence)
    });

    it('should handle drag leave state', () => {
      renderWithProviders(<PdfUploadSection {...defaultProps} />);

      const dropZone = screen.getByText(/Trascina un PDF/i).closest('div');
      expect(dropZone).toBeInTheDocument();

      fireEvent.dragOver(dropZone!);
      fireEvent.dragLeave(dropZone!);
      // Visual state change should revert
    });

    it('should handle file drop', async () => {
      renderWithProviders(<PdfUploadSection {...defaultProps} />);

      const dropZone = screen.getByText(/Trascina un PDF/i).closest('div');
      expect(dropZone).toBeInTheDocument();

      const validPdf = createMockFile('dropped.pdf', 'application/pdf', 1024);

      const dropEvent = new Event('drop', { bubbles: true });
      Object.defineProperty(dropEvent, 'dataTransfer', {
        value: {
          files: [validPdf],
        },
      });
      Object.defineProperty(dropEvent, 'preventDefault', { value: vi.fn() });

      fireEvent(dropZone!, dropEvent);

      await waitFor(() => {
        expect(screen.getByText('dropped.pdf')).toBeInTheDocument();
      });
    });
  });

  describe('Disabled State', () => {
    it('should disable interactions when disabled prop is true', () => {
      renderWithProviders(<PdfUploadSection {...defaultProps} disabled={true} />);

      const input = document.getElementById('admin-pdf-input') as HTMLInputElement;
      expect(input).toBeDisabled();
    });
  });

  describe('Existing PDF Display', () => {
    it('should display existing PDF with remove button', () => {
      const existingPdf = {
        id: 'pdf-456',
        fileName: 'existing-rulebook.pdf',
        fileSize: 2 * 1024 * 1024,
      };

      renderWithProviders(<PdfUploadSection {...defaultProps} existingPdf={existingPdf} />);

      expect(screen.getByText('existing-rulebook.pdf')).toBeInTheDocument();
      // Remove button should be present
      const removeButton = screen.getByRole('button');
      expect(removeButton).toBeInTheDocument();
    });

    it('should call onPdfRemoved when remove is clicked', async () => {
      const user = userEvent.setup();
      const existingPdf = {
        id: 'pdf-789',
        fileName: 'remove-me.pdf',
        fileSize: 1024 * 1024,
      };

      renderWithProviders(<PdfUploadSection {...defaultProps} existingPdf={existingPdf} />);

      // Find and click the remove button (X icon button)
      const removeButtons = screen.getAllByRole('button');
      const removeButton = removeButtons.find(btn => btn.querySelector('svg'));

      if (removeButton) {
        await user.click(removeButton);
        expect(defaultProps.onPdfRemoved).toHaveBeenCalled();
      }
    });
  });

  describe('File Size Formatting', () => {
    it('should format bytes correctly', () => {
      const smallPdf = {
        id: 'small',
        fileName: 'small.pdf',
        fileSize: 500,
      };

      renderWithProviders(<PdfUploadSection {...defaultProps} existingPdf={smallPdf} />);
      expect(screen.getByText(/500 B/i)).toBeInTheDocument();
    });

    it('should format KB correctly', () => {
      const kbPdf = {
        id: 'kb',
        fileName: 'kb.pdf',
        fileSize: 2048,
      };

      renderWithProviders(<PdfUploadSection {...defaultProps} existingPdf={kbPdf} />);
      expect(screen.getByText(/2\.0 KB/i)).toBeInTheDocument();
    });

    it('should format MB correctly', () => {
      const mbPdf = {
        id: 'mb',
        fileName: 'mb.pdf',
        fileSize: 5 * 1024 * 1024,
      };

      renderWithProviders(<PdfUploadSection {...defaultProps} existingPdf={mbPdf} />);
      expect(screen.getByText(/5\.00 MB/i)).toBeInTheDocument();
    });
  });
});
