/**
 * CategoriesTable Tests
 * Issue #4862: Admin table with EntityTableView design system
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { CategoriesTable, type GameCategory } from '../CategoriesTable';

const mockEntityTableView = vi.fn(() => null);
vi.mock('@/components/ui/data-display/entity-list-view', () => ({
  EntityTableView: (props: Record<string, unknown>) => {
    mockEntityTableView(props);
    return <div data-testid={props['data-testid'] as string}>EntityTableView</div>;
  },
}));

const mockCategories: GameCategory[] = [
  { id: 'cat-1', name: 'Strategy', gameCount: 42, description: 'Games requiring strategic thinking' },
  { id: 'cat-2', name: 'Party', gameCount: 15, description: 'Social and party games' },
  { id: 'cat-3', name: 'Cooperative', gameCount: 8 },
];

describe('CategoriesTable', () => {
  beforeEach(() => {
    mockEntityTableView.mockClear();
  });

  it('renders with entity="game"', () => {
    render(<CategoriesTable categories={mockCategories} />);

    expect(mockEntityTableView).toHaveBeenCalledWith(
      expect.objectContaining({ entity: 'game' })
    );
  });

  it('passes categories as displayItems and items', () => {
    render(<CategoriesTable categories={mockCategories} />);

    expect(mockEntityTableView).toHaveBeenCalledWith(
      expect.objectContaining({
        displayItems: mockCategories,
        items: mockCategories,
      })
    );
  });

  it('provides 3 tableColumns', () => {
    render(<CategoriesTable categories={mockCategories} />);

    const call = mockEntityTableView.mock.calls[0]?.[0] as Record<string, unknown> | undefined;
    const columns = call?.tableColumns as Array<{ id: string; header: string }>;

    expect(columns).toHaveLength(3);
    expect(columns.map((c) => c.header)).toEqual(['Name', 'Games', 'Description']);
  });

  it('renderItem maps category to correct props', () => {
    render(<CategoriesTable categories={mockCategories} />);

    const call = mockEntityTableView.mock.calls[0]?.[0] as Record<string, unknown> | undefined;
    const renderItem = call?.renderItem as (c: GameCategory) => Record<string, unknown>;
    const result = renderItem(mockCategories[0]);

    expect(result.title).toBe('Strategy');
    expect(result.id).toBe('cat-1');
    expect(result.subtitle).toBe('Games requiring strategic thinking');
  });

  it('renderItem includes game count in metadata', () => {
    render(<CategoriesTable categories={mockCategories} />);

    const call = mockEntityTableView.mock.calls[0]?.[0] as Record<string, unknown> | undefined;
    const renderItem = call?.renderItem as (c: GameCategory) => Record<string, unknown>;
    const result = renderItem(mockCategories[0]);
    const metadata = result.metadata as Array<{ value: string }>;

    expect(metadata[0].value).toBe('42');
  });

  it('passes onCategoryClick as onItemClick', () => {
    const onClick = vi.fn();
    render(<CategoriesTable categories={mockCategories} onCategoryClick={onClick} />);

    expect(mockEntityTableView).toHaveBeenCalledWith(
      expect.objectContaining({ onItemClick: onClick })
    );
  });

  it('sets correct data-testid', () => {
    render(<CategoriesTable categories={mockCategories} />);

    expect(screen.getByTestId('categories-table')).toBeInTheDocument();
  });

  it('handles empty categories', () => {
    render(<CategoriesTable categories={[]} />);

    expect(mockEntityTableView).toHaveBeenCalledWith(
      expect.objectContaining({
        displayItems: [],
        emptyMessage: 'No categories found',
      })
    );
  });

  it('handles category without description', () => {
    render(<CategoriesTable categories={mockCategories} />);

    const call = mockEntityTableView.mock.calls[0]?.[0] as Record<string, unknown> | undefined;
    const renderItem = call?.renderItem as (c: GameCategory) => Record<string, unknown>;
    const result = renderItem(mockCategories[2]);

    expect(result.subtitle).toBeUndefined();
  });
});
