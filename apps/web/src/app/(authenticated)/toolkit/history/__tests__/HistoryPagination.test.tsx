/**
 * HistoryPagination — client-side pagination bar for /toolkit/history
 * (Issue #3006, Task A6).
 *
 * `useTranslation` is mocked with an id-passthrough `t()` for keys without
 * interpolation values, and a `id::JSON(values)` encoding for interpolated
 * keys (`rangeMeta`, `mobileMeta`) so assertions can check both the message
 * id and the exact values passed in, without depending on the real ICU
 * message catalog.
 *
 * The component renders both a mobile and a desktop layout simultaneously
 * (visibility toggled via Tailwind's `sm:` breakpoint, which jsdom does not
 * evaluate), so tests scope queries to `history-pagination-desktop` /
 * `history-pagination-mobile` via `within()` to avoid duplicate-match
 * ambiguity between the two layouts.
 */

import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { HistoryPagination, type HistoryPaginationProps } from '../_components/HistoryPagination';

vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({
    t: (id: string, values?: Record<string, unknown>) =>
      values ? `${id}::${JSON.stringify(values)}` : id,
  }),
}));

function renderPagination(overrides: Partial<HistoryPaginationProps> = {}) {
  const onPageChange = overrides.onPageChange ?? vi.fn();
  const onPageSizeChange = overrides.onPageSizeChange ?? vi.fn();

  const props: HistoryPaginationProps = {
    page: overrides.page ?? 3,
    total: overrides.total ?? 156,
    pageSize: overrides.pageSize ?? 20,
    onPageChange,
    onPageSizeChange,
  };

  render(<HistoryPagination {...props} />);

  return { onPageChange, onPageSizeChange };
}

describe('HistoryPagination', () => {
  it('renders the range meta with the correct from/to/total for page 3 of a 156-item, 20-per-page set', () => {
    renderPagination({ page: 3, total: 156, pageSize: 20 });

    const desktop = screen.getByTestId('history-pagination-desktop');
    expect(
      within(desktop).getByText(
        'pages.toolkitHistory.pagination.rangeMeta::{"from":41,"to":60,"total":156}'
      )
    ).toBeInTheDocument();
  });

  it('renders the mobile meta with page/pages/total', () => {
    renderPagination({ page: 3, total: 156, pageSize: 20 });

    const mobile = screen.getByTestId('history-pagination-mobile');
    // pages = ceil(156 / 20) = 8
    expect(
      within(mobile).getByText(
        'pages.toolkitHistory.pagination.mobileMeta::{"page":3,"pages":8,"total":156}'
      )
    ).toBeInTheDocument();
  });

  it('shows the empty-state label instead of the range meta when total is 0', () => {
    renderPagination({ page: 1, total: 0, pageSize: 20 });

    const desktop = screen.getByTestId('history-pagination-desktop');
    expect(within(desktop).getByText('pages.toolkitHistory.pagination.empty')).toBeInTheDocument();
    expect(within(desktop).queryByText(/rangeMeta/)).not.toBeInTheDocument();
  });

  it('calls onPageChange(page + 1) when "Succ." is clicked', async () => {
    const user = userEvent.setup();
    const { onPageChange } = renderPagination({ page: 3, total: 156, pageSize: 20 });

    const desktop = screen.getByTestId('history-pagination-desktop');
    await user.click(
      within(desktop).getByRole('button', { name: 'pages.toolkitHistory.pagination.nextAria' })
    );

    expect(onPageChange).toHaveBeenCalledWith(4);
  });

  it('calls onPageChange(page - 1) when "Prec." is clicked', async () => {
    const user = userEvent.setup();
    const { onPageChange } = renderPagination({ page: 3, total: 156, pageSize: 20 });

    const desktop = screen.getByTestId('history-pagination-desktop');
    await user.click(
      within(desktop).getByRole('button', { name: 'pages.toolkitHistory.pagination.prevAria' })
    );

    expect(onPageChange).toHaveBeenCalledWith(2);
  });

  it('disables the previous button and does not call onPageChange when already on page 1', async () => {
    const user = userEvent.setup();
    const { onPageChange } = renderPagination({ page: 1, total: 156, pageSize: 20 });

    const desktop = screen.getByTestId('history-pagination-desktop');
    const prevButton = within(desktop).getByRole('button', {
      name: 'pages.toolkitHistory.pagination.prevAria',
    });

    expect(prevButton).toBeDisabled();
    await user.click(prevButton);
    expect(onPageChange).not.toHaveBeenCalled();
  });

  it('disables the next button when already on the last page', () => {
    // pages = ceil(156 / 20) = 8
    renderPagination({ page: 8, total: 156, pageSize: 20 });

    const desktop = screen.getByTestId('history-pagination-desktop');
    const nextButton = within(desktop).getByRole('button', {
      name: 'pages.toolkitHistory.pagination.nextAria',
    });

    expect(nextButton).toBeDisabled();
  });

  it('marks the active page number with aria-current="page"', () => {
    renderPagination({ page: 3, total: 156, pageSize: 20 });

    const desktop = screen.getByTestId('history-pagination-desktop');
    const activeButton = within(desktop).getByRole('button', { name: '3' });

    expect(activeButton).toHaveAttribute('aria-current', 'page');
    expect(within(desktop).getByRole('button', { name: '4' })).not.toHaveAttribute('aria-current');
  });

  it('renders ellipses for a page list with more than 7 pages', () => {
    // pages = ceil(156 / 20) = 8 > 7
    renderPagination({ page: 3, total: 156, pageSize: 20 });

    const desktop = screen.getByTestId('history-pagination-desktop');
    expect(within(desktop).getAllByText('···').length).toBeGreaterThan(0);
  });

  it('calls onPageSizeChange(50) when a new page size is selected', async () => {
    const user = userEvent.setup();
    const { onPageSizeChange } = renderPagination({ page: 3, total: 156, pageSize: 20 });

    const desktop = screen.getByTestId('history-pagination-desktop');
    const trigger = within(desktop).getByRole('combobox', {
      name: 'pages.toolkitHistory.pagination.perPageAria',
    });
    await user.click(trigger);
    const option = await screen.findByRole('option', { name: '50' });
    await user.click(option);

    expect(onPageSizeChange).toHaveBeenCalledWith(50);
  });
});
