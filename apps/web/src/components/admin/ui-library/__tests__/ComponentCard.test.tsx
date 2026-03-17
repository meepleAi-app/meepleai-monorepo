import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { ComponentCard } from '../ComponentCard';
import type { ComponentEntry } from '@/config/component-registry';

vi.mock('next/link', () => ({
  default: ({ children, href }: { children: React.ReactNode; href: string }) => (
    <a href={href}>{children}</a>
  ),
}));

const interactiveEntry: ComponentEntry = {
  id: 'meeple-card',
  name: 'MeepleCard',
  importPath: '@/components/ui/data-display/meeple-card/MeepleCard',
  category: 'Data Display',
  areas: ['shared'],
  tier: 'interactive',
  description: 'Universal card component for displaying entities.',
  tags: ['card'],
};

const staticEntry: ComponentEntry = {
  id: 'stat-card',
  name: 'StatCard',
  importPath: '@/components/ui/data-display/stat-card',
  category: 'Data Display',
  areas: ['admin', 'shared'],
  tier: 'static',
  description: 'A static stats card showing KPI values.',
};

describe('ComponentCard', () => {
  it('renders component name', () => {
    render(<ComponentCard entry={interactiveEntry} />);
    expect(screen.getByText('MeepleCard')).toBeInTheDocument();
  });

  it('renders description', () => {
    render(<ComponentCard entry={interactiveEntry} />);
    expect(
      screen.getByText('Universal card component for displaying entities.')
    ).toBeInTheDocument();
  });

  it('renders category badge', () => {
    render(<ComponentCard entry={interactiveEntry} />);
    expect(screen.getByText('Data Display')).toBeInTheDocument();
  });

  it('renders area badges', () => {
    render(<ComponentCard entry={staticEntry} />);
    expect(screen.getByText('admin')).toBeInTheDocument();
    expect(screen.getByText('shared')).toBeInTheDocument();
  });

  it('renders interactive tier indicator with Zap icon label', () => {
    render(<ComponentCard entry={interactiveEntry} />);
    expect(screen.getByLabelText('Interactive')).toBeInTheDocument();
  });

  it('renders static tier indicator with Camera icon label', () => {
    render(<ComponentCard entry={staticEntry} />);
    expect(screen.getByLabelText('Static')).toBeInTheDocument();
  });

  it('links to correct detail page', () => {
    render(<ComponentCard entry={interactiveEntry} />);
    const link = screen.getByRole('link');
    expect(link).toHaveAttribute('href', '/admin/ui-library/meeple-card');
  });

  it('links to correct detail page for different id', () => {
    render(<ComponentCard entry={staticEntry} />);
    const link = screen.getByRole('link');
    expect(link).toHaveAttribute('href', '/admin/ui-library/stat-card');
  });
});
