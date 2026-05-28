import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';

vi.mock('next/link', () => ({
  default: ({ children, href, ...props }: any) => (
    <a href={href} {...props}>
      {children}
    </a>
  ),
}));

vi.mock('next/navigation', () => ({
  usePathname: () => '/players',
  useRouter: () => ({ push: vi.fn() }),
}));

vi.mock('@/hooks/queries/useCurrentUser', () => ({
  useCurrentUser: () => ({
    data: { displayName: 'Mario', email: 'mario@test.com', role: 'admin' },
  }),
}));

vi.mock('@/actions/auth', () => ({
  logoutAction: vi.fn().mockResolvedValue({ success: true }),
}));

import { SideDrawer } from '@/components/layout/SideDrawer/SideDrawer';

describe('SideDrawer', () => {
  const defaultProps = {
    open: true,
    onClose: vi.fn(),
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(() => {
    document.body.style.overflow = '';
  });

  it('renders the secondary ("tutto il resto") destinations when open', () => {
    render(<SideDrawer {...defaultProps} />);
    expect(screen.getByText('Serate')).toBeDefined();
    expect(screen.getByText('Agenti')).toBeDefined();
    expect(screen.getByText('Giocatori')).toBeDefined();
    expect(screen.getByText('Knowledge Base')).toBeDefined();
    expect(screen.getByText('Toolkit')).toBeDefined();
  });

  it('does NOT render items that live in the bottom bar', () => {
    render(<SideDrawer {...defaultProps} />);
    expect(screen.queryByText('Hub')).toBeNull();
    expect(screen.queryByText('Chat')).toBeNull();
    expect(screen.queryByText('Dashboard')).toBeNull();
  });

  it('does not render when closed', () => {
    render(<SideDrawer open={false} onClose={vi.fn()} />);
    expect(screen.queryByText('Serate')).toBeNull();
  });

  it('highlights the active route with aria-current="page"', () => {
    render(<SideDrawer {...defaultProps} />);
    expect(screen.getByText('Giocatori').closest('a')?.getAttribute('aria-current')).toBe('page');
    expect(screen.getByText('Agenti').closest('a')?.getAttribute('aria-current')).toBeNull();
  });

  it('shows Admin Hub and Editor Agenti for admin users', () => {
    render(<SideDrawer {...defaultProps} />);
    expect(screen.getByText('Admin Hub')).toBeDefined();
    expect(screen.getByText('Editor Agenti')).toBeDefined();
  });

  it('shows user info (name, email, initial)', () => {
    render(<SideDrawer {...defaultProps} />);
    expect(screen.getByText('Mario')).toBeDefined();
    expect(screen.getByText('mario@test.com')).toBeDefined();
    expect(screen.getByText('M')).toBeDefined();
  });

  it('renders Impostazioni and Esci in the footer', () => {
    render(<SideDrawer {...defaultProps} />);
    expect(screen.getByText('Impostazioni')).toBeDefined();
    expect(screen.getByText('Esci')).toBeDefined();
  });

  it('calls onClose when overlay clicked', () => {
    const onClose = vi.fn();
    render(<SideDrawer open onClose={onClose} />);
    fireEvent.click(screen.getByTestId('drawer-overlay'));
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('calls onClose when Escape key is pressed', () => {
    const onClose = vi.fn();
    render(<SideDrawer open onClose={onClose} />);
    fireEvent.keyDown(document, { key: 'Escape', code: 'Escape' });
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('prevents body scroll when open and restores when closed', () => {
    const { rerender } = render(<SideDrawer open onClose={vi.fn()} />);
    expect(document.body.style.overflow).toBe('hidden');
    rerender(<SideDrawer open={false} onClose={vi.fn()} />);
    expect(document.body.style.overflow).toBe('');
  });
});
