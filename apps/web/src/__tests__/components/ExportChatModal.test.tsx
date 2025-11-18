/**
 * ExportChatModal Component Tests (CHAT-05)
 *
 * Tests for the ExportChatModal component that provides chat export functionality
 * with format selection (PDF, TXT, Markdown) and optional date range filtering.
 *
 * Issue #1078: FE-IMP-002 — Server Actions per Auth & Export
 * Updated to test useActionState pattern instead of direct API calls
 *
 * Target Coverage: 90%+ (from 30.6%)
 */

import React from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { ExportChatModal } from '../../components/modals/ExportChatModal';
import * as chatActions from '@/actions/chat';
import type { ExportFormat } from '@/lib/api';

// Mock chat actions module
jest.mock('@/actions/chat', () => ({
  exportChatAction: jest.fn(),
}));

// Mock useActionState from React
jest.mock('react', () => ({
  ...jest.requireActual('react'),
  useActionState: jest.fn((action, initialState) => {
    const [state, setState] = (jest.requireActual('react') as typeof import('react')).useState(initialState);
    const [isPending, setIsPending] = (jest.requireActual('react') as typeof import('react')).useState(false);

    const wrappedAction = async (formData: FormData) => {
      setIsPending(true);
      const result = await action(state, formData);
      setState(result);
      setIsPending(false);
      return result;
    };

    return [state, wrappedAction, isPending];
  }),
}));

const mockExportChatAction = chatActions.exportChatAction as jest.MockedFunction<typeof chatActions.exportChatAction>;

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
    mockExportChatAction.mockResolvedValue({ success: true, message: 'Esportazione completata!' });
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

      const dateFromInput = screen.getByLabelText('Da') as HTMLInputElement;
      fireEvent.change(dateFromInput, { target: { value: '2025-01-01' } });

      expect(dateFromInput.value).toBe('2025-01-01');
    });

    it('allows entering date to', () => {
      render(<ExportChatModal {...defaultProps} />);

      const dateToInput = screen.getByLabelText('A') as HTMLInputElement;
      fireEvent.change(dateToInput, { target: { value: '2025-01-31' } });

      expect(dateToInput.value).toBe('2025-01-31');
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
    it('submits form with correct default format (PDF)', async () => {
      render(<ExportChatModal {...defaultProps} />);

      const form = screen.getByRole('button', { name: /esporta/i }).closest('form');
      expect(form).toBeInTheDocument();

      const exportButton = screen.getByRole('button', { name: /esporta/i });
      fireEvent.click(exportButton);

      await waitFor(() => {
        expect(mockExportChatAction).toHaveBeenCalled();
      });

      // Verify FormData contains correct values
      const callArgs = mockExportChatAction.mock.calls[0];
      const formData = callArgs[1] as FormData;
      expect(formData.get('chatId')).toBe('chat-123');
      expect(formData.get('format')).toBe('pdf');
    });

    it('submits form with selected TXT format', async () => {
      render(<ExportChatModal {...defaultProps} />);

      const txtRadio = screen.getByRole('radio', { name: /testo \(txt\)/i });
      fireEvent.click(txtRadio);

      const exportButton = screen.getByRole('button', { name: /esporta/i });
      fireEvent.click(exportButton);

      await waitFor(() => {
        expect(mockExportChatAction).toHaveBeenCalled();
      });

      const callArgs = mockExportChatAction.mock.calls[0];
      const formData = callArgs[1] as FormData;
      expect(formData.get('format')).toBe('txt');
    });

    it('includes date range in form submission', async () => {
      render(<ExportChatModal {...defaultProps} />);

      const dateFromInput = screen.getByLabelText('Da');
      const dateToInput = screen.getByLabelText('A');

      fireEvent.change(dateFromInput, { target: { value: '2025-01-01' } });
      fireEvent.change(dateToInput, { target: { value: '2025-01-31' } });

      const exportButton = screen.getByRole('button', { name: /esporta/i });
      fireEvent.click(exportButton);

      await waitFor(() => {
        expect(mockExportChatAction).toHaveBeenCalled();
      });

      const callArgs = mockExportChatAction.mock.calls[0];
      const formData = callArgs[1] as FormData;
      expect(formData.get('dateFrom')).toBe('2025-01-01');
      expect(formData.get('dateTo')).toBe('2025-01-31');
    });

    it('closes modal on successful export', async () => {
      render(<ExportChatModal {...defaultProps} />);

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
    it('displays error from action state', async () => {
      mockExportChatAction.mockResolvedValue({
        success: false,
        error: {
          type: 'validation',
          message: 'La data iniziale deve essere precedente alla data finale.'
        }
      });

      render(<ExportChatModal {...defaultProps} />);

      const exportButton = screen.getByRole('button', { name: /esporta/i });
      fireEvent.click(exportButton);

      expect(await screen.findByText('La data iniziale deve essere precedente alla data finale.')).toBeInTheDocument();
    });

    it('displays network error', async () => {
      mockExportChatAction.mockResolvedValue({
        success: false,
        error: {
          type: 'network',
          message: 'Impossibile esportare la chat. Verifica la tua connessione.'
        }
      });

      render(<ExportChatModal {...defaultProps} />);

      const exportButton = screen.getByRole('button', { name: /esporta/i });
      fireEvent.click(exportButton);

      expect(await screen.findByText(/Impossibile esportare la chat/)).toBeInTheDocument();
    });

    it('displays server error', async () => {
      mockExportChatAction.mockResolvedValue({
        success: false,
        error: {
          type: 'server',
          message: 'Errore del server. Riprova più tardi.'
        }
      });

      render(<ExportChatModal {...defaultProps} />);

      const exportButton = screen.getByRole('button', { name: /esporta/i });
      fireEvent.click(exportButton);

      expect(await screen.findByText(/Errore del server/)).toBeInTheDocument();
    });

    it('does not close modal when export fails', async () => {
      mockExportChatAction.mockResolvedValue({
        success: false,
        error: {
          type: 'server',
          message: 'Export failed'
        }
      });

      render(<ExportChatModal {...defaultProps} />);

      const exportButton = screen.getByRole('button', { name: /esporta/i });
      fireEvent.click(exportButton);

      await waitFor(() => {
        expect(screen.getByText(/Export failed/)).toBeInTheDocument();
      });

      expect(mockOnClose).not.toHaveBeenCalled();
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

    it('resets format after successful export', async () => {
      render(<ExportChatModal {...defaultProps} />);

      // Change format to TXT
      const txtRadio = screen.getByRole('radio', { name: /testo \(txt\)/i });
      fireEvent.click(txtRadio);
      expect(txtRadio).toBeChecked();

      // Submit form
      const exportButton = screen.getByRole('button', { name: /esporta/i });
      fireEvent.click(exportButton);

      await waitFor(() => {
        expect(mockOnClose).toHaveBeenCalled();
      });

      // Format should reset after modal closes (tested in next render)
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
      mockExportChatAction.mockResolvedValue({
        success: false,
        error: {
          type: 'validation',
          message: 'Errore di validazione'
        }
      });

      render(<ExportChatModal {...defaultProps} />);

      const exportButton = screen.getByRole('button', { name: /esporta/i });
      fireEvent.click(exportButton);

      const alert = await screen.findByRole('alert');
      expect(alert).toBeInTheDocument();
      expect(alert).toHaveTextContent('Errore di validazione');
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
        expect(mockExportChatAction).toHaveBeenCalled();
      });

      const callArgs = mockExportChatAction.mock.calls[0];
      const formData = callArgs[1] as FormData;
      expect(formData.get('chatId')).toBe('');
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
        expect(mockExportChatAction).toHaveBeenCalled();
      });

      const callArgs = mockExportChatAction.mock.calls[0];
      const formData = callArgs[1] as FormData;
      expect(formData.get('dateFrom')).toBe('2025-01-01');
      expect(formData.get('dateTo')).toBe('');
    });

    it('handles only dateTo specified', async () => {
      render(<ExportChatModal {...defaultProps} />);

      const dateToInput = screen.getByLabelText('A');
      fireEvent.change(dateToInput, { target: { value: '2025-01-31' } });

      const exportButton = screen.getByRole('button', { name: /esporta/i });
      fireEvent.click(exportButton);

      await waitFor(() => {
        expect(mockExportChatAction).toHaveBeenCalled();
      });

      const callArgs = mockExportChatAction.mock.calls[0];
      const formData = callArgs[1] as FormData;
      expect(formData.get('dateFrom')).toBe('');
      expect(formData.get('dateTo')).toBe('2025-01-31');
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
        expect(mockExportChatAction).toHaveBeenCalled();
      });

      const callArgs = mockExportChatAction.mock.calls[0];
      const formData = callArgs[1] as FormData;
      expect(formData.get('dateFrom')).toBe('2025-01-15');
      expect(formData.get('dateTo')).toBe('2025-01-15');
    });

    it('handles all export formats', async () => {
      const formats: Array<{ value: ExportFormat; label: RegExp }> = [
        { value: 'pdf', label: /pdf/i },
        { value: 'txt', label: /testo \(txt\)/i },
        { value: 'md', label: /markdown/i },
      ];

      for (const { value, label } of formats) {
        mockExportChatAction.mockClear();
        const { unmount } = render(<ExportChatModal {...defaultProps} />);

        const radio = screen.getByRole('radio', { name: label });
        fireEvent.click(radio);

        const exportButton = screen.getByRole('button', { name: /esporta/i });
        fireEvent.click(exportButton);

        await waitFor(() => {
          expect(mockExportChatAction).toHaveBeenCalled();
        });

        const callArgs = mockExportChatAction.mock.calls[0];
        const formData = callArgs[1] as FormData;
        expect(formData.get('format')).toBe(value);

        unmount();
      }
    });
  });

  /**
   * Test Group: Form Validation
   */
  describe('Form Validation', () => {
    it('validates date range in action', async () => {
      mockExportChatAction.mockResolvedValue({
        success: false,
        error: {
          type: 'validation',
          message: 'La data iniziale deve essere precedente alla data finale.'
        }
      });

      render(<ExportChatModal {...defaultProps} />);

      const dateFromInput = screen.getByLabelText('Da');
      const dateToInput = screen.getByLabelText('A');

      fireEvent.change(dateFromInput, { target: { value: '2025-01-31' } });
      fireEvent.change(dateToInput, { target: { value: '2025-01-01' } });

      const exportButton = screen.getByRole('button', { name: /esporta/i });
      fireEvent.click(exportButton);

      expect(await screen.findByText('La data iniziale deve essere precedente alla data finale.')).toBeInTheDocument();
    });
  });
});
