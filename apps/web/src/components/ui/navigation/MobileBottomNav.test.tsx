import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { MobileBottomNav } from './MobileBottomNav';

vi.mock('next/navigation', () => ({
  usePathname: () => '/dashboard',
  useRouter: () => ({ push: vi.fn() }),
}));

vi.mock('next/link', () => ({
  default: ({
    children,
    href,
    ...props
  }: {
    children: React.ReactNode;
    href: string;
    [key: string]: unknown;
  }) => (
    <a href={href} {...props}>
      {children}
    </a>
  ),
}));

describe('MobileBottomNav', () => {
  it('renders 5 navigation items', () => {
    render(<MobileBottomNav />);
    const nav = screen.getByRole('navigation', { name: /bottom/i });
    const links = nav.querySelectorAll('a, button');
    expect(links).toHaveLength(5);
  });

  it('renders Home, Libreria, Chat, Profilo labels', () => {
    render(<MobileBottomNav />);
    expect(screen.getByText('Home')).toBeInTheDocument();
    expect(screen.getByText('Libreria')).toBeInTheDocument();
    expect(screen.getByText('Chat')).toBeInTheDocument();
    expect(screen.getByText('Profilo')).toBeInTheDocument();
  });

  it('highlights active route', () => {
    render(<MobileBottomNav />);
    const homeLink = screen.getByText('Home').closest('a');
    expect(homeLink).toHaveAttribute('aria-current', 'page');
  });

  it('is hidden on desktop (lg:hidden class)', () => {
    const { container } = render(<MobileBottomNav />);
    expect(container.firstChild).toHaveClass('lg:hidden');
  });

  it('has fixed bottom positioning', () => {
    const { container } = render(<MobileBottomNav />);
    expect(container.firstChild).toHaveClass('fixed');
    expect(container.firstChild).toHaveClass('bottom-0');
  });

  it('is not rendered when hidden prop is true', () => {
    const { container } = render(<MobileBottomNav hidden />);
    expect(container.firstChild).toBeNull();
  });
});
