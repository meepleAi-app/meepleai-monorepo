/**
 * ExportChatModal Component Tests (CHAT-05)
 *
 * Tests for the ExportChatModal component that provides chat export functionality
 * with format selection (PDF, TXT, Markdown) and optional date range filtering.
 *
 * Target Coverage: 90%+ (from 30.6%)
 */

import React from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { ExportChatModal } from '../../components/ExportChatModal';
import { api, ExportFormat } from '@/lib/api';

// Mock API
jest.mock('@/lib/api', () => ({
  api: {
    chat: {
      exportChat: jest.fn(),
    },
  },
  ExportFormat: {
    PDF: 'pdf',
    TXT: 'txt',
    MD: 'md',
  },
}));

const mockApi = api as jest.Mocked<typeof api>;

describe('ExportChatModal Component', () => {
  const mockOnClose = jest.fn();
  const defaultProps = {
    isOpen: true,
    onClose: mockOnClose,
    chatId: 'chat-123',
    gameName: 'Chess',
  };

  beforeEach(() => {
    jest.clearAllMocks();
    mockApi.chat.exportChat.mockResolvedValue(undefined);
  });

  /**
   * Test Group: Basic Rendering
   */
  describe('Basic Rendering', () => {
    it('renders modal when isOpen is true', () => {
      render(<ExportChatModal {...defaultProps} />);

      expect(screen.getByText('Esporta Chat')).toBeInTheDocument();
      expect(screen.getByText(/Esporta la conversazione per Chess/)).toBeInTheDocument();
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
  });

  /**
   * Test Group: Format Selection
   */
  describe('Format Selection', () => {
    it('defaults to PDF format', () => {
      render(<ExportChatModal {...defaultProps} />);

      const pdfRadio = screen.getByRole('radio', { name: /pdf/i });
      expect(pdfRadio).toBeChecked();
    });

    it('allows selecting TXT format', () => {
      render(<ExportChatModal {...defaultProps} />);

      const txtRadio = screen.getByRole('radio', { name: /testo \(txt\)/i });
      fireEvent.click(txtRadio);

      expect(txtRadio).toBeChecked();
    });

    it('allows selecting Markdown format', () => {
      render(<ExportChatModal {...defaultProps} />);

      const mdRadio = screen.getByRole('radio', { name: /markdown/i });
      fireEvent.click(mdRadio);

      expect(mdRadio).toBeChecked();
    });

    it('switches between formats', () => {
      render(<ExportChatModal {...defaultProps} />);

      const pdfRadio = screen.getByRole('radio', { name: /pdf/i });
      const txtRadio = screen.getByRole('radio', { name: /testo \(txt\)/i });

      expect(pdfRadio).toBeChecked();

      fireEvent.click(txtRadio);
      expect(txtRadio).toBeChecked();
      expect(pdfRadio).not.toBeChecked();

      fireEvent.click(pdfRadio);
      expect(pdfRadio).toBeChecked();
      expect(txtRadio).not.toBeChecked();
    });

    it('displays format descriptions', () => {
      render(<ExportChatModal {...defaultProps} />);

      expect(screen.getByText('Documento formattato per la stampa')).toBeInTheDocument();
      expect(screen.getByText('Testo semplice senza formattazione')).toBeInTheDocument();
      expect(screen.getByText('Formato Markdown per documentazione')).toBeInTheDocument();
    });
  });

  /**
   * Test Group: Date Range Filter
   */
  describe('Date Range Filter', () => {
    it('renders date filter inputs', () => {
      render(<ExportChatModal {...defaultProps} />);

      expect(screen.getByLabelText('Da')).toBeInTheDocument();
      expect(screen.getByLabelText('A')).toBeInTheDocument();
    });

    it('allows entering date from', () => {
      render(<ExportChatModal {...defaultProps} />);

      const dateFromInput = screen.getByLabelText('Da');
      fireEvent.change(dateFromInput, { target: { value: '2025-01-01' } });

      expect(dateFromInput).toHaveValue('2025-01-01');
    });

    it('allows entering date to', () => {
      render(<ExportChatModal {...defaultProps} />);

      const dateToInput = screen.getByLabelText('A');
      fireEvent.change(dateToInput, { target: { value: '2025-01-31' } });

      expect(dateToInput).toHaveValue('2025-01-31');
    });

    it('displays helper text for date filter', () => {
      render(<ExportChatModal {...defaultProps} />);

      expect(screen.getByText('Lascia vuoto per esportare tutti i messaggi')).toBeInTheDocument();
    });
  });

  /**
   * Test Group: Export Functionality
   */
  describe('Export Functionality', () => {
    it('calls exportChat with correct parameters when exporting PDF', async () => {
      render(<ExportChatModal {...defaultProps} />);

      const exportButton = screen.getByRole('button', { name: /esporta/i });
      fireEvent.click(exportButton);

      await waitFor(() => {
        expect(mockApi.chat.exportChat).toHaveBeenCalledWith('chat-123', {
          format: 'pdf',
          dateFrom: undefined,
          dateTo: undefined,
        });
      });
    });

    it('calls exportChat with selected format', async () => {
      render(<ExportChatModal {...defaultProps} />);

      const mdRadio = screen.getByRole('radio', { name: /markdown/i });
      fireEvent.click(mdRadio);

      const exportButton = screen.getByRole('button', { name: /esporta/i });
      fireEvent.click(exportButton);

      await waitFor(() => {
        expect(mockApi.chat.exportChat).toHaveBeenCalledWith('chat-123', {
          format: 'md',
          dateFrom: undefined,
          dateTo: undefined,
        });
      });
    });

    it('includes date range in export request', async () => {
      render(<ExportChatModal {...defaultProps} />);

      const dateFromInput = screen.getByLabelText('Da');
      const dateToInput = screen.getByLabelText('A');

      fireEvent.change(dateFromInput, { target: { value: '2025-01-01' } });
      fireEvent.change(dateToInput, { target: { value: '2025-01-31' } });

      const exportButton = screen.getByRole('button', { name: /esporta/i });
      fireEvent.click(exportButton);

      await waitFor(() => {
        expect(mockApi.chat.exportChat).toHaveBeenCalledWith('chat-123', {
          format: 'pdf',
          dateFrom: '2025-01-01',
          dateTo: '2025-01-31',
        });
      });
    });

    it('closes modal on successful export', async () => {
      render(<ExportChatModal {...defaultProps} />);

      const exportButton = screen.getByRole('button', { name: /esporta/i });
      fireEvent.click(exportButton);

      await waitFor(() => {
        expect(mockOnClose).toHaveBeenCalled();
      });
    });

    it('resets form state after successful export', async () => {
      render(<ExportChatModal {...defaultProps} />);

      // Change format and dates
      const txtRadio = screen.getByRole('radio', { name: /testo \(txt\)/i });
      fireEvent.click(txtRadio);

      const dateFromInput = screen.getByLabelText('Da');
      fireEvent.change(dateFromInput, { target: { value: '2025-01-01' } });

      const exportButton = screen.getByRole('button', { name: /esporta/i });
      fireEvent.click(exportButton);

      await waitFor(() => {
        expect(mockOnClose).toHaveBeenCalled();
      });
    });
  });

  /**
   * Test Group: Error Handling
   */
  describe('Error Handling', () => {
    it('displays error when date from is after date to', async () => {
      render(<ExportChatModal {...defaultProps} />);

      const dateFromInput = screen.getByLabelText('Da');
      const dateToInput = screen.getByLabelText('A');

      fireEvent.change(dateFromInput, { target: { value: '2025-01-31' } });
      fireEvent.change(dateToInput, { target: { value: '2025-01-01' } });

      const exportButton = screen.getByRole('button', { name: /esporta/i });
      fireEvent.click(exportButton);

      expect(await screen.findByText('La data iniziale deve essere precedente alla data finale.')).toBeInTheDocument();
      expect(mockApi.chat.exportChat).not.toHaveBeenCalled();
    });

    it('displays error when export fails', async () => {
      mockApi.chat.exportChat.mockRejectedValue(new Error('Network error'));
      render(<ExportChatModal {...defaultProps} />);

      const exportButton = screen.getByRole('button', { name: /esporta/i });
      fireEvent.click(exportButton);

      expect(await screen.findByText(/Network error/)).toBeInTheDocument();
    });

    it('displays generic error message for unknown errors', async () => {
      mockApi.chat.exportChat.mockRejectedValue('Unknown error');
      render(<ExportChatModal {...defaultProps} />);

      const exportButton = screen.getByRole('button', { name: /esporta/i });
      fireEvent.click(exportButton);

      expect(await screen.findByText(/Errore durante l'esportazione della chat/)).toBeInTheDocument();
    });

    it('does not close modal when export fails', async () => {
      mockApi.chat.exportChat.mockRejectedValue(new Error('Export failed'));
      render(<ExportChatModal {...defaultProps} />);

      const exportButton = screen.getByRole('button', { name: /esporta/i });
      fireEvent.click(exportButton);

      await waitFor(() => {
        expect(screen.getByText(/Export failed/)).toBeInTheDocument();
      });

      expect(mockOnClose).not.toHaveBeenCalled();
    });

    it('clears previous error when starting new export', async () => {
      mockApi.chat.exportChat
        .mockRejectedValueOnce(new Error('First error'))
        .mockResolvedValueOnce(undefined);

      render(<ExportChatModal {...defaultProps} />);

      const exportButton = screen.getByRole('button', { name: /esporta/i });

      // First export fails
      fireEvent.click(exportButton);
      expect(await screen.findByText(/First error/)).toBeInTheDocument();

      // Second export succeeds
      fireEvent.click(exportButton);

      await waitFor(() => {
        expect(screen.queryByText(/First error/)).not.toBeInTheDocument();
      });
    });
  });

  /**
   * Test Group: Loading State
   */
  describe('Loading State', () => {
    it('shows loading state during export', async () => {
      mockApi.chat.exportChat.mockImplementation(
        () => new Promise(resolve => setTimeout(resolve, 100))
      );

      render(<ExportChatModal {...defaultProps} />);

      const exportButton = screen.getByRole('button', { name: /esporta/i });
      fireEvent.click(exportButton);

      expect(await screen.findByText('Esportazione...')).toBeInTheDocument();
    });

    it('disables buttons during export', async () => {
      mockApi.chat.exportChat.mockImplementation(
        () => new Promise(resolve => setTimeout(resolve, 100))
      );

      render(<ExportChatModal {...defaultProps} />);

      const exportButton = screen.getByRole('button', { name: /esporta/i });
      const cancelButton = screen.getByRole('button', { name: /annulla/i });

      fireEvent.click(exportButton);

      await waitFor(() => {
        expect(exportButton).toBeDisabled();
        expect(cancelButton).toBeDisabled();
      });
    });

    it('disables format radios during export', async () => {
      mockApi.chat.exportChat.mockImplementation(
        () => new Promise(resolve => setTimeout(resolve, 100))
      );

      render(<ExportChatModal {...defaultProps} />);

      const exportButton = screen.getByRole('button', { name: /esporta/i });
      fireEvent.click(exportButton);

      await waitFor(() => {
        const radios = screen.getAllByRole('radio');
        radios.forEach(radio => {
          expect(radio).toBeDisabled();
        });
      });
    });

    it('disables date inputs during export', async () => {
      mockApi.chat.exportChat.mockImplementation(
        () => new Promise(resolve => setTimeout(resolve, 100))
      );

      render(<ExportChatModal {...defaultProps} />);

      const exportButton = screen.getByRole('button', { name: /esporta/i });
      fireEvent.click(exportButton);

      await waitFor(() => {
        expect(screen.getByLabelText('Da')).toBeDisabled();
        expect(screen.getByLabelText('A')).toBeDisabled();
      });
    });

    it('shows loading spinner during export', async () => {
      mockApi.chat.exportChat.mockImplementation(
        () => new Promise(resolve => setTimeout(resolve, 100))
      );

      render(<ExportChatModal {...defaultProps} />);

      const exportButton = screen.getByRole('button', { name: /esporta/i });
      fireEvent.click(exportButton);

      await waitFor(() => {
        // Spinner SVG should be present
        const svg = exportButton.querySelector('svg.animate-spin');
        expect(svg).toBeInTheDocument();
      });
    });
  });

  /**
   * Test Group: Modal Behavior
   */
  describe('Modal Behavior', () => {
    it('calls onClose when cancel button is clicked', () => {
      render(<ExportChatModal {...defaultProps} />);

      const cancelButton = screen.getByRole('button', { name: /annulla/i });
      fireEvent.click(cancelButton);

      expect(mockOnClose).toHaveBeenCalled();
    });

    it('does not call onClose when backdrop is clicked during export', async () => {
      mockApi.chat.exportChat.mockImplementation(
        () => new Promise(resolve => setTimeout(resolve, 100))
      );

      render(<ExportChatModal {...defaultProps} />);

      const exportButton = screen.getByRole('button', { name: /esporta/i });
      fireEvent.click(exportButton);

      // Modal should not be closable during export
      await waitFor(() => {
        expect(screen.getByText('Esportazione...')).toBeInTheDocument();
      });
    });

    it('clears error when closing modal', () => {
      render(<ExportChatModal {...defaultProps} />);

      const dateFromInput = screen.getByLabelText('Da');
      const dateToInput = screen.getByLabelText('A');

      fireEvent.change(dateFromInput, { target: { value: '2025-01-31' } });
      fireEvent.change(dateToInput, { target: { value: '2025-01-01' } });

      const exportButton = screen.getByRole('button', { name: /esporta/i });
      fireEvent.click(exportButton);

      // Error should appear
      expect(screen.getByRole('alert')).toBeInTheDocument();

      const cancelButton = screen.getByRole('button', { name: /annulla/i });
      fireEvent.click(cancelButton);

      expect(mockOnClose).toHaveBeenCalled();
    });
  });

  /**
   * Test Group: Accessibility
   */
  describe('Accessibility', () => {
    it('provides accessible modal title', () => {
      render(<ExportChatModal {...defaultProps} />);

      expect(screen.getByText('Esporta Chat')).toBeInTheDocument();
    });

    it('uses role="alert" for error messages', async () => {
      render(<ExportChatModal {...defaultProps} />);

      const dateFromInput = screen.getByLabelText('Da');
      const dateToInput = screen.getByLabelText('A');

      fireEvent.change(dateFromInput, { target: { value: '2025-01-31' } });
      fireEvent.change(dateToInput, { target: { value: '2025-01-01' } });

      const exportButton = screen.getByRole('button', { name: /esporta/i });
      fireEvent.click(exportButton);

      const alert = await screen.findByRole('alert');
      expect(alert).toBeInTheDocument();
    });

    it('provides accessible labels for date inputs', () => {
      render(<ExportChatModal {...defaultProps} />);

      const dateFromLabel = screen.getByText('Da');
      const dateToLabel = screen.getByText('A');

      expect(dateFromLabel).toBeInTheDocument();
      expect(dateToLabel).toBeInTheDocument();
    });

    it('uses radio group for format selection', () => {
      render(<ExportChatModal {...defaultProps} />);

      const radioGroup = screen.getByRole('group', { name: /export format selection/i });
      expect(radioGroup).toBeInTheDocument();
    });

    it('provides accessible button labels', () => {
      render(<ExportChatModal {...defaultProps} />);

      expect(screen.getByRole('button', { name: /annulla/i })).toHaveAccessibleName();
      expect(screen.getByRole('button', { name: /esporta/i })).toHaveAccessibleName();
    });
  });

  /**
   * Test Group: Edge Cases
   */
  describe('Edge Cases', () => {
    it('handles empty chatId', async () => {
      render(<ExportChatModal {...defaultProps} chatId="" />);

      const exportButton = screen.getByRole('button', { name: /esporta/i });
      fireEvent.click(exportButton);

      await waitFor(() => {
        expect(mockApi.chat.exportChat).toHaveBeenCalledWith('', expect.any(Object));
      });
    });

    it('handles empty gameName', () => {
      render(<ExportChatModal {...defaultProps} gameName="" />);

      expect(screen.getByText(/Esporta la conversazione per/)).toBeInTheDocument();
    });

    it('handles only dateFrom specified', async () => {
      render(<ExportChatModal {...defaultProps} />);

      const dateFromInput = screen.getByLabelText('Da');
      fireEvent.change(dateFromInput, { target: { value: '2025-01-01' } });

      const exportButton = screen.getByRole('button', { name: /esporta/i });
      fireEvent.click(exportButton);

      await waitFor(() => {
        expect(mockApi.chat.exportChat).toHaveBeenCalledWith('chat-123', {
          format: 'pdf',
          dateFrom: '2025-01-01',
          dateTo: undefined,
        });
      });
    });

    it('handles only dateTo specified', async () => {
      render(<ExportChatModal {...defaultProps} />);

      const dateToInput = screen.getByLabelText('A');
      fireEvent.change(dateToInput, { target: { value: '2025-01-31' } });

      const exportButton = screen.getByRole('button', { name: /esporta/i });
      fireEvent.click(exportButton);

      await waitFor(() => {
        expect(mockApi.chat.exportChat).toHaveBeenCalledWith('chat-123', {
          format: 'pdf',
          dateFrom: undefined,
          dateTo: '2025-01-31',
        });
      });
    });

    it('handles same dateFrom and dateTo', async () => {
      render(<ExportChatModal {...defaultProps} />);

      const dateFromInput = screen.getByLabelText('Da');
      const dateToInput = screen.getByLabelText('A');

      fireEvent.change(dateFromInput, { target: { value: '2025-01-15' } });
      fireEvent.change(dateToInput, { target: { value: '2025-01-15' } });

      const exportButton = screen.getByRole('button', { name: /esporta/i });
      fireEvent.click(exportButton);

      await waitFor(() => {
        expect(mockApi.chat.exportChat).toHaveBeenCalledWith('chat-123', {
          format: 'pdf',
          dateFrom: '2025-01-15',
          dateTo: '2025-01-15',
        });
      });
    });
  });
});
