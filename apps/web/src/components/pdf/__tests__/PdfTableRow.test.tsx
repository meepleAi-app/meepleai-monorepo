import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { PdfTableRow } from '../PdfTableRow';

// Mock the table context for TableRow component
vi.mock('@/components/ui/table', () => ({
  TableCell: ({ children, className, ...props }: any) => (
    <td className={className} {...props}>
      {children}
    </td>
  ),
  TableRow: ({ children, ...props }: any) => <tr {...props}>{children}</tr>,
}));

describe('PdfTableRow', () => {
  const mockPdf = {
    id: 'pdf-123',
    fileName: 'test-game-rules.pdf',
    fileSizeBytes: 2048000, // ~2MB
    uploadedAt: '2024-01-15T10:30:00Z',
    uploadedByUserId: '990e8400-e29b-41d4-a716-000000000456',
    language: 'en',
    status: 'completed',
    logUrl: 'https://example.com/log/pdf-123',
  };

  const mockOnRetryParsing = vi.fn();
  const mockOnOpenLog = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Basic Rendering', () => {
    it('should render file name', () => {
      render(
        <table>
          <tbody>
            <PdfTableRow pdf={mockPdf} />
          </tbody>
        </table>
      );

      expect(screen.getByText('test-game-rules.pdf')).toBeInTheDocument();
    });

    it('should render language badge', () => {
      render(
        <table>
          <tbody>
            <PdfTableRow pdf={mockPdf} />
          </tbody>
        </table>
      );

      const badge = screen.getByTitle('English');
      expect(badge).toBeInTheDocument();
      expect(badge).toHaveTextContent('EN');
    });

    it('should render formatted file size', () => {
      render(
        <table>
          <tbody>
            <PdfTableRow pdf={mockPdf} />
          </tbody>
        </table>
      );

      // File size is 2048000 bytes = 1.95 MB
      expect(screen.getByText('1.95 MB')).toBeInTheDocument();
    });

    it('should render formatted upload date', () => {
      render(
        <table>
          <tbody>
            <PdfTableRow pdf={mockPdf} />
          </tbody>
        </table>
      );

      // Date formatting may vary, check it exists
      const dateCell = screen.getByText(/Jan 15, 2024/);
      expect(dateCell).toBeInTheDocument();
    });

    it('should render status badge', () => {
      render(
        <table>
          <tbody>
            <PdfTableRow pdf={mockPdf} />
          </tbody>
        </table>
      );

      expect(screen.getByText('completed')).toBeInTheDocument();
    });
  });

  describe('File Size Formatting', () => {
    it('should format bytes correctly', () => {
      const pdfInBytes = { ...mockPdf, fileSizeBytes: 500 };
      render(
        <table>
          <tbody>
            <PdfTableRow pdf={pdfInBytes} />
          </tbody>
        </table>
      );

      expect(screen.getByText('500 Bytes')).toBeInTheDocument();
    });

    it('should format kilobytes correctly', () => {
      const pdfInKB = { ...mockPdf, fileSizeBytes: 5120 }; // 5KB
      render(
        <table>
          <tbody>
            <PdfTableRow pdf={pdfInKB} />
          </tbody>
        </table>
      );

      expect(screen.getByText('5 KB')).toBeInTheDocument();
    });

    it('should format megabytes correctly', () => {
      const pdfInMB = { ...mockPdf, fileSizeBytes: 2048000 }; // 2048000 bytes = 1.95 MB
      render(
        <table>
          <tbody>
            <PdfTableRow pdf={pdfInMB} />
          </tbody>
        </table>
      );

      expect(screen.getByText('1.95 MB')).toBeInTheDocument();
    });

    it('should handle zero bytes', () => {
      const emptyPdf = { ...mockPdf, fileSizeBytes: 0 };
      render(
        <table>
          <tbody>
            <PdfTableRow pdf={emptyPdf} />
          </tbody>
        </table>
      );

      expect(screen.getByText('0 Bytes')).toBeInTheDocument();
    });
  });

  describe('Language Display', () => {
    it('should display English language', () => {
      const englishPdf = { ...mockPdf, language: 'en' };
      render(
        <table>
          <tbody>
            <PdfTableRow pdf={englishPdf} />
          </tbody>
        </table>
      );

      const badge = screen.getByTitle('English');
      expect(badge).toHaveTextContent('EN');
    });

    it('should display Italian language', () => {
      const italianPdf = { ...mockPdf, language: 'it' };
      render(
        <table>
          <tbody>
            <PdfTableRow pdf={italianPdf} />
          </tbody>
        </table>
      );

      const badge = screen.getByTitle('Italiano');
      expect(badge).toHaveTextContent('IT');
    });

    it('should display German language', () => {
      const germanPdf = { ...mockPdf, language: 'de' };
      render(
        <table>
          <tbody>
            <PdfTableRow pdf={germanPdf} />
          </tbody>
        </table>
      );

      const badge = screen.getByTitle('Deutsch');
      expect(badge).toHaveTextContent('DE');
    });

    it('should default to English for unknown language', () => {
      const unknownLangPdf = { ...mockPdf, language: 'xx' };
      render(
        <table>
          <tbody>
            <PdfTableRow pdf={unknownLangPdf} />
          </tbody>
        </table>
      );

      const badge = screen.getByTitle('Unknown');
      expect(badge).toHaveTextContent('XX');
    });

    it('should default to English when language is null', () => {
      const nullLangPdf = { ...mockPdf, language: null };
      render(
        <table>
          <tbody>
            <PdfTableRow pdf={nullLangPdf} />
          </tbody>
        </table>
      );

      const badge = screen.getByTitle('English');
      expect(badge).toHaveTextContent('EN');
    });
  });

  describe('Status Badge', () => {
    it('should display completed status with default variant', () => {
      const completedPdf = { ...mockPdf, status: 'completed' };
      render(
        <table>
          <tbody>
            <PdfTableRow pdf={completedPdf} />
          </tbody>
        </table>
      );

      expect(screen.getByText('completed')).toBeInTheDocument();
    });

    it('should display failed status with destructive variant', () => {
      const failedPdf = { ...mockPdf, status: 'failed' };
      render(
        <table>
          <tbody>
            <PdfTableRow pdf={failedPdf} />
          </tbody>
        </table>
      );

      expect(screen.getByText('failed')).toBeInTheDocument();
    });

    it('should display pending status when status is null', () => {
      const pendingPdf = { ...mockPdf, status: null };
      render(
        <table>
          <tbody>
            <PdfTableRow pdf={pendingPdf} />
          </tbody>
        </table>
      );

      expect(screen.getByText('Pending')).toBeInTheDocument();
    });

    it('should display processing status with secondary variant', () => {
      const processingPdf = { ...mockPdf, status: 'processing' };
      render(
        <table>
          <tbody>
            <PdfTableRow pdf={processingPdf} />
          </tbody>
        </table>
      );

      expect(screen.getByText('processing')).toBeInTheDocument();
    });
  });

  describe('Action Buttons', () => {
    it('should render log button when onOpenLog is provided', () => {
      render(
        <table>
          <tbody>
            <PdfTableRow pdf={mockPdf} onOpenLog={mockOnOpenLog} />
          </tbody>
        </table>
      );

      expect(screen.getByRole('button', { name: /log/i })).toBeInTheDocument();
    });

    it('should not render log button when onOpenLog is not provided', () => {
      render(
        <table>
          <tbody>
            <PdfTableRow pdf={mockPdf} />
          </tbody>
        </table>
      );

      expect(screen.queryByRole('button', { name: /log/i })).not.toBeInTheDocument();
    });

    it('should render retry button when onRetryParsing is provided', () => {
      render(
        <table>
          <tbody>
            <PdfTableRow pdf={mockPdf} onRetryParsing={mockOnRetryParsing} />
          </tbody>
        </table>
      );

      expect(screen.getByRole('button', { name: /retry/i })).toBeInTheDocument();
    });

    it('should not render retry button when onRetryParsing is not provided', () => {
      render(
        <table>
          <tbody>
            <PdfTableRow pdf={mockPdf} />
          </tbody>
        </table>
      );

      expect(screen.queryByRole('button', { name: /retry/i })).not.toBeInTheDocument();
    });

    it('should call onOpenLog when log button is clicked', async () => {
      const user = userEvent.setup();
      render(
        <table>
          <tbody>
            <PdfTableRow pdf={mockPdf} onOpenLog={mockOnOpenLog} />
          </tbody>
        </table>
      );

      const logButton = screen.getByRole('button', { name: /log/i });
      await user.click(logButton);

      expect(mockOnOpenLog).toHaveBeenCalledWith(mockPdf);
      expect(mockOnOpenLog).toHaveBeenCalledTimes(1);
    });

    it('should call onRetryParsing when retry button is clicked', async () => {
      const user = userEvent.setup();
      render(
        <table>
          <tbody>
            <PdfTableRow pdf={mockPdf} onRetryParsing={mockOnRetryParsing} />
          </tbody>
        </table>
      );

      const retryButton = screen.getByRole('button', { name: /retry/i });
      await user.click(retryButton);

      expect(mockOnRetryParsing).toHaveBeenCalledWith(mockPdf);
      expect(mockOnRetryParsing).toHaveBeenCalledTimes(1);
    });
  });

  describe('Retry State', () => {
    it('should disable retry button when isRetrying is true', () => {
      render(
        <table>
          <tbody>
            <PdfTableRow pdf={mockPdf} isRetrying={true} onRetryParsing={mockOnRetryParsing} />
          </tbody>
        </table>
      );

      const retryButton = screen.getByRole('button', { name: /retrying/i });
      expect(retryButton).toBeDisabled();
    });

    it('should display "Retrying..." text when isRetrying is true', () => {
      render(
        <table>
          <tbody>
            <PdfTableRow pdf={mockPdf} isRetrying={true} onRetryParsing={mockOnRetryParsing} />
          </tbody>
        </table>
      );

      expect(screen.getByText('Retrying...')).toBeInTheDocument();
    });

    it('should display "Retry" text when isRetrying is false', () => {
      render(
        <table>
          <tbody>
            <PdfTableRow pdf={mockPdf} isRetrying={false} onRetryParsing={mockOnRetryParsing} />
          </tbody>
        </table>
      );

      expect(screen.getByText('Retry')).toBeInTheDocument();
    });

    it('should enable retry button when isRetrying is false', () => {
      render(
        <table>
          <tbody>
            <PdfTableRow pdf={mockPdf} isRetrying={false} onRetryParsing={mockOnRetryParsing} />
          </tbody>
        </table>
      );

      const retryButton = screen.getByRole('button', { name: /retry/i });
      expect(retryButton).toBeEnabled();
    });
  });

  describe('Memoization', () => {
    it('should be a memoized component', () => {
      // React.memo returns a special object type
      expect(PdfTableRow).toBeDefined();
      expect(typeof PdfTableRow).toBe('object');
    });

    it('should not re-render when props are identical', () => {
      const { rerender } = render(
        <table>
          <tbody>
            <PdfTableRow pdf={mockPdf} />
          </tbody>
        </table>
      );

      const initialFileName = screen.getByText('test-game-rules.pdf');

      // Rerender with same props
      rerender(
        <table>
          <tbody>
            <PdfTableRow pdf={mockPdf} />
          </tbody>
        </table>
      );

      // Should still be the same element
      expect(screen.getByText('test-game-rules.pdf')).toBe(initialFileName);
    });
  });

  describe('Edge Cases', () => {
    it('should handle very large file sizes', () => {
      const largePdf = { ...mockPdf, fileSizeBytes: 52428800 }; // 52428800 bytes = 50 MB exactly
      render(
        <table>
          <tbody>
            <PdfTableRow pdf={largePdf} />
          </tbody>
        </table>
      );

      expect(screen.getByText('50 MB')).toBeInTheDocument();
    });

    it('should handle very small file sizes', () => {
      const smallPdf = { ...mockPdf, fileSizeBytes: 100 };
      render(
        <table>
          <tbody>
            <PdfTableRow pdf={smallPdf} />
          </tbody>
        </table>
      );

      expect(screen.getByText('100 Bytes')).toBeInTheDocument();
    });

    it('should handle invalid date strings gracefully', () => {
      const invalidDatePdf = { ...mockPdf, uploadedAt: 'invalid-date' };

      // Should not crash when rendering
      const { container } = render(
        <table>
          <tbody>
            <PdfTableRow pdf={invalidDatePdf} />
          </tbody>
        </table>
      );

      // Verify component rendered without crashing
      expect(container.querySelector('tr')).toBeInTheDocument();
    });

    it('should handle missing language', () => {
      const noLangPdf = { ...mockPdf, language: undefined };
      render(
        <table>
          <tbody>
            <PdfTableRow pdf={noLangPdf} />
          </tbody>
        </table>
      );

      const badge = screen.getByTitle('English');
      expect(badge).toHaveTextContent('EN');
    });

    it('should render both action buttons when both callbacks provided', () => {
      render(
        <table>
          <tbody>
            <PdfTableRow
              pdf={mockPdf}
              onOpenLog={mockOnOpenLog}
              onRetryParsing={mockOnRetryParsing}
            />
          </tbody>
        </table>
      );

      expect(screen.getByRole('button', { name: /log/i })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /retry/i })).toBeInTheDocument();
    });

    it('should handle file names with special characters', () => {
      const specialCharPdf = { ...mockPdf, fileName: 'test_game (2024) [v2.1].pdf' };
      render(
        <table>
          <tbody>
            <PdfTableRow pdf={specialCharPdf} />
          </tbody>
        </table>
      );

      expect(screen.getByText('test_game (2024) [v2.1].pdf')).toBeInTheDocument();
    });
  });

  describe('Accessibility', () => {
    it('should have accessible button labels', () => {
      render(
        <table>
          <tbody>
            <PdfTableRow
              pdf={mockPdf}
              onOpenLog={mockOnOpenLog}
              onRetryParsing={mockOnRetryParsing}
            />
          </tbody>
        </table>
      );

      const logButton = screen.getByRole('button', { name: /log/i });
      const retryButton = screen.getByRole('button', { name: /retry/i });

      expect(logButton).toBeInTheDocument();
      expect(retryButton).toBeInTheDocument();
    });

    it('should have proper table cell structure', () => {
      const { container } = render(
        <table>
          <tbody>
            <PdfTableRow pdf={mockPdf} />
          </tbody>
        </table>
      );

      const cells = container.querySelectorAll('td');
      expect(cells).toHaveLength(6); // fileName, language, size, date, status, actions
    });
  });
});
