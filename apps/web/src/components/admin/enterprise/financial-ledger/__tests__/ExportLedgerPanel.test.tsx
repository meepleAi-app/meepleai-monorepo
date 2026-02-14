/**
 * ExportLedgerPanel Tests
 * Issue #3724 - Export Ledger (PDF/CSV/Excel)
 */

import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { ExportLedgerPanel } from '../ExportLedgerPanel';

// Mock api
vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      exportLedgerEntries: vi.fn(),
    },
  },
}));

// Mock URL.createObjectURL/revokeObjectURL
const mockCreateObjectURL = vi.fn(() => 'blob:mock-url');
const mockRevokeObjectURL = vi.fn();
Object.defineProperty(global.URL, 'createObjectURL', { value: mockCreateObjectURL });
Object.defineProperty(global.URL, 'revokeObjectURL', { value: mockRevokeObjectURL });

describe('ExportLedgerPanel', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders the export panel', () => {
    render(<ExportLedgerPanel />);
    expect(screen.getByTestId('export-ledger-panel')).toBeInTheDocument();
  });

  it('renders the panel title', () => {
    render(<ExportLedgerPanel />);
    expect(screen.getByText('Export Ledger Data')).toBeInTheDocument();
  });

  it('renders format selector with three options', () => {
    render(<ExportLedgerPanel />);
    expect(screen.getByTestId('format-csv')).toBeInTheDocument();
    expect(screen.getByTestId('format-excel')).toBeInTheDocument();
    expect(screen.getByTestId('format-pdf')).toBeInTheDocument();
  });

  it('renders date range inputs', () => {
    render(<ExportLedgerPanel />);
    expect(screen.getByTestId('export-date-from')).toBeInTheDocument();
    expect(screen.getByTestId('export-date-to')).toBeInTheDocument();
  });

  it('renders type and category filter selects', () => {
    render(<ExportLedgerPanel />);
    expect(screen.getByTestId('export-filter-type')).toBeInTheDocument();
    expect(screen.getByTestId('export-filter-category')).toBeInTheDocument();
  });

  it('renders export button', () => {
    render(<ExportLedgerPanel />);
    expect(screen.getByTestId('export-btn')).toBeInTheDocument();
  });

  it('defaults to CSV format', () => {
    render(<ExportLedgerPanel />);
    const btn = screen.getByTestId('export-btn');
    expect(btn.textContent).toContain('CSV');
  });

  it('switches format when clicking Excel', () => {
    render(<ExportLedgerPanel />);
    fireEvent.click(screen.getByTestId('format-excel'));
    const btn = screen.getByTestId('export-btn');
    expect(btn.textContent).toContain('Excel');
  });

  it('switches format when clicking PDF', () => {
    render(<ExportLedgerPanel />);
    fireEvent.click(screen.getByTestId('format-pdf'));
    const btn = screen.getByTestId('export-btn');
    expect(btn.textContent).toContain('PDF');
  });

  it('shows format labels correctly', () => {
    render(<ExportLedgerPanel />);
    expect(screen.getByText('CSV')).toBeInTheDocument();
    expect(screen.getByText('Excel (.xlsx)')).toBeInTheDocument();
    expect(screen.getByText('PDF Report')).toBeInTheDocument();
  });

  it('calls exportLedgerEntries when export button is clicked', async () => {
    const { api } = await import('@/lib/api');
    const mockBlob = new Blob(['test'], { type: 'text/csv' });
    vi.mocked(api.admin.exportLedgerEntries).mockResolvedValueOnce(mockBlob);

    render(<ExportLedgerPanel />);
    fireEvent.click(screen.getByTestId('export-btn'));

    await waitFor(() => {
      expect(api.admin.exportLedgerEntries).toHaveBeenCalledWith({
        format: 0,
        dateFrom: undefined,
        dateTo: undefined,
        type: undefined,
        category: undefined,
      });
    });
  });

  it('passes date range to export params', async () => {
    const { api } = await import('@/lib/api');
    const mockBlob = new Blob(['test'], { type: 'text/csv' });
    vi.mocked(api.admin.exportLedgerEntries).mockResolvedValueOnce(mockBlob);

    render(<ExportLedgerPanel />);
    fireEvent.change(screen.getByTestId('export-date-from'), { target: { value: '2026-01-01' } });
    fireEvent.change(screen.getByTestId('export-date-to'), { target: { value: '2026-01-31' } });
    fireEvent.click(screen.getByTestId('export-btn'));

    await waitFor(() => {
      expect(api.admin.exportLedgerEntries).toHaveBeenCalledWith({
        format: 0,
        dateFrom: '2026-01-01',
        dateTo: '2026-01-31',
        type: undefined,
        category: undefined,
      });
    });
  });

  it('passes selected format to export params', async () => {
    const { api } = await import('@/lib/api');
    const mockBlob = new Blob(['test'], { type: 'application/pdf' });
    vi.mocked(api.admin.exportLedgerEntries).mockResolvedValueOnce(mockBlob);

    render(<ExportLedgerPanel />);
    fireEvent.click(screen.getByTestId('format-pdf'));
    fireEvent.click(screen.getByTestId('export-btn'));

    await waitFor(() => {
      expect(api.admin.exportLedgerEntries).toHaveBeenCalledWith(
        expect.objectContaining({ format: 2 })
      );
    });
  });

  it('shows error message when export fails', async () => {
    const { api } = await import('@/lib/api');
    vi.mocked(api.admin.exportLedgerEntries).mockRejectedValueOnce(new Error('Network error'));

    render(<ExportLedgerPanel />);
    fireEvent.click(screen.getByTestId('export-btn'));

    await waitFor(() => {
      expect(screen.getByTestId('export-error')).toBeInTheDocument();
      expect(screen.getByText('Network error')).toBeInTheDocument();
    });
  });

  it('shows loading state while exporting', async () => {
    const { api } = await import('@/lib/api');
    let resolveExport: (value: Blob) => void;
    const exportPromise = new Promise<Blob>((resolve) => { resolveExport = resolve; });
    vi.mocked(api.admin.exportLedgerEntries).mockReturnValueOnce(exportPromise);

    render(<ExportLedgerPanel />);
    fireEvent.click(screen.getByTestId('export-btn'));

    expect(screen.getByText('Exporting...')).toBeInTheDocument();
    expect(screen.getByTestId('export-btn')).toBeDisabled();

    resolveExport!(new Blob(['test']));
    await waitFor(() => {
      expect(screen.queryByText('Exporting...')).not.toBeInTheDocument();
    });
  });

  it('creates blob URL and triggers download', async () => {
    const { api } = await import('@/lib/api');
    const mockBlob = new Blob(['test'], { type: 'text/csv' });
    vi.mocked(api.admin.exportLedgerEntries).mockResolvedValueOnce(mockBlob);

    // Render first, then spy on DOM operations for the download anchor
    const { getByTestId } = render(<ExportLedgerPanel />);

    const originalAppendChild = document.body.appendChild.bind(document.body);
    const originalRemoveChild = document.body.removeChild.bind(document.body);
    const clickSpy = vi.fn();

    vi.spyOn(document.body, 'appendChild').mockImplementation((node) => {
      if (node instanceof HTMLAnchorElement) {
        node.click = clickSpy;
        return node;
      }
      return originalAppendChild(node);
    });
    vi.spyOn(document.body, 'removeChild').mockImplementation((node) => {
      if (node instanceof HTMLAnchorElement) return node;
      return originalRemoveChild(node);
    });

    fireEvent.click(getByTestId('export-btn'));

    await waitFor(() => {
      expect(mockCreateObjectURL).toHaveBeenCalledWith(mockBlob);
      expect(clickSpy).toHaveBeenCalled();
      expect(mockRevokeObjectURL).toHaveBeenCalledWith('blob:mock-url');
    });

    vi.restoreAllMocks();
  });

  it('passes type filter to export params', async () => {
    const { api } = await import('@/lib/api');
    const mockBlob = new Blob(['test'], { type: 'text/csv' });
    vi.mocked(api.admin.exportLedgerEntries).mockResolvedValueOnce(mockBlob);

    const { getByTestId } = render(<ExportLedgerPanel />);
    fireEvent.change(getByTestId('export-filter-type'), { target: { value: '0' } });
    fireEvent.click(getByTestId('export-btn'));

    await waitFor(() => {
      expect(api.admin.exportLedgerEntries).toHaveBeenCalledWith(
        expect.objectContaining({ type: 0 })
      );
    });
  });

  it('passes category filter to export params', async () => {
    const { api } = await import('@/lib/api');
    const mockBlob = new Blob(['test'], { type: 'text/csv' });
    vi.mocked(api.admin.exportLedgerEntries).mockResolvedValueOnce(mockBlob);

    const { getByTestId } = render(<ExportLedgerPanel />);
    fireEvent.change(getByTestId('export-filter-category'), { target: { value: '7' } });
    fireEvent.click(getByTestId('export-btn'));

    await waitFor(() => {
      expect(api.admin.exportLedgerEntries).toHaveBeenCalledWith(
        expect.objectContaining({ category: 7 })
      );
    });
  });

  it('clears error when export succeeds after failure', async () => {
    const { api } = await import('@/lib/api');
    vi.mocked(api.admin.exportLedgerEntries).mockRejectedValueOnce(new Error('First fail'));

    const { getByTestId, queryByTestId } = render(<ExportLedgerPanel />);
    fireEvent.click(getByTestId('export-btn'));

    await waitFor(() => {
      expect(getByTestId('export-error')).toBeInTheDocument();
    });

    const mockBlob = new Blob(['test'], { type: 'text/csv' });
    vi.mocked(api.admin.exportLedgerEntries).mockResolvedValueOnce(mockBlob);
    fireEvent.click(getByTestId('export-btn'));

    await waitFor(() => {
      expect(queryByTestId('export-error')).not.toBeInTheDocument();
    });
  });
});
