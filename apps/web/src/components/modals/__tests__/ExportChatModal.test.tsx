/**
 * ExportChatModal Component Tests
 *
 * Comprehensive tests for the ExportChatModal component using Server Actions (RHF + Zod).
 * Tests cover rendering, format selection, form submission, error handling, and accessibility.
 *
 * Issue #1887 - Batch 12: Test rewrite to match Server Actions migration
 */

import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ExportChatModal } from '../ExportChatModal';
import { exportChatAction } from '@/actions/chat';
import type { ExportChatActionState } from '@/actions/chat';

// Mock Server Action
vi.mock('@/actions/chat', () => ({
  exportChatAction: vi.fn(),
}));

describe('ExportChatModal', () => {
  const mockOnClose = vi.fn();
  const defaultProps = {
    isOpen: true,
    onClose: mockOnClose,
    chatId: 'chat-123',
    gameName: 'Catan',
  };

  beforeEach(() => {
    vi.clearAllMocks();
    // Default successful response
    (exportChatAction as any).mockResolvedValue({ success: true });
  });

  /**
   * RENDERING TESTS
   */
  describe('Rendering', () => {
    it('renders modal when isOpen is true', () => {
      render(<ExportChatModal {...defaultProps} />);

      expect(screen.getByText('Esporta Chat')).toBeInTheDocument();
      expect(screen.getByText(/Esporta la conversazione per Catan/)).toBeInTheDocument();
    });

    it('does not render modal when isOpen is false', () => {
      render(<ExportChatModal {...defaultProps} isOpen={false} />);

      expect(screen.queryByText('Esporta Chat')).not.toBeInTheDocument();
    });

    it('displays game name in description', () => {
      render(<ExportChatModal {...defaultProps} gameName="Monopoly" />);

      expect(screen.getByText(/Esporta la conversazione per Monopoly/)).toBeInTheDocument();
    });

    it('renders all format options', () => {
      render(<ExportChatModal {...defaultProps} />);

      expect(screen.getByText('PDF')).toBeInTheDocument();
      expect(screen.getByText('Testo (TXT)')).toBeInTheDocument();
      expect(screen.getByText('Markdown')).toBeInTheDocument();
    });

    it('renders action buttons', () => {
      render(<ExportChatModal {...defaultProps} />);

      expect(screen.getByRole('button', { name: /annulla/i })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /esporta/i })).toBeInTheDocument();
    });

    it('renders date range section', () => {
      render(<ExportChatModal {...defaultProps} />);

      // Check for date filter section
      expect(screen.getByText(/filtro per data/i)).toBeInTheDocument();
    });
  });

  /**
   * FORMAT SELECTION TESTS
   */
  describe('Format Selection', () => {
    it('defaults to PDF format', () => {
      render(<ExportChatModal {...defaultProps} />);

      const pdfRadio = screen.getByRole('radio', {
        name: /pdf documento formattato per la stampa/i,
      });
      expect(pdfRadio).toBeChecked();
    });

    it('allows selecting TXT format', () => {
      render(<ExportChatModal {...defaultProps} />);

      const txtRadio = screen.getByRole('radio', {
        name: /testo \(txt\) testo semplice senza formattazione/i,
      });
      fireEvent.click(txtRadio);

      expect(txtRadio).toBeChecked();
    });

    it('allows selecting Markdown format', () => {
      render(<ExportChatModal {...defaultProps} />);

      const mdRadio = screen.getByRole('radio', {
        name: /markdown formato markdown per documentazione/i,
      });
      fireEvent.click(mdRadio);

      expect(mdRadio).toBeChecked();
    });
  });

  /**
   * FORM SUBMISSION TESTS
   */
  describe('Form Submission', () => {
    it('calls exportChatAction with correct data on submit', async () => {
      render(<ExportChatModal {...defaultProps} />);

      const exportButton = screen.getByRole('button', { name: /esporta/i });
      fireEvent.click(exportButton);

      await waitFor(() => {
        expect(exportChatAction).toHaveBeenCalled();
      });

      const callArgs = (exportChatAction as any).mock.calls[0];
      const formData = callArgs[1] as FormData;

      expect(formData.get('chatId')).toBe('chat-123');
      expect(formData.get('format')).toBe('pdf');
    });

    it('submits without date range when not specified', async () => {
      render(<ExportChatModal {...defaultProps} />);

      const exportButton = screen.getByRole('button', { name: /esporta/i });
      fireEvent.click(exportButton);

      await waitFor(() => {
        expect(exportChatAction).toHaveBeenCalled();
      });

      const formData = (exportChatAction as any).mock.calls[0][1] as FormData;
      // Date fields should not be present if not filled
      expect(formData.get('chatId')).toBe('chat-123');
      expect(formData.get('format')).toBeTruthy();
    });

    it('closes modal and resets form on successful export', async () => {
      render(<ExportChatModal {...defaultProps} />);

      const exportButton = screen.getByRole('button', { name: /esporta/i });
      fireEvent.click(exportButton);

      await waitFor(() => {
        expect(mockOnClose).toHaveBeenCalled();
      });
    });

    it('submits with selected format', async () => {
      render(<ExportChatModal {...defaultProps} />);

      // Select TXT format
      const txtRadio = screen.getByRole('radio', {
        name: /testo \(txt\) testo semplice senza formattazione/i,
      });
      fireEvent.click(txtRadio);

      const exportButton = screen.getByRole('button', { name: /esporta/i });
      fireEvent.click(exportButton);

      await waitFor(() => {
        expect(exportChatAction).toHaveBeenCalled();
      });

      const formData = (exportChatAction as any).mock.calls[0][1] as FormData;
      expect(formData.get('format')).toBe('txt');
    });
  });

  /**
   * ERROR HANDLING TESTS
   */
  describe('Error Handling', () => {
    it('displays error message when export fails', async () => {
      (exportChatAction as any).mockResolvedValue({
        success: false,
        error: { message: 'Errore di rete' },
      });

      render(<ExportChatModal {...defaultProps} />);

      const exportButton = screen.getByRole('button', { name: /esporta/i });
      fireEvent.click(exportButton);

      await waitFor(() => {
        expect(screen.getByRole('alert')).toBeInTheDocument();
        expect(screen.getByText(/errore di rete/i)).toBeInTheDocument();
      });
    });

    it('does not close modal when export fails', async () => {
      (exportChatAction as any).mockResolvedValue({
        success: false,
        error: { message: 'Errore' },
      });

      render(<ExportChatModal {...defaultProps} />);

      const exportButton = screen.getByRole('button', { name: /esporta/i });
      fireEvent.click(exportButton);

      await waitFor(() => {
        expect(screen.getByRole('alert')).toBeInTheDocument();
      });

      expect(mockOnClose).not.toHaveBeenCalled();
    });

    it('displays generic error for exceptions', async () => {
      (exportChatAction as any).mockRejectedValue(new Error('Network error'));

      render(<ExportChatModal {...defaultProps} />);

      const exportButton = screen.getByRole('button', { name: /esporta/i });
      fireEvent.click(exportButton);

      await waitFor(() => {
        expect(screen.getByRole('alert')).toBeInTheDocument();
        expect(screen.getByText(/network error/i)).toBeInTheDocument();
      });
    });
  });

  /**
   * LOADING STATE TESTS
   */
  describe('Loading State', () => {
    it('shows loading state during export', async () => {
      // Delay the resolution to capture loading state
      (exportChatAction as any).mockImplementation(
        () => new Promise(resolve => setTimeout(() => resolve({ success: true }), 100))
      );

      render(<ExportChatModal {...defaultProps} />);

      const exportButton = screen.getByRole('button', { name: /esporta/i });
      fireEvent.click(exportButton);

      // Check loading text appears
      await waitFor(() => {
        expect(screen.getByText(/esportazione/i)).toBeInTheDocument();
      });
    });

    it('disables buttons during export', async () => {
      (exportChatAction as any).mockImplementation(
        () => new Promise(resolve => setTimeout(() => resolve({ success: true }), 100))
      );

      render(<ExportChatModal {...defaultProps} />);

      const exportButton = screen.getByRole('button', { name: /esporta/i });
      fireEvent.click(exportButton);

      await waitFor(() => {
        expect(exportButton).toBeDisabled();
      });
    });
  });

  /**
   * MODAL BEHAVIOR TESTS
   */
  describe('Modal Behavior', () => {
    it('calls onClose when cancel button is clicked', () => {
      render(<ExportChatModal {...defaultProps} />);

      const cancelButton = screen.getByRole('button', { name: /annulla/i });
      fireEvent.click(cancelButton);

      expect(mockOnClose).toHaveBeenCalled();
    });

    it('resets form when modal closes', async () => {
      const { rerender } = render(<ExportChatModal {...defaultProps} />);

      // Change format to TXT
      const txtRadio = screen.getByRole('radio', {
        name: /testo \(txt\) testo semplice senza formattazione/i,
      });
      fireEvent.click(txtRadio);
      expect(txtRadio).toBeChecked();

      // Close modal
      rerender(<ExportChatModal {...defaultProps} isOpen={false} />);

      // Reopen modal
      rerender(<ExportChatModal {...defaultProps} isOpen={true} />);

      // Should be back to PDF default
      const pdfRadio = screen.getByRole('radio', {
        name: /pdf documento formattato per la stampa/i,
      });
      expect(pdfRadio).toBeChecked();
    });
  });

  /**
   * ACCESSIBILITY TESTS
   */
  describe('Accessibility', () => {
    it('has accessible dialog role', () => {
      render(<ExportChatModal {...defaultProps} />);

      expect(screen.getByRole('dialog')).toBeInTheDocument();
    });

    it('has accessible modal title', () => {
      render(<ExportChatModal {...defaultProps} />);

      expect(screen.getByText('Esporta Chat')).toBeInTheDocument();
    });

    it('has accessible form labels', () => {
      render(<ExportChatModal {...defaultProps} />);

      // Check for main form sections
      expect(screen.getByText('Formato di esportazione')).toBeInTheDocument();
      expect(screen.getByText(/filtro per data/i)).toBeInTheDocument();
    });

    it('uses role="alert" for error messages', async () => {
      (exportChatAction as any).mockResolvedValue({
        success: false,
        error: { message: 'Errore' },
      });

      render(<ExportChatModal {...defaultProps} />);

      const exportButton = screen.getByRole('button', { name: /esporta/i });
      fireEvent.click(exportButton);

      await waitFor(() => {
        expect(screen.getByRole('alert')).toBeInTheDocument();
      });
    });
  });
});
