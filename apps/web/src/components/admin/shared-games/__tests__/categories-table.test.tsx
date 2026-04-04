import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';

const mockLoggerDebug = vi.hoisted(() => vi.fn());
vi.mock('@/lib/logger', () => ({
  logger: {
    debug: (...args: unknown[]) => mockLoggerDebug(...args),
    info: vi.fn(),
    warn: vi.fn(),
    error: vi.fn(),
  },
  getLogger: () => ({
    debug: (...args: unknown[]) => mockLoggerDebug(...args),
    info: vi.fn(),
    warn: vi.fn(),
    error: vi.fn(),
  }),
  resetLogger: vi.fn(),
  LogLevel: { DEBUG: 'debug', INFO: 'info', WARN: 'warn', ERROR: 'error' },
}));

import { CategoriesTable } from '../categories-table';

describe('CategoriesTable', () => {
  it('renders categories table with header', () => {
    render(<CategoriesTable />);

    expect(screen.getByText('Categories')).toBeInTheDocument();
    expect(screen.getByText('Drag to reorder, click to edit')).toBeInTheDocument();
  });

  it('renders Add Category button', () => {
    render(<CategoriesTable />);

    const addButton = screen.getByRole('button', { name: /add category/i });
    expect(addButton).toBeInTheDocument();
  });

  it('displays all 8 mock categories', () => {
    render(<CategoriesTable />);

    expect(screen.getByText('Strategy')).toBeInTheDocument();
    expect(screen.getByText('Party')).toBeInTheDocument();
    expect(screen.getByText('Cooperative')).toBeInTheDocument();
    expect(screen.getByText('Deck Building')).toBeInTheDocument();
    expect(screen.getByText('Family')).toBeInTheDocument();
    expect(screen.getByText('Abstract')).toBeInTheDocument();
    expect(screen.getByText('Thematic')).toBeInTheDocument();
    expect(screen.getByText('Euro')).toBeInTheDocument();
  });

  it('shows game counts for categories', () => {
    render(<CategoriesTable />);

    expect(screen.getByText('42 games')).toBeInTheDocument(); // Strategy
    expect(screen.getByText('28 games')).toBeInTheDocument(); // Party
  });

  it('handles Add Category button click', () => {
    mockLoggerDebug.mockClear();
    render(<CategoriesTable />);

    const addButton = screen.getByRole('button', { name: /add category/i });
    fireEvent.click(addButton);

    expect(mockLoggerDebug).toHaveBeenCalledWith('Add category');
  });
});
