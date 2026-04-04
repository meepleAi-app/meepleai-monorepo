import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

vi.mock('next/link', () => ({
  default: ({ children, href }: { children: React.ReactNode; href: string }) => (
    <a href={href}>{children}</a>
  ),
}));

vi.mock('next/dynamic', () => ({
  default: () => () => <div data-testid="lazy-loaded" />,
}));

vi.mock('@/components/showcase/stories', () => ({
  STORY_MAP: {},
}));

import { ComponentDetail } from '../ComponentDetail';
import type { ComponentEntry } from '@/config/component-registry';

const staticEntry: ComponentEntry = {
  id: 'badge',
  name: 'Badge',
  importPath: '@/components/ui/data-display/badge',
  category: 'Data Display',
  areas: ['shared', 'admin'],
  tier: 'static',
  description: 'A compact status indicator component.',
  tags: ['status', 'label'],
  mockProps: { children: 'New', variant: 'default' },
};

const interactiveEntry: ComponentEntry = {
  id: 'meeple-card',
  name: 'MeepleCard',
  importPath: '@/components/ui/data-display/meeple-card/MeepleCard',
  category: 'Data Display',
  areas: ['shared'],
  tier: 'interactive',
  description: 'Universal card component for displaying entities.',
  tags: ['card', 'entity'],
  compositions: ['game-list', 'player-roster'],
};

const entryWithCompositions: ComponentEntry = {
  ...staticEntry,
  id: 'stat-card',
  name: 'StatCard',
  compositions: ['kpi-row', 'admin-overview'],
};

describe('ComponentDetail', () => {
  it('renders component name', () => {
    render(<ComponentDetail entry={staticEntry} />);
    expect(screen.getByRole('heading', { name: 'Badge' })).toBeInTheDocument();
  });

  it('renders component description', () => {
    render(<ComponentDetail entry={staticEntry} />);
    expect(screen.getByText('A compact status indicator component.')).toBeInTheDocument();
  });

  it('renders back link to /admin/ui-library', () => {
    render(<ComponentDetail entry={staticEntry} />);
    // The link text is "UI Library"; aria-label is on the element itself
    const backLink = screen.getByRole('link', { name: /ui library/i });
    expect(backLink).toHaveAttribute('href', '/admin/ui-library');
  });

  it('renders Static tier indicator for static entry', () => {
    render(<ComponentDetail entry={staticEntry} />);
    expect(screen.getByLabelText('Static')).toBeInTheDocument();
  });

  it('renders Interactive tier indicator for interactive entry', () => {
    render(<ComponentDetail entry={interactiveEntry} />);
    expect(screen.getByLabelText('Interactive')).toBeInTheDocument();
  });

  it('renders category badge', () => {
    render(<ComponentDetail entry={staticEntry} />);
    // category appears in the badges row — use getAllByText in case it repeats
    const badges = screen.getAllByText('Data Display');
    expect(badges.length).toBeGreaterThan(0);
  });

  it('renders area badges', () => {
    render(<ComponentDetail entry={staticEntry} />);
    expect(screen.getByText('shared')).toBeInTheDocument();
    expect(screen.getByText('admin')).toBeInTheDocument();
  });

  it('renders import path code block', () => {
    render(<ComponentDetail entry={staticEntry} />);
    const importBlock = screen.getByTestId('import-path');
    expect(importBlock.textContent).toContain('@/components/ui/data-display/badge');
    expect(importBlock.textContent).toContain('Badge');
  });

  it('renders compositions links when present', () => {
    render(<ComponentDetail entry={entryWithCompositions} />);
    expect(screen.getByRole('link', { name: 'kpi-row' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'admin-overview' })).toBeInTheDocument();
  });

  it('does not render compositions section when none defined', () => {
    render(<ComponentDetail entry={staticEntry} />);
    expect(screen.queryByText('Used in Compositions')).not.toBeInTheDocument();
  });

  it('renders Props section heading', () => {
    render(<ComponentDetail entry={staticEntry} />);
    expect(screen.getByRole('heading', { name: 'Props' })).toBeInTheDocument();
  });

  it('renders lazy-loaded static renderer for static tier without story', () => {
    render(<ComponentDetail entry={staticEntry} />);
    expect(screen.getByTestId('lazy-loaded')).toBeInTheDocument();
  });
});
