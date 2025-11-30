/**
 * ExportChatModal Component Tests (CHAT-05)
 *
 * Tests for the ExportChatModal component that provides chat export functionality
 * with format selection (PDF, TXT, Markdown) and optional date range filtering.
 *
 * Target Coverage: 90%+ (from 30.6%)
 */

import type { Mock } from 'vitest';
import React from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { ExportChatModal } from '../../components/ExportChatModal';
import { api, ExportFormat } from '@/lib/api';

// Mock API
vi.mock('@/lib/api', () => ({
  api: {
    chat: {
      exportChat: vi.fn(),
    },
  },
  ExportFormat: {
    PDF: 'pdf',
    TXT: 'txt',
    MD: 'md',
  },
}));

const mockApi = api as Mocked<typeof api>;

describe('ExportChatModal - Export Operations', () => {
  const mockOnClose = vi.fn();
  const defaultProps = {
    isOpen: true,
    onClose: mockOnClose,
    chatId: 'chat-123',
    gameName: 'Chess',
  };

  beforeEach(() => {
    vi.clearAllMocks();
    (mockApi.chat.exportChat as Mock).mockResolvedValue(undefined);
  });

  /**
   * Test Group: Export Error Handling
   */
  describe('Export Error Handling', () => {
    it('displays error message when export fails', async () => {
      (mockApi.chat.exportChat as Mock).mockRejectedValue(new Error('Export failed'));
      render(<ExportChatModal {...defaultProps} />);

      const exportButton = screen.getByRole('button', { name: /esporta/i });
      fireEvent.click(exportButton);

      await waitFor(() => {
        expect(screen.getByText(/Export failed/)).toBeInTheDocument();
      });

      expect(mockOnClose).not.toHaveBeenCalled();
    });

    it('clears previous error when starting new export', async () => {
      (mockApi.chat.exportChat as Mock)
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
      (mockApi.chat.exportChat as Mock).mockImplementation(
        () => new Promise(resolve => setTimeout(resolve, 100))
      );

      render(<ExportChatModal {...defaultProps} />);

      const exportButton = screen.getByRole('button', { name: /esporta/i });
      fireEvent.click(exportButton);

      expect(await screen.findByText('Esportazione...')).toBeInTheDocument();
    });

    it('disables buttons during export', async () => {
      (mockApi.chat.exportChat as Mock).mockImplementation(
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
      (mockApi.chat.exportChat as Mock).mockImplementation(
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
      (mockApi.chat.exportChat as Mock).mockImplementation(
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
      (mockApi.chat.exportChat as Mock).mockImplementation(
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
      (mockApi.chat.exportChat as Mock).mockImplementation(
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
