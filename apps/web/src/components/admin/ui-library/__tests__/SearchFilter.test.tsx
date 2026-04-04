import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { SearchFilter } from '../SearchFilter';
import type { LibraryFilters } from '../SearchFilter';

vi.mock('next/link', () => ({
  default: ({ children, href }: { children: React.ReactNode; href: string }) => (
    <a href={href}>{children}</a>
  ),
}));

const mockCategories = [
  { category: 'Data Display' as const, count: 5 },
  { category: 'Navigation' as const, count: 3 },
];

const mockAreas = [
  { area: 'shared' as const, count: 6 },
  { area: 'admin' as const, count: 2 },
];

const defaultFilters: LibraryFilters = {};

describe('SearchFilter', () => {
  it('renders search input', () => {
    render(
      <SearchFilter
        categories={mockCategories}
        areas={mockAreas}
        filters={defaultFilters}
        onChange={vi.fn()}
        totalCount={10}
        filteredCount={10}
      />
    );

    expect(screen.getByPlaceholderText('Search components...')).toBeInTheDocument();
  });

  it('calls onChange when search input changes', () => {
    const onChange = vi.fn();
    render(
      <SearchFilter
        categories={mockCategories}
        areas={mockAreas}
        filters={defaultFilters}
        onChange={onChange}
        totalCount={10}
        filteredCount={10}
      />
    );

    const input = screen.getByPlaceholderText('Search components...');
    fireEvent.change(input, { target: { value: 'button' } });

    expect(onChange).toHaveBeenCalledWith(expect.objectContaining({ search: 'button' }));
  });

  it('shows count badge with filtered/total', () => {
    render(
      <SearchFilter
        categories={mockCategories}
        areas={mockAreas}
        filters={defaultFilters}
        onChange={vi.fn()}
        totalCount={20}
        filteredCount={7}
      />
    );

    expect(screen.getByText('7 / 20')).toBeInTheDocument();
  });

  it('renders category, area, and tier selects', () => {
    render(
      <SearchFilter
        categories={mockCategories}
        areas={mockAreas}
        filters={defaultFilters}
        onChange={vi.fn()}
        totalCount={10}
        filteredCount={10}
      />
    );

    expect(screen.getByLabelText('Filter by category')).toBeInTheDocument();
    expect(screen.getByLabelText('Filter by area')).toBeInTheDocument();
    expect(screen.getByLabelText('Filter by tier')).toBeInTheDocument();
  });

  it('calls onChange with category when category select changes', () => {
    const onChange = vi.fn();
    render(
      <SearchFilter
        categories={mockCategories}
        areas={mockAreas}
        filters={defaultFilters}
        onChange={onChange}
        totalCount={10}
        filteredCount={10}
      />
    );

    const select = screen.getByLabelText('Filter by category');
    fireEvent.change(select, { target: { value: 'Data Display' } });

    expect(onChange).toHaveBeenCalledWith(expect.objectContaining({ category: 'Data Display' }));
  });

  it('reflects current filter values', () => {
    const filters: LibraryFilters = { search: 'card', tier: 'interactive' };
    render(
      <SearchFilter
        categories={mockCategories}
        areas={mockAreas}
        filters={filters}
        onChange={vi.fn()}
        totalCount={10}
        filteredCount={4}
      />
    );

    const input = screen.getByPlaceholderText('Search components...');
    expect((input as HTMLInputElement).value).toBe('card');
    const tierSelect = screen.getByLabelText('Filter by tier') as HTMLSelectElement;
    expect(tierSelect.value).toBe('interactive');
  });
});
