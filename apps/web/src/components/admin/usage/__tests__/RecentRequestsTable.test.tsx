/**
 * RecentRequestsTable unit tests.
 * Issue #5083: Admin usage page — recent LLM requests table.
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { RecentRequestsTable } from '../RecentRequestsTable';

// ─── Fixtures ─────────────────────────────────────────────────────────────────

const makeItem = (id: string, success: boolean, isFreeModel = false) => ({
  id,
  requestedAt:      '2026-02-22T10:30:00Z',
  modelId:          'openai/gpt-4o-mini',
  source:           'Manual',
  isFreeModel,
  promptTokens:     100,
  completionTokens: 50,
  totalTokens:      150,
  costUsd:          0.00025,
  latencyMs:        423,
  success,
  errorMessage:     success ? null : 'Rate limit exceeded',
});

const mockData = {
  items:      [
    makeItem('req-1', true),
    makeItem('req-2', false),
    makeItem('req-3', true, true),
  ],
  total:      3,
  page:       1,
  pageSize:   20,
  totalPages: 1,
};

const defaultFilters = { page: 1, pageSize: 20 };

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('RecentRequestsTable', () => {
  const noop = vi.fn();

  it('renders card title', () => {
    render(<RecentRequestsTable data={mockData} filters={defaultFilters} onFiltersChange={noop} />);
    expect(screen.getByText('Recent Requests')).toBeInTheDocument();
  });

  it('shows total count in header', () => {
    render(<RecentRequestsTable data={mockData} filters={defaultFilters} onFiltersChange={noop} />);
    expect(screen.getByText('3 total')).toBeInTheDocument();
  });

  it('renders all table column headers', () => {
    render(<RecentRequestsTable data={mockData} filters={defaultFilters} onFiltersChange={noop} />);
    expect(screen.getByText('Time')).toBeInTheDocument();
    expect(screen.getByText('Model')).toBeInTheDocument();
    expect(screen.getByText('Source')).toBeInTheDocument();
    expect(screen.getByText('Tokens')).toBeInTheDocument();
    expect(screen.getByText('Cost')).toBeInTheDocument();
    expect(screen.getByText('Latency')).toBeInTheDocument();
    expect(screen.getByText('OK')).toBeInTheDocument();
  });

  it('renders a row per item — shows cost for each', () => {
    render(<RecentRequestsTable data={mockData} filters={defaultFilters} onFiltersChange={noop} />);
    const costCells = screen.getAllByText('$0.00025');
    expect(costCells).toHaveLength(3);
  });

  it('shows token count for each row', () => {
    render(<RecentRequestsTable data={mockData} filters={defaultFilters} onFiltersChange={noop} />);
    const tokenCells = screen.getAllByText('150');
    expect(tokenCells).toHaveLength(3);
  });

  it('formats latency as milliseconds', () => {
    render(<RecentRequestsTable data={mockData} filters={defaultFilters} onFiltersChange={noop} />);
    const latencyCells = screen.getAllByText('423ms');
    expect(latencyCells).toHaveLength(3);
  });

  it('formats latency >= 1000ms as seconds', () => {
    const withHighLatency = {
      ...mockData,
      items: [{ ...makeItem('req-x', true), latencyMs: 1500 }],
    };
    render(<RecentRequestsTable data={withHighLatency} filters={defaultFilters} onFiltersChange={noop} />);
    expect(screen.getByText('1.5s')).toBeInTheDocument();
  });

  it('shows "free" badge for free-model requests', () => {
    render(<RecentRequestsTable data={mockData} filters={defaultFilters} onFiltersChange={noop} />);
    expect(screen.getByText('free')).toBeInTheDocument();
  });

  it('shows loading skeletons when isLoading', () => {
    render(
      <RecentRequestsTable
        data={null}
        filters={defaultFilters}
        onFiltersChange={noop}
        isLoading
      />
    );
    const skeletons = document.querySelectorAll('.animate-pulse');
    expect(skeletons.length).toBeGreaterThan(0);
  });

  it('shows empty-state message when items list is empty', () => {
    render(
      <RecentRequestsTable
        data={{ ...mockData, items: [], total: 0 }}
        filters={defaultFilters}
        onFiltersChange={noop}
      />
    );
    expect(screen.getByText('No requests found')).toBeInTheDocument();
  });

  it('shows empty-state message when data is null', () => {
    render(<RecentRequestsTable data={null} filters={defaultFilters} onFiltersChange={noop} />);
    expect(screen.getByText('No requests found')).toBeInTheDocument();
  });

  // ── Filter panel ────────────────────────────────────────────────────────────

  it('hides filter controls initially', () => {
    render(<RecentRequestsTable data={mockData} filters={defaultFilters} onFiltersChange={noop} />);
    expect(screen.queryByLabelText('Filter by source')).not.toBeInTheDocument();
  });

  it('shows filter controls after clicking Filters button', () => {
    render(<RecentRequestsTable data={mockData} filters={defaultFilters} onFiltersChange={noop} />);
    fireEvent.click(screen.getByRole('button', { name: /Filters/i }));
    expect(screen.getByLabelText('Filter by source')).toBeInTheDocument();
    expect(screen.getByLabelText('Filter by model')).toBeInTheDocument();
    expect(screen.getByLabelText('Filter by success')).toBeInTheDocument();
  });

  it('calls onFiltersChange with source filter when source changed', () => {
    const onChange = vi.fn();
    render(<RecentRequestsTable data={mockData} filters={defaultFilters} onFiltersChange={onChange} />);
    fireEvent.click(screen.getByRole('button', { name: /Filters/i }));
    fireEvent.change(screen.getByLabelText('Filter by source'), { target: { value: 'Manual' } });
    expect(onChange).toHaveBeenCalledWith(expect.objectContaining({ source: 'Manual', page: 1 }));
  });

  it('calls onFiltersChange with undefined source when "All sources" selected', () => {
    const onChange = vi.fn();
    render(
      <RecentRequestsTable
        data={mockData}
        filters={{ ...defaultFilters, source: 'Manual' }}
        onFiltersChange={onChange}
      />
    );
    fireEvent.click(screen.getByRole('button', { name: /Filters/i }));
    fireEvent.change(screen.getByLabelText('Filter by source'), { target: { value: '' } });
    expect(onChange).toHaveBeenCalledWith(expect.objectContaining({ source: undefined, page: 1 }));
  });

  it('calls onFiltersChange with successOnly:true when "Success only" selected', () => {
    const onChange = vi.fn();
    render(<RecentRequestsTable data={mockData} filters={defaultFilters} onFiltersChange={onChange} />);
    fireEvent.click(screen.getByRole('button', { name: /Filters/i }));
    fireEvent.change(screen.getByLabelText('Filter by success'), { target: { value: 'true' } });
    expect(onChange).toHaveBeenCalledWith(expect.objectContaining({ successOnly: true, page: 1 }));
  });

  it('calls onFiltersChange with successOnly:false when "Errors only" selected', () => {
    const onChange = vi.fn();
    render(<RecentRequestsTable data={mockData} filters={defaultFilters} onFiltersChange={onChange} />);
    fireEvent.click(screen.getByRole('button', { name: /Filters/i }));
    fireEvent.change(screen.getByLabelText('Filter by success'), { target: { value: 'false' } });
    expect(onChange).toHaveBeenCalledWith(expect.objectContaining({ successOnly: false, page: 1 }));
  });

  // ── Pagination ──────────────────────────────────────────────────────────────

  it('does not render pagination when totalPages = 1', () => {
    render(<RecentRequestsTable data={mockData} filters={defaultFilters} onFiltersChange={noop} />);
    expect(screen.queryByLabelText('Previous page')).not.toBeInTheDocument();
    expect(screen.queryByLabelText('Next page')).not.toBeInTheDocument();
  });

  it('renders pagination controls when totalPages > 1', () => {
    const multiPage = { ...mockData, totalPages: 3, total: 60 };
    render(<RecentRequestsTable data={multiPage} filters={defaultFilters} onFiltersChange={noop} />);
    expect(screen.getByLabelText('Previous page')).toBeInTheDocument();
    expect(screen.getByLabelText('Next page')).toBeInTheDocument();
  });

  it('shows page N of M in pagination', () => {
    const multiPage = { ...mockData, totalPages: 3, total: 60 };
    render(<RecentRequestsTable data={multiPage} filters={defaultFilters} onFiltersChange={noop} />);
    expect(screen.getByText('Page 1 of 3')).toBeInTheDocument();
  });

  it('calls onFiltersChange with page+1 when Next clicked', () => {
    const onChange = vi.fn();
    const multiPage = { ...mockData, totalPages: 3, total: 60 };
    render(<RecentRequestsTable data={multiPage} filters={defaultFilters} onFiltersChange={onChange} />);
    fireEvent.click(screen.getByLabelText('Next page'));
    expect(onChange).toHaveBeenCalledWith({ page: 2, pageSize: 20 });
  });

  it('calls onFiltersChange with page-1 when Previous clicked', () => {
    const onChange = vi.fn();
    const multiPage = { ...mockData, totalPages: 3, total: 60 };
    const page2Filters = { page: 2, pageSize: 20 };
    render(<RecentRequestsTable data={multiPage} filters={page2Filters} onFiltersChange={onChange} />);
    fireEvent.click(screen.getByLabelText('Previous page'));
    expect(onChange).toHaveBeenCalledWith({ page: 1, pageSize: 20 });
  });

  it('disables Previous button on first page', () => {
    const multiPage = { ...mockData, totalPages: 3, total: 60 };
    render(<RecentRequestsTable data={multiPage} filters={defaultFilters} onFiltersChange={noop} />);
    expect(screen.getByLabelText('Previous page')).toBeDisabled();
  });

  it('disables Next button on last page', () => {
    const onChange = vi.fn();
    const multiPage = { ...mockData, totalPages: 3, total: 60 };
    const lastPageFilters = { page: 3, pageSize: 20 };
    render(<RecentRequestsTable data={multiPage} filters={lastPageFilters} onFiltersChange={onChange} />);
    expect(screen.getByLabelText('Next page')).toBeDisabled();
  });
});
