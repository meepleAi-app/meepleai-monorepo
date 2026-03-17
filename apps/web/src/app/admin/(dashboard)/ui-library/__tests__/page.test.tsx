import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

vi.mock('next/link', () => ({
  default: ({ children, href }: { children: React.ReactNode; href: string }) => (
    <a href={href}>{children}</a>
  ),
}));

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
  useSearchParams: () => new URLSearchParams(),
}));

// Mock the component registry to avoid large import in tests
vi.mock('@/config/component-registry', () => ({
  COMPONENT_REGISTRY: [
    {
      id: 'badge',
      name: 'Badge',
      importPath: '@/components/ui/data-display/badge',
      category: 'Data Display',
      areas: ['shared'],
      tier: 'static',
      description: 'A compact status indicator.',
    },
    {
      id: 'meeple-card',
      name: 'MeepleCard',
      importPath: '@/components/ui/data-display/meeple-card/MeepleCard',
      category: 'Data Display',
      areas: ['shared'],
      tier: 'interactive',
      description: 'Universal card component.',
    },
  ],
  filterRegistry: (filters: Record<string, unknown>) => {
    // Return all items for simplicity in tests
    return [
      {
        id: 'badge',
        name: 'Badge',
        importPath: '@/components/ui/data-display/badge',
        category: 'Data Display',
        areas: ['shared'],
        tier: 'static',
        description: 'A compact status indicator.',
      },
      {
        id: 'meeple-card',
        name: 'MeepleCard',
        importPath: '@/components/ui/data-display/meeple-card/MeepleCard',
        category: 'Data Display',
        areas: ['shared'],
        tier: 'interactive',
        description: 'Universal card component.',
      },
    ];
  },
  getCategories: () => [{ category: 'Data Display', count: 2 }],
  getAreas: () => [{ area: 'shared', count: 2 }],
}));

import UILibraryPage from '../page';

describe('UILibraryPage', () => {
  it('renders page title "UI Library"', () => {
    render(<UILibraryPage />);
    expect(screen.getByRole('heading', { name: 'UI Library' })).toBeInTheDocument();
  });

  it('renders search filter input', () => {
    render(<UILibraryPage />);
    expect(screen.getByPlaceholderText('Search components...')).toBeInTheDocument();
  });

  it('renders component count in subtitle', () => {
    render(<UILibraryPage />);
    // Should mention count of components (multiple matches possible — getAllByText is fine)
    const matches = screen.getAllByText(/components/i);
    expect(matches.length).toBeGreaterThan(0);
  });

  it('renders compositions section when no filters active', () => {
    render(<UILibraryPage />);
    expect(screen.getByText('Compositions')).toBeInTheDocument();
  });

  it('renders link to compositions page', () => {
    render(<UILibraryPage />);
    const link = screen.getByRole('link', { name: /view compositions/i });
    expect(link).toHaveAttribute('href', '/admin/ui-library/compositions');
  });

  it('renders component cards in grid', () => {
    render(<UILibraryPage />);
    expect(screen.getByText('Badge')).toBeInTheDocument();
    expect(screen.getByText('MeepleCard')).toBeInTheDocument();
  });
});
