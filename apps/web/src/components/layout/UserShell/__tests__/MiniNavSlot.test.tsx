import { render, screen } from '@testing-library/react';
import { describe, it, expect, beforeEach } from 'vitest';

import { useMiniNavConfigStore } from '@/lib/stores/mini-nav-config-store';

import { MiniNavSlot } from '../MiniNavSlot';

describe('MiniNavSlot', () => {
  beforeEach(() => {
    useMiniNavConfigStore.getState().clear();
  });

  it('renders nothing when config is null', () => {
    const { container } = render(<MiniNavSlot />);
    expect(container).toBeEmptyDOMElement();
  });

  it('renders breadcrumb and tabs when config is set', () => {
    useMiniNavConfigStore.getState().setConfig({
      breadcrumb: 'Libreria · Hub',
      tabs: [
        { id: 'hub', label: 'Hub', href: '/library' },
        { id: 'personal', label: 'Personal', href: '/library?tab=personal', count: 47 },
      ],
      activeTabId: 'hub',
    });
    render(<MiniNavSlot />);
    expect(screen.getByText(/Libreria/i)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Hub/i })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Personal/i })).toBeInTheDocument();
    expect(screen.getByText('47')).toBeInTheDocument();
  });

  it('marks active tab with aria-current', () => {
    useMiniNavConfigStore.getState().setConfig({
      breadcrumb: 'Libreria',
      tabs: [
        { id: 'hub', label: 'Hub', href: '/library' },
        { id: 'personal', label: 'Personal', href: '/library?tab=personal' },
      ],
      activeTabId: 'personal',
    });
    render(<MiniNavSlot />);
    expect(screen.getByRole('link', { name: /Personal/i })).toHaveAttribute('aria-current', 'page');
  });

  it('renders primary action button when provided', () => {
    useMiniNavConfigStore.getState().setConfig({
      breadcrumb: 'Home',
      tabs: [{ id: 'a', label: 'A', href: '/' }],
      activeTabId: 'a',
      primaryAction: { label: '＋ Nuova partita', onClick: () => {} },
    });
    render(<MiniNavSlot />);
    expect(screen.getByRole('button', { name: /Nuova partita/i })).toBeInTheDocument();
  });

  it('renders the primary action icon when provided', () => {
    useMiniNavConfigStore.getState().setConfig({
      breadcrumb: 'Home',
      tabs: [{ id: 'a', label: 'A', href: '/' }],
      activeTabId: 'a',
      primaryAction: {
        label: 'Nuova partita',
        icon: '＋',
        onClick: () => {},
      },
    });
    render(<MiniNavSlot />);
    expect(screen.getByText('＋')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Nuova partita/i })).toBeInTheDocument();
  });
});
