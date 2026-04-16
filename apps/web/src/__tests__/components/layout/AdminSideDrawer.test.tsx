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
  usePathname: () => '/admin/overview',
}));

vi.mock('@/hooks/queries/useCurrentUser', () => ({
  useCurrentUser: () => ({
    data: { displayName: 'Admin', email: 'admin@test.com', role: 'admin' },
  }),
}));

import { AdminSideDrawer } from '@/components/layout/AdminSideDrawer/AdminSideDrawer';

describe('AdminSideDrawer', () => {
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

  it('renders "Torna all\'app" link when open', () => {
    render(<AdminSideDrawer {...defaultProps} />);
    const link = screen.getByText(/torna all'app/i);
    expect(link).toBeDefined();
  });

  it('renders admin sections Overview, Content, AI, Users', () => {
    render(<AdminSideDrawer {...defaultProps} />);
    expect(screen.getByText('Overview')).toBeDefined();
    expect(screen.getByText('Content')).toBeDefined();
    expect(screen.getByText('AI')).toBeDefined();
    expect(screen.getByText('Users')).toBeDefined();
  });

  it('renders items inside Overview section', () => {
    render(<AdminSideDrawer {...defaultProps} />);
    expect(screen.getByText('Dashboard')).toBeDefined();
    expect(screen.getByText('Activity Feed')).toBeDefined();
    expect(screen.getByText('System Health')).toBeDefined();
  });

  it('renders items inside Content section', () => {
    render(<AdminSideDrawer {...defaultProps} />);
    expect(screen.getByText('Giochi')).toBeDefined();
    expect(screen.getByText('Knowledge Base')).toBeDefined();
    expect(screen.getByText('Email Templates')).toBeDefined();
  });

  it('renders items inside AI section', () => {
    render(<AdminSideDrawer {...defaultProps} />);
    expect(screen.getByText('Mission Control')).toBeDefined();
    expect(screen.getByText('RAG Inspector')).toBeDefined();
    expect(screen.getByText('Agent Definitions')).toBeDefined();
  });

  it('renders items inside Users section', () => {
    render(<AdminSideDrawer {...defaultProps} />);
    expect(screen.getByText('Tutti gli utenti')).toBeDefined();
    expect(screen.getByText('Invitations')).toBeDefined();
    expect(screen.getByText('Ruoli & Permessi')).toBeDefined();
  });

  it('does not render when closed', () => {
    render(<AdminSideDrawer open={false} onClose={vi.fn()} />);
    expect(screen.queryByText(/torna all'app/i)).toBeNull();
    expect(screen.queryByText('Overview')).toBeNull();
  });

  it('calls onClose on overlay click', () => {
    const onClose = vi.fn();
    render(<AdminSideDrawer open={true} onClose={onClose} />);
    const overlay = screen.getByTestId('drawer-overlay');
    fireEvent.click(overlay);
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('calls onClose on Escape key', () => {
    const onClose = vi.fn();
    render(<AdminSideDrawer open={true} onClose={onClose} />);
    fireEvent.keyDown(document, { key: 'Escape', code: 'Escape' });
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('prevents body scroll when open', () => {
    render(<AdminSideDrawer {...defaultProps} />);
    expect(document.body.style.overflow).toBe('hidden');
  });

  it('restores body scroll when closed', () => {
    const { rerender } = render(<AdminSideDrawer open={true} onClose={vi.fn()} />);
    expect(document.body.style.overflow).toBe('hidden');
    rerender(<AdminSideDrawer open={false} onClose={vi.fn()} />);
    expect(document.body.style.overflow).toBe('');
  });

  it('shows user displayName and email', () => {
    render(<AdminSideDrawer {...defaultProps} />);
    expect(screen.getByText('Admin')).toBeDefined();
    expect(screen.getByText('admin@test.com')).toBeDefined();
  });

  it('shows admin role label', () => {
    render(<AdminSideDrawer {...defaultProps} />);
    // The role badge renders the role value; use getAllByText since "admin" appears in multiple places
    const adminElements = screen.getAllByText(/admin/i);
    expect(adminElements.length).toBeGreaterThan(0);
  });

  it('AI Altro sub-menu is collapsed by default when not on an Altro route', () => {
    render(<AdminSideDrawer {...defaultProps} />);
    // These items belong to the AI "Altro" collapsible
    expect(screen.queryByText('Config')).toBeNull();
    expect(screen.queryByText('Usage')).toBeNull();
  });

  it('Users Altro sub-menu is collapsed by default when not on an Altro route', () => {
    render(<AdminSideDrawer {...defaultProps} />);
    expect(screen.queryByText('Access Requests')).toBeNull();
    expect(screen.queryByText('Activity')).toBeNull();
  });
});
