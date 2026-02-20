/**
 * PdfAdminTable Tests
 * Issue #4862: EntityTableView visual consistency migration
 *
 * Tests entity="document" slate border styling, selection,
 * sort headers, and row rendering.
 */

import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import type { AdminPdfListItem } from '@/lib/api/clients/pdfClient';

import { PdfAdminTable } from '../PdfAdminTable';

vi.mock('@/components/pdf/PdfProgressBar', () => ({
  PdfProgressBar: ({ state, progress }: { state: string; progress: number }) => (
    <div data-testid="pdf-progress-bar">{state}: {progress}%</div>
  ),
}));

vi.mock('@/components/pdf/PdfStatusBadge', () => ({
  PdfStatusBadge: ({ state }: { state: string }) => (
    <span data-testid="pdf-status-badge">{state}</span>
  ),
}));

vi.mock('../PdfRowActions', () => ({
  PdfRowActions: ({ pdf }: { pdf: { id: string } }) => (
    <div data-testid={`row-actions-${pdf.id}`}>Actions</div>
  ),
}));

vi.mock('@/components/ui/primitives/checkbox', () => ({
  Checkbox: ({ checked, onCheckedChange, 'aria-label': ariaLabel }: {
    checked: boolean | 'indeterminate';
    onCheckedChange: () => void;
    'aria-label': string;
  }) => (
    <input
      type="checkbox"
      checked={checked === true}
      onChange={onCheckedChange}
      aria-label={ariaLabel}
      data-indeterminate={checked === 'indeterminate' ? 'true' : undefined}
    />
  ),
}));

const mockItems: AdminPdfListItem[] = [
  {
    id: 'pdf-1',
    fileName: 'catan-rules.pdf',
    gameTitle: 'Catan',
    gameId: 'game-1',
    processingStatus: 'completed',
    processingState: 'Ready',
    progressPercentage: 100,
    fileSizeBytes: 2048576,
    pageCount: 12,
    chunkCount: 45,
    processingError: null,
    errorCategory: null,
    retryCount: 0,
    uploadedAt: '2025-01-15T10:30:00Z',
    processedAt: '2025-01-15T10:35:00Z',
  },
  {
    id: 'pdf-2',
    fileName: 'ticket-to-ride.pdf',
    gameTitle: null,
    gameId: null,
    processingStatus: 'processing',
    processingState: 'Extracting',
    progressPercentage: 45,
    fileSizeBytes: 512000,
    pageCount: null,
    chunkCount: 0,
    processingError: null,
    errorCategory: null,
    retryCount: 0,
    uploadedAt: '2025-01-16T14:00:00Z',
    processedAt: null,
  },
];

const defaultProps = {
  items: mockItems,
  selectedIds: new Set<string>(),
  onSelectionChange: vi.fn(),
  sortBy: 'uploadedat',
  sortOrder: 'desc' as const,
  onSortChange: vi.fn(),
  onDelete: vi.fn(),
  onReindex: vi.fn(),
  onRetry: vi.fn(),
};

describe('PdfAdminTable', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders with data-testid', () => {
    render(<PdfAdminTable {...defaultProps} />);

    expect(screen.getByTestId('pdf-admin-table')).toBeInTheDocument();
  });

  it('applies entity document border on rows', () => {
    const { container } = render(<PdfAdminTable {...defaultProps} />);

    const rows = container.querySelectorAll('tbody tr');
    expect(rows[0]).toHaveClass('border-l-4');
    expect(rows[0]).toHaveClass('border-l-[hsl(210,40%,55%)]');
  });

  it('applies font-nunito for design system consistency', () => {
    render(<PdfAdminTable {...defaultProps} />);

    const table = screen.getByTestId('pdf-admin-table');
    expect(table).toHaveClass('font-nunito');
  });

  it('renders filenames for each item', () => {
    render(<PdfAdminTable {...defaultProps} />);

    expect(screen.getByText('catan-rules.pdf')).toBeInTheDocument();
    expect(screen.getByText('ticket-to-ride.pdf')).toBeInTheDocument();
  });

  it('renders game title or dash', () => {
    render(<PdfAdminTable {...defaultProps} />);

    expect(screen.getByText('Catan')).toBeInTheDocument();
    // null gameTitle renders dash
    const dashes = screen.getAllByText('—');
    expect(dashes.length).toBeGreaterThan(0);
  });

  it('renders PdfStatusBadge for each item', () => {
    render(<PdfAdminTable {...defaultProps} />);

    const badges = screen.getAllByTestId('pdf-status-badge');
    expect(badges).toHaveLength(2);
  });

  it('renders PdfProgressBar for non-terminal states', () => {
    render(<PdfAdminTable {...defaultProps} />);

    const progressBars = screen.getAllByTestId('pdf-progress-bar');
    expect(progressBars).toHaveLength(1);
    expect(progressBars[0]).toHaveTextContent('extracting: 45%');
  });

  it('renders 100% text for ready state', () => {
    render(<PdfAdminTable {...defaultProps} />);

    expect(screen.getByText('100%')).toBeInTheDocument();
  });

  it('renders PdfRowActions for each item', () => {
    render(<PdfAdminTable {...defaultProps} />);

    expect(screen.getByTestId('row-actions-pdf-1')).toBeInTheDocument();
    expect(screen.getByTestId('row-actions-pdf-2')).toBeInTheDocument();
  });

  it('renders select-all checkbox', () => {
    render(<PdfAdminTable {...defaultProps} />);

    expect(screen.getByLabelText('Select all')).toBeInTheDocument();
  });

  it('renders individual row checkboxes', () => {
    render(<PdfAdminTable {...defaultProps} />);

    expect(screen.getByLabelText('Select catan-rules.pdf')).toBeInTheDocument();
    expect(screen.getByLabelText('Select ticket-to-ride.pdf')).toBeInTheDocument();
  });

  it('calls onSelectionChange when select-all is clicked', async () => {
    const user = userEvent.setup();
    const onSelectionChange = vi.fn();
    render(<PdfAdminTable {...defaultProps} onSelectionChange={onSelectionChange} />);

    await user.click(screen.getByLabelText('Select all'));

    expect(onSelectionChange).toHaveBeenCalledWith(new Set(['pdf-1', 'pdf-2']));
  });

  it('calls onSelectionChange to deselect all when all are selected', async () => {
    const user = userEvent.setup();
    const onSelectionChange = vi.fn();
    render(
      <PdfAdminTable
        {...defaultProps}
        selectedIds={new Set(['pdf-1', 'pdf-2'])}
        onSelectionChange={onSelectionChange}
      />
    );

    await user.click(screen.getByLabelText('Select all'));

    expect(onSelectionChange).toHaveBeenCalledWith(new Set());
  });

  it('calls onSelectionChange for individual row', async () => {
    const user = userEvent.setup();
    const onSelectionChange = vi.fn();
    render(<PdfAdminTable {...defaultProps} onSelectionChange={onSelectionChange} />);

    await user.click(screen.getByLabelText('Select catan-rules.pdf'));

    expect(onSelectionChange).toHaveBeenCalledWith(new Set(['pdf-1']));
  });

  it('renders sortable headers', () => {
    render(<PdfAdminTable {...defaultProps} />);

    expect(screen.getByText('Filename')).toBeInTheDocument();
    expect(screen.getByText('Size')).toBeInTheDocument();
    expect(screen.getByText('Uploaded')).toBeInTheDocument();
  });

  it('calls onSortChange when sort header clicked', async () => {
    const user = userEvent.setup();
    const onSortChange = vi.fn();
    render(<PdfAdminTable {...defaultProps} onSortChange={onSortChange} />);

    await user.click(screen.getByText('Filename'));

    expect(onSortChange).toHaveBeenCalledWith('filename');
  });

  it('shows empty state message when no items', () => {
    render(<PdfAdminTable {...defaultProps} items={[]} />);

    expect(screen.getByText('No PDF documents found.')).toBeInTheDocument();
  });

  it('highlights selected rows', () => {
    const { container } = render(
      <PdfAdminTable {...defaultProps} selectedIds={new Set(['pdf-1'])} />
    );

    const rows = container.querySelectorAll('tbody tr');
    expect(rows[0]).toHaveClass('bg-muted/20');
    expect(rows[1]).not.toHaveClass('bg-muted/20');
  });
});
