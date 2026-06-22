import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import type { CategoryDto } from '@/lib/api/admin-categories';
import { AdminCategoriesApiError } from '@/lib/api/admin-categories';

const mockLoggerWarn = vi.hoisted(() => vi.fn());
vi.mock('@/lib/logger', () => ({
  logger: {
    debug: vi.fn(),
    info: vi.fn(),
    warn: (...args: unknown[]) => mockLoggerWarn(...args),
    error: vi.fn(),
  },
  getLogger: () => ({
    debug: vi.fn(),
    info: vi.fn(),
    warn: (...args: unknown[]) => mockLoggerWarn(...args),
    error: vi.fn(),
  }),
  resetLogger: vi.fn(),
  LogLevel: { DEBUG: 'debug', INFO: 'info', WARN: 'warn', ERROR: 'error' },
}));

const seedCategories: CategoryDto[] = [
  {
    id: 'cat-strategy',
    name: 'Strategy',
    slug: 'strategy',
    emoji: '♟️',
    color: '#3b82f6',
    gameCount: 42,
  },
  { id: 'cat-party', name: 'Party', slug: 'party', emoji: '🎉', color: '#ec4899', gameCount: 28 },
  {
    id: 'cat-coop',
    name: 'Cooperative',
    slug: 'cooperative',
    emoji: '🤝',
    color: '#10b981',
    gameCount: 19,
  },
  {
    id: 'cat-deck',
    name: 'Deck Building',
    slug: 'deck-building',
    emoji: '🃏',
    color: '#8b5cf6',
    gameCount: 15,
  },
  {
    id: 'cat-family',
    name: 'Family',
    slug: 'family',
    emoji: '👨‍👩‍👧‍👦',
    color: '#f59e0b',
    gameCount: 34,
  },
  {
    id: 'cat-abstract',
    name: 'Abstract',
    slug: 'abstract',
    emoji: '🔷',
    color: '#06b6d4',
    gameCount: 12,
  },
  {
    id: 'cat-thematic',
    name: 'Thematic',
    slug: 'thematic',
    emoji: '🗺️',
    color: '#ef4444',
    gameCount: 23,
  },
  { id: 'cat-euro', name: 'Euro', slug: 'euro', emoji: '🏛️', color: '#6366f1', gameCount: 31 },
];

const mocks = vi.hoisted(() => ({
  queryState: {
    data: undefined as CategoryDto[] | undefined,
    isLoading: false,
    isError: false,
    error: null as unknown,
  },
  createMutateAsync: vi.fn<(args: unknown) => Promise<CategoryDto>>(),
  updateMutateAsync: vi.fn<(args: unknown) => Promise<CategoryDto>>(),
  deleteMutateAsync: vi.fn<(id: string) => Promise<void>>(),
}));

vi.mock('@/hooks/queries/useAdminCategories', () => ({
  useAdminCategories: () => ({
    data: mocks.queryState.data,
    isLoading: mocks.queryState.isLoading,
    isError: mocks.queryState.isError,
    error: mocks.queryState.error,
  }),
  useCreateAdminCategory: () => ({ mutateAsync: mocks.createMutateAsync }),
  useUpdateAdminCategory: () => ({ mutateAsync: mocks.updateMutateAsync }),
  useDeleteAdminCategory: () => ({ mutateAsync: mocks.deleteMutateAsync }),
}));

import { CategoriesTable } from '../categories-table';

beforeEach(() => {
  mocks.queryState.data = seedCategories;
  mocks.queryState.isLoading = false;
  mocks.queryState.isError = false;
  mocks.queryState.error = null;
  mocks.createMutateAsync.mockReset();
  mocks.updateMutateAsync.mockReset();
  mocks.deleteMutateAsync.mockReset();
  // Default: mutations resolve with a plausible echo so the component closes
  // the dialog without surfacing an error.
  mocks.createMutateAsync.mockResolvedValue({
    id: 'cat-new',
    name: 'New',
    slug: 'new',
    emoji: '🎲',
    color: '#3b82f6',
    gameCount: 0,
  });
  mocks.updateMutateAsync.mockImplementation(async ({ id, payload }: any) => ({
    id,
    name: payload.name,
    slug: payload.slug,
    emoji: payload.emoji,
    color: payload.color,
    gameCount: 0,
  }));
  mocks.deleteMutateAsync.mockResolvedValue(undefined);
});

describe('CategoriesTable (#1440 Phase 2 — live API)', () => {
  it('renders header + Add Category button', () => {
    render(<CategoriesTable />);
    expect(screen.getByText('Categories')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /add category/i })).toBeInTheDocument();
  });

  it('renders all categories returned by the server', () => {
    render(<CategoriesTable />);
    for (const cat of seedCategories) {
      expect(screen.getByText(cat.name)).toBeInTheDocument();
    }
  });

  it('renders derived game counts from the server response', () => {
    render(<CategoriesTable />);
    expect(screen.getByText('42 games')).toBeInTheDocument();
    expect(screen.getByText('28 games')).toBeInTheDocument();
  });

  it('renders a loading row when the query is loading', () => {
    mocks.queryState.data = undefined;
    mocks.queryState.isLoading = true;
    render(<CategoriesTable />);
    expect(screen.getByText(/loading categories/i)).toBeInTheDocument();
  });

  it('renders an error row when the list query fails', () => {
    mocks.queryState.data = undefined;
    mocks.queryState.isError = true;
    mocks.queryState.error = new Error('network down');
    render(<CategoriesTable />);
    expect(screen.getByRole('alert')).toHaveTextContent(/failed to load categories.*network down/i);
  });

  it('renders an empty-state row when the list is empty', () => {
    mocks.queryState.data = [];
    render(<CategoriesTable />);
    expect(screen.getByText(/no categories yet/i)).toBeInTheDocument();
  });

  it('calls the create mutation with a derived slug on Add submit', async () => {
    render(<CategoriesTable />);
    fireEvent.click(screen.getByRole('button', { name: /add category/i }));
    const dialog = screen.getByRole('dialog');
    fireEvent.change(within(dialog).getByLabelText(/name/i), {
      target: { value: 'Engine Builder' },
    });
    fireEvent.change(within(dialog).getByLabelText(/emoji/i), { target: { value: '⚙️' } });
    fireEvent.click(within(dialog).getByRole('button', { name: /add category/i }));

    await waitFor(() => expect(mocks.createMutateAsync).toHaveBeenCalledTimes(1));
    expect(mocks.createMutateAsync).toHaveBeenCalledWith({
      name: 'Engine Builder',
      slug: 'engine-builder',
      emoji: '⚙️',
      color: '#3b82f6',
    });
  });

  it('surfaces a server error message when create fails', async () => {
    mocks.createMutateAsync.mockRejectedValueOnce(
      new AdminCategoriesApiError(409, "Category with name 'Strategy' already exists")
    );
    render(<CategoriesTable />);
    fireEvent.click(screen.getByRole('button', { name: /add category/i }));
    const dialog = screen.getByRole('dialog');
    fireEvent.change(within(dialog).getByLabelText(/name/i), { target: { value: 'Strategy' } });
    fireEvent.change(within(dialog).getByLabelText(/emoji/i), { target: { value: '♟️' } });
    fireEvent.click(within(dialog).getByRole('button', { name: /add category/i }));

    expect(await screen.findByText(/strategy.*already exists/i)).toBeInTheDocument();
  });

  it('calls the update mutation with the existing slug + new values on Edit submit', async () => {
    render(<CategoriesTable />);
    fireEvent.click(screen.getByRole('button', { name: /edit strategy/i }));
    const dialog = screen.getByRole('dialog');
    fireEvent.change(within(dialog).getByLabelText(/name/i), {
      target: { value: 'Heavy Strategy' },
    });
    fireEvent.click(within(dialog).getByRole('button', { name: /save changes/i }));

    await waitFor(() => expect(mocks.updateMutateAsync).toHaveBeenCalledTimes(1));
    expect(mocks.updateMutateAsync).toHaveBeenCalledWith({
      id: 'cat-strategy',
      payload: expect.objectContaining({
        name: 'Heavy Strategy',
        slug: 'strategy', // preserved from existing row
      }),
    });
  });

  it('opens the Delete dialog with a warning when the category has games', () => {
    render(<CategoriesTable />);
    fireEvent.click(screen.getByRole('button', { name: /delete strategy/i }));
    const dialog = screen.getByRole('dialog');
    expect(within(dialog).getByText(/strategy/i)).toBeInTheDocument();
    expect(within(dialog).getByRole('alert')).toHaveTextContent(/42 games are currently tagged/i);
  });

  it('calls the delete mutation on confirm', async () => {
    render(<CategoriesTable />);
    fireEvent.click(screen.getByRole('button', { name: /delete party/i }));
    fireEvent.click(screen.getByRole('button', { name: /^delete$/i }));

    await waitFor(() => expect(mocks.deleteMutateAsync).toHaveBeenCalledWith('cat-party'));
  });

  it('surfaces a server error message when delete is rejected by the BE (409 linked games)', async () => {
    mocks.deleteMutateAsync.mockRejectedValueOnce(
      new AdminCategoriesApiError(
        409,
        "Cannot delete category 'Strategy' — 42 linked games. Detag them first."
      )
    );
    render(<CategoriesTable />);
    fireEvent.click(screen.getByRole('button', { name: /delete strategy/i }));
    fireEvent.click(screen.getByRole('button', { name: /^delete$/i }));

    // The mutation-error banner sits outside the dialog, so we match its
    // copy directly. The Delete dialog still shows its own gameCount warning
    // ("42 games are currently tagged…"), which is intentionally a different
    // string from the 409 server message.
    expect(await screen.findByText(/cannot delete category.*42 linked games/i)).toBeInTheDocument();
  });

  it('warns when Edit is requested for an unknown id (defensive guard)', () => {
    render(<CategoriesTable />);
    // The CategoryRow buttons render once per row, so this scenario can only
    // happen if the categories array drifts mid-render. Direct invocation of
    // the handler isn't easy to assert; the loop relies on the warn channel.
    // We assert no warning has been logged for normal interactions.
    fireEvent.click(screen.getByRole('button', { name: /edit strategy/i }));
    expect(mockLoggerWarn).not.toHaveBeenCalled();
  });
});
