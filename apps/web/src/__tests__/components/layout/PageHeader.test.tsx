import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';

import { PageHeader } from '@/components/layout/PageHeader';

// Mock next/link
vi.mock('next/link', () => ({
  default: ({
    href,
    children,
    ...props
  }: {
    href: string;
    children: React.ReactNode;
    [key: string]: unknown;
  }) => (
    <a href={href} {...props}>
      {children}
    </a>
  ),
}));

describe('PageHeader', () => {
  it('renders the title', () => {
    render(<PageHeader title="My Games" />);
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('My Games');
  });

  it('renders back link when parentHref and parentLabel are provided', () => {
    render(<PageHeader title="Game Detail" parentHref="/games" parentLabel="Games" />);
    const link = screen.getByRole('link', { name: /games/i });
    expect(link).toBeInTheDocument();
    expect(link).toHaveAttribute('href', '/games');
  });

  it('does not render back link when parentHref is not provided', () => {
    render(<PageHeader title="My Games" />);
    expect(screen.queryByRole('link')).not.toBeInTheDocument();
  });

  it('does not render back link when parentLabel is not provided', () => {
    render(<PageHeader title="My Games" parentHref="/games" />);
    expect(screen.queryByRole('link')).not.toBeInTheDocument();
  });

  it('renders subtitle when provided', () => {
    render(<PageHeader title="My Games" subtitle="Manage your board game collection" />);
    expect(screen.getByText('Manage your board game collection')).toBeInTheDocument();
  });

  it('does not render subtitle when not provided', () => {
    render(<PageHeader title="My Games" />);
    // No muted text aside from the title
    expect(screen.queryByText(/manage/i)).not.toBeInTheDocument();
  });

  it('renders primary action button when provided', () => {
    const handleClick = vi.fn();
    render(
      <PageHeader title="My Games" primaryAction={{ label: 'Add Game', onClick: handleClick }} />
    );
    const button = screen.getByRole('button', { name: 'Add Game' });
    expect(button).toBeInTheDocument();
  });

  it('calls onClick when primary action button is clicked', () => {
    const handleClick = vi.fn();
    render(
      <PageHeader title="My Games" primaryAction={{ label: 'Add Game', onClick: handleClick }} />
    );
    fireEvent.click(screen.getByRole('button', { name: 'Add Game' }));
    expect(handleClick).toHaveBeenCalledTimes(1);
  });

  it('renders primary action icon when provided', () => {
    render(
      <PageHeader
        title="My Games"
        primaryAction={{
          label: 'Add Game',
          onClick: vi.fn(),
          icon: <span data-testid="action-icon">+</span>,
        }}
      />
    );
    expect(screen.getByTestId('action-icon')).toBeInTheDocument();
  });

  it('does not render primary action button when not provided', () => {
    render(<PageHeader title="My Games" />);
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
  });

  it('renders tabs when provided', () => {
    const tabs = [
      { id: 'all', label: 'All Games', href: '/games' },
      { id: 'wishlist', label: 'Wishlist', href: '/games/wishlist' },
    ];
    render(<PageHeader title="My Games" tabs={tabs} activeTabId="all" />);
    expect(screen.getByText('All Games')).toBeInTheDocument();
    expect(screen.getByText('Wishlist')).toBeInTheDocument();
  });

  it('marks the active tab with aria-current="page"', () => {
    const tabs = [
      { id: 'all', label: 'All Games', href: '/games' },
      { id: 'wishlist', label: 'Wishlist', href: '/games/wishlist' },
    ];
    render(<PageHeader title="My Games" tabs={tabs} activeTabId="wishlist" />);
    const wishlistLink = screen.getByRole('link', { name: /wishlist/i });
    expect(wishlistLink).toHaveAttribute('aria-current', 'page');
  });

  it('does not mark inactive tabs with aria-current', () => {
    const tabs = [
      { id: 'all', label: 'All Games', href: '/games' },
      { id: 'wishlist', label: 'Wishlist', href: '/games/wishlist' },
    ];
    render(<PageHeader title="My Games" tabs={tabs} activeTabId="wishlist" />);
    const allGamesLink = screen.getByRole('link', { name: /all games/i });
    expect(allGamesLink).not.toHaveAttribute('aria-current', 'page');
  });

  it('renders tab count badge when count is provided', () => {
    const tabs = [{ id: 'all', label: 'All Games', href: '/games', count: 42 }];
    render(<PageHeader title="My Games" tabs={tabs} />);
    expect(screen.getByText('42')).toBeInTheDocument();
  });

  it('does not render count badge when count is not provided', () => {
    const tabs = [{ id: 'all', label: 'All Games', href: '/games' }];
    render(<PageHeader title="My Games" tabs={tabs} />);
    // No numeric badge
    expect(screen.queryByText(/^\d+$/)).not.toBeInTheDocument();
  });

  it('accepts and applies className', () => {
    const { container } = render(<PageHeader title="My Games" className="custom-class" />);
    expect(container.firstChild).toHaveClass('custom-class');
  });
});
