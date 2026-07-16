/**
 * categories-table.tsx (admin CRUD table) — axe AA accessibility gate.
 *
 * Distinct from the sibling `CategoriesTable.axe.test.tsx`, which guards the
 * EntityTableView `CategoriesTable.tsx`. This one guards the bespoke admin CRUD
 * `categories-table.tsx` (#1440), whose drag-handle header column was an empty
 * `<th>` (axe `empty-table-header`). Renders the live table via mocked
 * admin-category hooks + axe.
 */
import { render } from '@testing-library/react';
import { axe, toHaveNoViolations } from 'jest-axe';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import type { CategoryDto } from '@/lib/api/admin-categories';

vi.mock('@/lib/logger', () => ({
  logger: { debug: vi.fn(), info: vi.fn(), warn: vi.fn(), error: vi.fn() },
  getLogger: () => ({ debug: vi.fn(), info: vi.fn(), warn: vi.fn(), error: vi.fn() }),
  resetLogger: vi.fn(),
  LogLevel: { DEBUG: 'debug', INFO: 'info', WARN: 'warn', ERROR: 'error' },
}));

const mocks = vi.hoisted(() => ({
  queryState: {
    data: undefined as CategoryDto[] | undefined,
    isLoading: false,
    isError: false,
    error: null as unknown,
  },
}));

vi.mock('@/hooks/queries/useAdminCategories', () => ({
  useAdminCategories: () => ({
    data: mocks.queryState.data,
    isLoading: mocks.queryState.isLoading,
    isError: mocks.queryState.isError,
    error: mocks.queryState.error,
  }),
  useCreateAdminCategory: () => ({ mutateAsync: vi.fn() }),
  useUpdateAdminCategory: () => ({ mutateAsync: vi.fn() }),
  useDeleteAdminCategory: () => ({ mutateAsync: vi.fn() }),
}));

import { CategoriesTable } from '../categories-table';

const CATEGORIES: CategoryDto[] = [
  {
    id: 'cat-strategy',
    name: 'Strategy',
    slug: 'strategy',
    emoji: '♟️',
    color: '#3b82f6',
    gameCount: 42,
  },
  { id: 'cat-party', name: 'Party', slug: 'party', emoji: '🎉', color: '#ec4899', gameCount: 28 },
];

expect.extend(toHaveNoViolations);

beforeEach(() => {
  mocks.queryState.data = CATEGORIES;
  mocks.queryState.isLoading = false;
  mocks.queryState.isError = false;
  mocks.queryState.error = null;
});

describe('categories-table.tsx (admin CRUD) — axe AA gate', () => {
  it('has no axe violations rendering the admin categories table', async () => {
    const { container } = render(<CategoriesTable />);
    expect(await axe(container)).toHaveNoViolations();
  });
});
