/**
 * QueueFiltersBar Component Tests (Issue #4737)
 *
 * Tests for the queue filter controls:
 * - Search input with debounce
 * - Status filter dropdown
 * - Date range dropdown
 * - Page reset on filter change
 */

import { render, screen, fireEvent, waitFor, act } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

import { QueueFiltersBar } from '@/app/admin/(dashboard)/knowledge-base/queue/components/queue-filters';
import type { QueueFilters } from '@/app/admin/(dashboard)/knowledge-base/queue/lib/queue-api';

describe('QueueFiltersBar', () => {
  const defaultFilters: QueueFilters = {
    page: 1,
    pageSize: 20,
  };

  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  describe('Search Input', () => {
    it('should render search input with placeholder', () => {
      const onChange = vi.fn();
      render(<QueueFiltersBar filters={defaultFilters} onFiltersChange={onChange} />);

      expect(screen.getByPlaceholderText('Search by filename...')).toBeInTheDocument();
    });

    it('should debounce search input', async () => {
      const onChange = vi.fn();
      render(<QueueFiltersBar filters={defaultFilters} onFiltersChange={onChange} />);

      const input = screen.getByPlaceholderText('Search by filename...');
      fireEvent.change(input, { target: { value: 'test.pdf' } });

      // Should not fire immediately
      expect(onChange).not.toHaveBeenCalled();

      // Advance timers past debounce (300ms)
      act(() => {
        vi.advanceTimersByTime(350);
      });

      expect(onChange).toHaveBeenCalledWith(
        expect.objectContaining({ search: 'test.pdf', page: 1 }),
      );
    });

    it('should clear search when input is emptied', async () => {
      const onChange = vi.fn();
      render(
        <QueueFiltersBar
          filters={{ ...defaultFilters, search: 'existing' }}
          onFiltersChange={onChange}
        />,
      );

      const input = screen.getByPlaceholderText('Search by filename...');
      fireEvent.change(input, { target: { value: '' } });

      act(() => {
        vi.advanceTimersByTime(350);
      });

      expect(onChange).toHaveBeenCalledWith(
        expect.objectContaining({ search: undefined, page: 1 }),
      );
    });
  });

  describe('Status Filter', () => {
    it('should render all status options', () => {
      const onChange = vi.fn();
      render(<QueueFiltersBar filters={defaultFilters} onFiltersChange={onChange} />);

      // The Select trigger should show "All Statuses" by default
      expect(screen.getByText('All Statuses')).toBeInTheDocument();
    });
  });

  describe('Date Filter', () => {
    it('should render date range dropdown', () => {
      const onChange = vi.fn();
      render(<QueueFiltersBar filters={defaultFilters} onFiltersChange={onChange} />);

      expect(screen.getByText('All Time')).toBeInTheDocument();
    });
  });

  describe('Layout', () => {
    it('should render all three filter controls', () => {
      const onChange = vi.fn();
      render(<QueueFiltersBar filters={defaultFilters} onFiltersChange={onChange} />);

      // Search input
      expect(screen.getByPlaceholderText('Search by filename...')).toBeInTheDocument();
      // Status select
      expect(screen.getByText('All Statuses')).toBeInTheDocument();
      // Date select
      expect(screen.getByText('All Time')).toBeInTheDocument();
    });
  });
});
