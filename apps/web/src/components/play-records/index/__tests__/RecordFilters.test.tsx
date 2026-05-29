/**
 * RecordFilters Component Tests
 *
 * Tests for sticky filters with search, status chips, dropdowns, and view toggle.
 * Issue #1488: Play Records Index Reskin (Task 1)
 */

import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';

import { RecordFilters } from '../RecordFilters';
import { playRecordsIndexMessages } from '@/__tests__/fixtures/i18n-test-messages';

// Mock next-intl
vi.mock('next-intl', () => ({
  useTranslations: () => (key: string) =>
    playRecordsIndexMessages[key as keyof typeof playRecordsIndexMessages] || key,
}));

describe('RecordFilters', () => {
  const mockOnStatusChange = vi.fn();
  const mockOnViewChange = vi.fn();
  const mockOnSearchChange = vi.fn();

  const defaultProps = {
    statusFilter: 'all' as const,
    view: 'list' as const,
    search: '',
    onStatusChange: mockOnStatusChange,
    onViewChange: mockOnViewChange,
    onSearchChange: mockOnSearchChange,
  };

  it('renders search input with placeholder', () => {
    render(<RecordFilters {...defaultProps} />);

    const input = screen.getByRole('searchbox');
    expect(input).toHaveAttribute('placeholder', expect.stringContaining('Cerca'));
  });

  it('renders 4 status chips', () => {
    render(<RecordFilters {...defaultProps} />);

    // AC-1.2: Status chips visible
    expect(screen.getByText(/statusAll/i)).toBeInTheDocument();
    expect(screen.getByText(/statusInProgress/i)).toBeInTheDocument();
    expect(screen.getByText(/statusCompleted/i)).toBeInTheDocument();
    expect(screen.getByText(/statusPlanned/i)).toBeInTheDocument();
  });

  it('renders view toggle with list and grid options', () => {
    render(<RecordFilters {...defaultProps} />);

    // AC-1.2: view toggle list/grid with radiogroup role
    const radiogroup = screen.getByRole('radiogroup', { name: /Vista/i });
    expect(radiogroup).toBeInTheDocument();
  });

  it('calls onSearchChange when input value changes', () => {
    render(<RecordFilters {...defaultProps} />);

    const input = screen.getByRole('searchbox');
    fireEvent.change(input, { target: { value: 'Catan' } });

    expect(mockOnSearchChange).toHaveBeenCalledWith('Catan');
  });

  it('calls onViewChange when view toggle clicked', () => {
    render(<RecordFilters {...defaultProps} />);

    const gridToggle = screen.getByRole('radio', { name: /Grid/i });
    fireEvent.click(gridToggle);

    expect(mockOnViewChange).toHaveBeenCalledWith('grid');
  });

  it('applies sticky positioning', () => {
    const { container } = render(<RecordFilters {...defaultProps} />);

    // AC-1.2: sticky top positioning with z-index
    const filtersContainer = container.firstChild;
    expect(filtersContainer).toHaveClass('sticky');
  });
});
