import { render, screen } from '@testing-library/react';
import { LayoutDashboard } from 'lucide-react';
import { describe, expect, it, vi } from 'vitest';

import { CardRackItem } from '../CardRackItem';

vi.mock('next/link', () => ({
  default: ({ children, href, ...props }: any) => (
    <a href={href} {...props}>
      {children}
    </a>
  ),
}));

vi.mock('next/navigation', () => ({
  usePathname: () => '/dashboard',
}));

describe('CardRackItem', () => {
  it('renders icon with aria-label', () => {
    render(<CardRackItem href="/library" icon={LayoutDashboard} label="Dashboard" />);
    const link = screen.getByRole('link', { name: 'Dashboard' });
    expect(link).toBeInTheDocument();
    expect(link).toHaveAttribute('href', '/library');
  });

  it('shows label when isExpanded is true', () => {
    render(<CardRackItem href="/library" icon={LayoutDashboard} label="Dashboard" isExpanded />);
    expect(screen.getByText('Dashboard')).toBeVisible();
  });

  it('applies active styles when isActive', () => {
    render(<CardRackItem href="/library" icon={LayoutDashboard} label="Dashboard" isActive />);
    const link = screen.getByRole('link', { name: 'Dashboard' });
    expect(link.className).toContain('font-semibold');
  });

  it('renders notification badge when provided', () => {
    render(<CardRackItem href="/chat" icon={LayoutDashboard} label="Chat" badge={3} />);
    expect(screen.getByText('3')).toBeInTheDocument();
  });

  it('shows 99+ for badges over 99', () => {
    render(<CardRackItem href="/chat" icon={LayoutDashboard} label="Chat" badge={150} />);
    expect(screen.getByText('99+')).toBeInTheDocument();
  });
});
