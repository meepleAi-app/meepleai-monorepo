import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent, within } from '@testing-library/react';

const mockLoggerDebug = vi.hoisted(() => vi.fn());
const mockLoggerWarn = vi.hoisted(() => vi.fn());
vi.mock('@/lib/logger', () => ({
  logger: {
    debug: (...args: unknown[]) => mockLoggerDebug(...args),
    info: vi.fn(),
    warn: (...args: unknown[]) => mockLoggerWarn(...args),
    error: vi.fn(),
  },
  getLogger: () => ({
    debug: (...args: unknown[]) => mockLoggerDebug(...args),
    info: vi.fn(),
    warn: (...args: unknown[]) => mockLoggerWarn(...args),
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

  it('displays all 8 initial categories', () => {
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

  // Issue #1429 Phase 1: dialog wiring — Add / Edit / Delete flows.
  // Tests cover the local-state mutations only; Phase 2 will swap the
  // mutations for a TanStack mutation hook and the assertions for the
  // happy-path will change to assert on the queryClient cache, not the DOM.

  it('opens the Add Category dialog when the header button is clicked', () => {
    render(<CategoriesTable />);

    fireEvent.click(screen.getByRole('button', { name: /add category/i }));

    expect(screen.getByRole('dialog')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: /add category/i })).toBeInTheDocument();
  });

  it('appends a new row when the Add dialog is submitted', () => {
    render(<CategoriesTable />);

    fireEvent.click(screen.getByRole('button', { name: /add category/i }));
    const dialog = screen.getByRole('dialog');

    fireEvent.change(within(dialog).getByLabelText(/name/i), {
      target: { value: 'Eurogame' },
    });
    fireEvent.change(within(dialog).getByLabelText(/emoji/i), {
      target: { value: '🏰' },
    });

    fireEvent.click(within(dialog).getByRole('button', { name: /add category/i }));

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(screen.getByText('Eurogame')).toBeInTheDocument();
  });

  it('rejects an empty name on submit and keeps the dialog open', () => {
    render(<CategoriesTable />);

    fireEvent.click(screen.getByRole('button', { name: /add category/i }));
    const dialog = screen.getByRole('dialog');

    // No name typed — submit should surface validation and not close.
    fireEvent.click(within(dialog).getByRole('button', { name: /add category/i }));

    expect(screen.getByRole('dialog')).toBeInTheDocument();
    expect(within(dialog).getByRole('alert')).toHaveTextContent(/name is required/i);
  });

  it('opens the Edit dialog seeded with the row values', () => {
    render(<CategoriesTable />);

    fireEvent.click(screen.getByRole('button', { name: /edit strategy/i }));
    const dialog = screen.getByRole('dialog');

    expect(within(dialog).getByRole('heading', { name: /edit category/i })).toBeInTheDocument();
    expect(within(dialog).getByLabelText(/name/i)).toHaveValue('Strategy');
  });

  it('updates a row when the Edit dialog is submitted', () => {
    render(<CategoriesTable />);

    fireEvent.click(screen.getByRole('button', { name: /edit strategy/i }));
    const dialog = screen.getByRole('dialog');

    fireEvent.change(within(dialog).getByLabelText(/name/i), {
      target: { value: 'Heavy Strategy' },
    });
    fireEvent.click(within(dialog).getByRole('button', { name: /save changes/i }));

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(screen.getByText('Heavy Strategy')).toBeInTheDocument();
    expect(screen.queryByText('Strategy')).not.toBeInTheDocument();
  });

  it('opens the Delete confirmation with a warning when the category has games', () => {
    render(<CategoriesTable />);

    fireEvent.click(screen.getByRole('button', { name: /delete strategy/i }));
    const dialog = screen.getByRole('dialog');

    expect(within(dialog).getByText(/strategy/i)).toBeInTheDocument();
    expect(within(dialog).getByRole('alert')).toHaveTextContent(/42 games are currently tagged/i);
  });

  it('removes a row when the Delete is confirmed', () => {
    render(<CategoriesTable />);

    fireEvent.click(screen.getByRole('button', { name: /delete party/i }));
    fireEvent.click(screen.getByRole('button', { name: /^delete$/i }));

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(screen.queryByText('Party')).not.toBeInTheDocument();
  });
});
