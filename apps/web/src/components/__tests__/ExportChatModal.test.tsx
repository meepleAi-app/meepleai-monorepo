/**
 * ExportChatModal Tests (FE-IMP-006)
 *
 * Tests for ExportChatModal component with React Hook Form + Zod.
 */

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { axe, toHaveNoViolations } from 'jest-axe';
import { ExportChatModal } from '../ExportChatModal';
import * as chatActions from '@/actions/chat';

expect.extend(toHaveNoViolations);

jest.mock('@/actions/chat', () => ({
  exportChatAction: jest.fn(),
}));

describe('ExportChatModal', () => {
  const mockOnClose = jest.fn();
  const mockExportChatAction = chatActions.exportChatAction as jest.MockedFunction<
    typeof chatActions.exportChatAction
  >;

  const defaultProps = {
    isOpen: true,
    onClose: mockOnClose,
    chatId: 'test-chat-123',
    gameName: 'Catan',
  };

  beforeEach(() => {
    jest.clearAllMocks();
  });

  describe('Rendering', () => {
    it('should render modal when open', () => {
      render(<ExportChatModal {...defaultProps} />);

      expect(screen.getByText(/esporta chat/i)).toBeInTheDocument();
      expect(screen.getByText(/esporta la conversazione per catan/i)).toBeInTheDocument();
    });

    it('should render all format options', () => {
      render(<ExportChatModal {...defaultProps} />);

      expect(screen.getByLabelText(/pdf/i)).toBeInTheDocument();
      expect(screen.getByLabelText(/testo \(txt\)/i)).toBeInTheDocument();
      expect(screen.getByLabelText(/markdown/i)).toBeInTheDocument();
    });

    it('should render date range filters', () => {
      render(<ExportChatModal {...defaultProps} />);

      expect(screen.getByLabelText(/^da$/i)).toBeInTheDocument();
      expect(screen.getByLabelText(/^a$/i)).toBeInTheDocument();
    });

    it('should have PDF selected by default', () => {
      render(<ExportChatModal {...defaultProps} />);

      const pdfRadio = screen.getByLabelText(/pdf/i) as HTMLInputElement;
      expect(pdfRadio).toBeChecked();
    });
  });

  describe('Format Selection', () => {
    it('should allow changing export format', async () => {
      const user = userEvent.setup();
      render(<ExportChatModal {...defaultProps} />);

      const txtRadio = screen.getByLabelText(/testo \(txt\)/i) as HTMLInputElement;
      await user.click(txtRadio);

      expect(txtRadio).toBeChecked();
    });
  });

  describe('Form Submission', () => {
    it('should call exportChatAction with correct data', async () => {
      const user = userEvent.setup();
      mockExportChatAction.mockResolvedValue({ success: true });

      render(<ExportChatModal {...defaultProps} />);

      const submitButton = screen.getByRole('button', { name: /^esporta$/i });
      await user.click(submitButton);

      await waitFor(() => {
        expect(mockExportChatAction).toHaveBeenCalled();
        const formData = mockExportChatAction.mock.calls[0][1] as FormData;
        expect(formData.get('chatId')).toBe('test-chat-123');
        expect(formData.get('format')).toBe('pdf');
        // Optional date fields should not be in FormData if empty
        expect(formData.has('dateFrom')).toBe(false);
        expect(formData.has('dateTo')).toBe(false);
      });
    });

    it('should include date range when provided', async () => {
      const user = userEvent.setup();
      mockExportChatAction.mockResolvedValue({ success: true });

      render(<ExportChatModal {...defaultProps} />);

      const dateFromInput = screen.getByLabelText(/^da$/i) as HTMLInputElement;
      const dateToInput = screen.getByLabelText(/^a$/i) as HTMLInputElement;

      // Use clear + type for date inputs
      await user.clear(dateFromInput);
      await user.type(dateFromInput, '2025-01-01');

      await user.clear(dateToInput);
      await user.type(dateToInput, '2025-01-31');

      const submitButton = screen.getByRole('button', { name: /^esporta$/i });
      await user.click(submitButton);

      await waitFor(() => {
        expect(mockExportChatAction).toHaveBeenCalled();
        const formData = mockExportChatAction.mock.calls[0][1] as FormData;
        // Check if dates are included when filled
        expect(formData.has('dateFrom')).toBe(true);
        expect(formData.has('dateTo')).toBe(true);
      });
    });

    it('should close modal on successful export', async () => {
      const user = userEvent.setup();
      mockExportChatAction.mockResolvedValue({ success: true });

      render(<ExportChatModal {...defaultProps} />);

      const submitButton = screen.getByRole('button', { name: /^esporta$/i });
      await user.click(submitButton);

      await waitFor(() => {
        expect(mockOnClose).toHaveBeenCalled();
      });
    });

    it('should show error message on export failure', async () => {
      const user = userEvent.setup();
      mockExportChatAction.mockResolvedValue({
        success: false,
        error: { type: 'auth' as const, message: 'Esportazione fallita' },
      });

      render(<ExportChatModal {...defaultProps} />);

      const submitButton = screen.getByRole('button', { name: /^esporta$/i });
      await user.click(submitButton);

      await waitFor(() => {
        expect(screen.getByText(/esportazione fallita/i)).toBeInTheDocument();
      });
    });
  });

  describe('Cancel Button', () => {
    it('should close modal when cancel is clicked', async () => {
      const user = userEvent.setup();
      render(<ExportChatModal {...defaultProps} />);

      const cancelButton = screen.getByRole('button', { name: /annulla/i });
      await user.click(cancelButton);

      expect(mockOnClose).toHaveBeenCalled();
    });

    it('should not close during submission', async () => {
      const user = userEvent.setup();
      mockExportChatAction.mockImplementation(() => new Promise(() => {})); // Never resolves

      render(<ExportChatModal {...defaultProps} />);

      const submitButton = screen.getByRole('button', { name: /^esporta$/i });
      await user.click(submitButton);

      await waitFor(() => {
        expect(screen.getByText(/esportazione\.\.\./i)).toBeInTheDocument();
      });

      const cancelButton = screen.getByRole('button', { name: /annulla/i });
      await user.click(cancelButton);

      expect(mockOnClose).not.toHaveBeenCalled();
    });
  });

  describe('Accessibility', () => {
    it('should have no accessibility violations', async () => {
      const { container } = render(<ExportChatModal {...defaultProps} />);
      const results = await axe(container);
      expect(results).toHaveNoViolations();
    });

    it('should announce errors with aria-live', async () => {
      const user = userEvent.setup();
      mockExportChatAction.mockResolvedValue({
        success: false,
        error: { type: 'auth' as const, message: 'Errore di esportazione' },
      });

      render(<ExportChatModal {...defaultProps} />);

      const submitButton = screen.getByRole('button', { name: /^esporta$/i });
      await user.click(submitButton);

      await waitFor(() => {
        const errorAlert = screen.getByRole('alert');
        expect(errorAlert).toHaveTextContent(/errore di esportazione/i);
      });
    });
  });
});
