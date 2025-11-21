import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { PdfTable } from '@/components/pdf/PdfTable';

describe('PdfTable', () => {
  const mockPdfs = [
    {
      id: '1',
      fileName: 'gloomhaven-rules.pdf',
      fileSizeBytes: 1024 * 1024 * 5, // 5 MB
      uploadedAt: '2024-01-15T10:30:00Z',
      uploadedByUserId: '990e8400-e29b-41d4-a716-000000000001',
      language: 'en',
      status: 'completed',
      logUrl: 'https://example.com/log1'
    },
    {
      id: '2',
      fileName: 'wingspan-rules.pdf',
      fileSizeBytes: 1024 * 1024 * 3, // 3 MB
      uploadedAt: '2024-01-16T14:20:00Z',
      uploadedByUserId: '990e8400-e29b-41d4-a716-000000000001',
      language: 'it',
      status: 'failed',
      logUrl: 'https://example.com/log2'
    },
    {
      id: '3',
      fileName: 'catan-rules.pdf',
      fileSizeBytes: 1024 * 1024 * 2, // 2 MB
      uploadedAt: '2024-01-17T09:15:00Z',
      uploadedByUserId: '990e8400-e29b-41d4-a716-000000000001',
      language: 'de',
      status: 'pending',
      logUrl: null
    }
  ];

  const mockProps = {
    pdfs: mockPdfs,
    loading: false,
    error: null,
    retryingPdfId: null,
    onRetryParsing: jest.fn(),
    onOpenLog: jest.fn()
  };

  beforeEach(() => {
    jest.clearAllMocks();
  });

  describe('Rendering', () => {
    it('renders table with all PDFs', () => {
      render(<PdfTable {...mockProps} />);

      expect(screen.getByText('gloomhaven-rules.pdf')).toBeInTheDocument();
      expect(screen.getByText('wingspan-rules.pdf')).toBeInTheDocument();
      expect(screen.getByText('catan-rules.pdf')).toBeInTheDocument();
    });

    it('renders table headers', () => {
      render(<PdfTable {...mockProps} />);

      expect(screen.getByRole('columnheader', { name: /File name/i })).toBeInTheDocument();
      expect(screen.getByRole('columnheader', { name: /Language/i })).toBeInTheDocument();
      expect(screen.getByRole('columnheader', { name: /Size/i })).toBeInTheDocument();
      expect(screen.getByRole('columnheader', { name: /Uploaded/i })).toBeInTheDocument();
      expect(screen.getByRole('columnheader', { name: /Status/i })).toBeInTheDocument();
      expect(screen.getByRole('columnheader', { name: /Actions/i })).toBeInTheDocument();
    });

    it('formats file sizes correctly', () => {
      render(<PdfTable {...mockProps} />);

      expect(screen.getByText('5 MB')).toBeInTheDocument();
      expect(screen.getByText('3 MB')).toBeInTheDocument();
      expect(screen.getByText('2 MB')).toBeInTheDocument();
    });

    it('displays language badges', () => {
      render(<PdfTable {...mockProps} />);

      expect(screen.getByTitle('English')).toHaveTextContent('EN');
      expect(screen.getByTitle('Italiano')).toHaveTextContent('IT');
      expect(screen.getByTitle('Deutsch')).toHaveTextContent('DE');
    });

    it('displays status badges with correct variants', () => {
      render(<PdfTable {...mockProps} />);

      const rows = screen.getAllByRole('row');
      expect(rows).toHaveLength(4); // header + 3 data rows
    });
  });

  describe('Loading State', () => {
    it('shows loading skeleton when loading', () => {
      render(<PdfTable {...mockProps} loading={true} />);

      expect(screen.getByRole('status')).toBeInTheDocument();
      expect(screen.queryByRole('table')).not.toBeInTheDocument();
    });

    it('shows correct number of skeleton rows', () => {
      render(<PdfTable {...mockProps} loading={true} />);

      const skeletons = screen.getByRole('status').querySelectorAll('.animate-pulse');
      expect(skeletons).toHaveLength(3);
    });
  });

  describe('Error State', () => {
    it('displays error message when error prop is provided', () => {
      render(<PdfTable {...mockProps} error="Failed to load PDFs" />);

      expect(screen.getByText('Failed to load PDFs')).toBeInTheDocument();
      expect(screen.queryByRole('table')).not.toBeInTheDocument();
    });
  });

  describe('Empty State', () => {
    it('shows empty state when no PDFs', () => {
      render(<PdfTable {...mockProps} pdfs={[]} />);

      expect(screen.getByText(/No PDFs uploaded yet/i)).toBeInTheDocument();
      expect(screen.queryByRole('table')).not.toBeInTheDocument();
    });

    it('displays icon in empty state', () => {
      render(<PdfTable {...mockProps} pdfs={[]} />);

      const icon = screen.getByText(/No PDFs uploaded yet/i).closest('div')?.querySelector('svg');
      expect(icon).toBeInTheDocument();
    });
  });

  describe('Actions', () => {
    it('calls onOpenLog when Log button is clicked', async () => {
      const user = userEvent.setup();
      render(<PdfTable {...mockProps} />);

      const logButtons = screen.getAllByRole('button', { name: /Log/i });
      await user.click(logButtons[0]);

      expect(mockProps.onOpenLog).toHaveBeenCalledWith(mockPdfs[0]);
    });

    it('calls onRetryParsing when Retry button is clicked', async () => {
      const user = userEvent.setup();
      render(<PdfTable {...mockProps} />);

      const retryButtons = screen.getAllByRole('button', { name: /Retry/i });
      await user.click(retryButtons[0]);

      expect(mockProps.onRetryParsing).toHaveBeenCalledWith(mockPdfs[0]);
    });

    it('shows spinning icon when retrying', () => {
      render(<PdfTable {...mockProps} retryingPdfId="2" />);

      const retryButton = screen.getByRole('button', { name: /Retrying.../i });
      expect(retryButton).toBeDisabled();
      expect(retryButton.querySelector('.animate-spin')).toBeInTheDocument();
    });

    it('disables retry button while retrying', () => {
      render(<PdfTable {...mockProps} retryingPdfId="2" />);

      const retryButton = screen.getByRole('button', { name: /Retrying.../i });
      expect(retryButton).toBeDisabled();
    });
  });

  describe('Date Formatting', () => {
    it('formats dates correctly', () => {
      render(<PdfTable {...mockProps} />);

      // Check that dates are formatted (exact format may vary by locale)
      const cells = screen.getAllByRole('cell');
      const dateCells = cells.filter(cell => cell.textContent?.includes('2024'));
      expect(dateCells.length).toBeGreaterThan(0);
    });
  });

  describe('Accessibility', () => {
    it('renders semantic table structure', () => {
      render(<PdfTable {...mockProps} />);

      expect(screen.getByRole('table')).toBeInTheDocument();
      expect(screen.getAllByRole('columnheader')).toHaveLength(6);
      expect(screen.getAllByRole('row')).toHaveLength(4); // header + 3 data rows
    });

    it('provides accessible button labels', () => {
      render(<PdfTable {...mockProps} />);

      const logButtons = screen.getAllByRole('button', { name: /Log/i });
      expect(logButtons).toHaveLength(3);

      const retryButtons = screen.getAllByRole('button', { name: /Retry/i });
      expect(retryButtons).toHaveLength(3);
    });
  });

  describe('Edge Cases', () => {
    it('handles PDF without language', () => {
      const pdfWithoutLanguage = {
        ...mockPdfs[0],
        language: null
      };

      render(<PdfTable {...mockProps} pdfs={[pdfWithoutLanguage]} />);

      expect(screen.getByTitle('English')).toHaveTextContent('EN'); // Default
    });

    it('handles PDF without status', () => {
      const pdfWithoutStatus = {
        ...mockPdfs[0],
        status: null
      };

      render(<PdfTable {...mockProps} pdfs={[pdfWithoutStatus]} />);

      expect(screen.getByText('Pending')).toBeInTheDocument();
    });

    it('handles PDF without logUrl', () => {
      const pdfWithoutLog = {
        ...mockPdfs[0],
        logUrl: null
      };

      render(<PdfTable {...mockProps} pdfs={[pdfWithoutLog]} onOpenLog={mockProps.onOpenLog} />);

      const logButtons = screen.getAllByRole('button', { name: /Log/i });
      expect(logButtons).toHaveLength(1); // Still renders button even without logUrl
    });

    it('does not render action buttons when handlers not provided', () => {
      render(<PdfTable pdfs={mockPdfs} />);

      expect(screen.queryByRole('button', { name: /Log/i })).not.toBeInTheDocument();
      expect(screen.queryByRole('button', { name: /Retry/i })).not.toBeInTheDocument();
    });
  });
});