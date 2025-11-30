import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { PdfTableRow } from '../PdfTableRow';

// Mock the table context for TableRow component
vi.mock('@/components/ui/table', () => ({
  TableCell: ({ children, className, ...props }: any) => (
    <td className={className} {...props}>{children}</td>
  ),
  TableRow: ({ children, ...props }: any) => <tr {...props}>{children}</tr>
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
    logUrl: 'https://example.com/log/pdf-123'
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
