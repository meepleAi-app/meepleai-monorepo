import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { LibraryGrid } from '../LibraryGrid';
import type { ComponentEntry } from '@/config/component-registry';

vi.mock('next/link', () => ({
  default: ({ children, href }: { children: React.ReactNode; href: string }) => (
    <a href={href}>{children}</a>
  ),
}));

const mockEntries: ComponentEntry[] = [
  {
    id: 'meeple-card',
    name: 'MeepleCard',
    importPath: '@/components/ui/data-display/meeple-card/MeepleCard',
    category: 'Data Display',
    areas: ['shared'],
    tier: 'interactive',
    description: 'Universal card for entities.',
  },
  {
    id: 'badge',
    name: 'Badge',
    importPath: '@/components/ui/data-display/badge',
    category: 'Tags',
    areas: ['shared'],
    tier: 'static',
    description: 'Small label badge.',
  },
  {
    id: 'stat-card',
    name: 'StatCard',
    importPath: '@/components/ui/data-display/stat-card',
    category: 'Data Display',
    areas: ['admin'],
    tier: 'static',
    description: 'KPI stat card.',
  },
];

describe('LibraryGrid', () => {
  it('renders all entries as cards', () => {
    render(<LibraryGrid entries={mockEntries} />);

    expect(screen.getByText('MeepleCard')).toBeInTheDocument();
    expect(screen.getByText('Badge')).toBeInTheDocument();
    expect(screen.getByText('StatCard')).toBeInTheDocument();
  });

  it('renders correct number of list items', () => {
    render(<LibraryGrid entries={mockEntries} />);
    const items = screen.getAllByRole('listitem');
    expect(items).toHaveLength(3);
  });

  it('shows empty state when no entries', () => {
    render(<LibraryGrid entries={[]} />);
    expect(screen.getByText('No components found')).toBeInTheDocument();
  });

  it('shows helpful message in empty state', () => {
    render(<LibraryGrid entries={[]} />);
    expect(screen.getByText('Try adjusting your filters or search query.')).toBeInTheDocument();
  });

  it('renders correct links for each entry', () => {
    render(<LibraryGrid entries={mockEntries} />);
    const links = screen.getAllByRole('link');
    const hrefs = links.map(l => l.getAttribute('href'));

    expect(hrefs).toContain('/admin/ui-library/meeple-card');
    expect(hrefs).toContain('/admin/ui-library/badge');
    expect(hrefs).toContain('/admin/ui-library/stat-card');
  });
});
