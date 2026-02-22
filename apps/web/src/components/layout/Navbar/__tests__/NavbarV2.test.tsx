/**
 * Navbar (v2) Tests
 * Issue #5036 — Navbar Component
 *
 * Tests: new Navbar, NavbarDropdown, NavbarUserMenu, NavbarMobileDrawer
 */

import { render, screen, fireEvent, act } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { Navbar } from '../Navbar';
import { NavbarDropdown } from '../NavbarDropdown';
import { NavbarMobileDrawer } from '../NavbarMobileDrawer';
import { NavbarUserMenu } from '../NavbarUserMenu';
import { Home, Settings } from 'lucide-react';

// ─── Mocks ────────────────────────────────────────────────────────────────────

vi.mock('next/navigation', () => ({
  usePathname: () => '/dashboard',
  useRouter: () => ({ push: vi.fn() }),
}));

const mockCurrentUser = vi.fn(() => ({
  data: null,
  isLoading: false,
}));

vi.mock('@/hooks/queries/useCurrentUser', () => ({
  useCurrentUser: () => mockCurrentUser(),
}));

vi.mock('@/actions/auth', () => ({
  logoutAction: vi.fn(async () => ({ success: true })),
}));

vi.mock('@/components/notifications', () => ({
  NotificationBell: () => <button aria-label="Notifiche">Bell</button>,
}));

// Mock Sheet to avoid Radix portal issues in jsdom
vi.mock('@/components/ui/navigation/sheet', () => ({
  Sheet: ({ open, children }: { open: boolean; children: React.ReactNode }) =>
    open ? <div data-testid="mobile-drawer">{children}</div> : null,
  SheetContent: ({ children }: { children: React.ReactNode }) => (
    <div data-testid="sheet-content">{children}</div>
  ),
  SheetHeader: ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
  SheetTitle: ({ children, asChild }: { children: React.ReactNode; asChild?: boolean }) =>
    asChild ? <>{children}</> : <div>{children}</div>,
}));

// Mock DropdownMenu to avoid Radix portal issues
vi.mock('@/components/ui/navigation/dropdown-menu', () => ({
  DropdownMenu: ({ children }: { children: React.ReactNode }) => (
    <div data-testid="dropdown-menu">{children}</div>
  ),
  DropdownMenuTrigger: ({ children, asChild }: { children: React.ReactNode; asChild?: boolean }) =>
    asChild ? <>{children}</> : <button>{children}</button>,
  DropdownMenuContent: ({ children }: { children: React.ReactNode }) => (
    <div data-testid="dropdown-content">{children}</div>
  ),
  DropdownMenuLabel: ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
  DropdownMenuSeparator: () => <hr />,
  DropdownMenuItem: ({
    children,
    onClick,
    disabled,
    asChild,
  }: {
    children: React.ReactNode;
    onClick?: () => void;
    disabled?: boolean;
    asChild?: boolean;
  }) =>
    asChild ? (
      <>{children}</>
    ) : (
      <button onClick={onClick} disabled={disabled}>
        {children}
      </button>
    ),
}));

vi.mock('@/components/ui/navigation/ThemeToggle', () => ({
  ThemeToggle: () => <div data-testid="theme-toggle">Theme</div>,
}));

vi.mock('@/components/ui/collapsible', () => ({
  Collapsible: ({
    open,
    children,
  }: {
    open: boolean;
    onOpenChange: (v: boolean) => void;
    children: React.ReactNode;
  }) => <div data-open={open}>{children}</div>,
  CollapsibleTrigger: ({ children }: { children: React.ReactNode }) => (
    <button>{children}</button>
  ),
  CollapsibleContent: ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
}));

vi.mock('../Logo', () => ({
  Logo: () => <div data-testid="logo">MeepleAI</div>,
}));

// ─── Helpers ──────────────────────────────────────────────────────────────────

const regularUser = {
  id: '1',
  displayName: 'Test User',
  email: 'test@test.com',
  role: 'user',
};

const adminUser = {
  id: '2',
  displayName: 'Admin User',
  email: 'admin@test.com',
  role: 'admin',
};

// ─── Navbar Tests ─────────────────────────────────────────────────────────────

describe('Navbar', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockCurrentUser.mockReturnValue({ data: regularUser, isLoading: false });
  });

  it('renders with role="navigation" and aria-label', () => {
    render(<Navbar />);
    const nav = screen.getByRole('navigation', { name: /main navigation/i });
    expect(nav).toBeInTheDocument();
  });

  it('renders logo', () => {
    render(<Navbar />);
    expect(screen.getByTestId('logo')).toBeInTheDocument();
  });

  it('renders notification bell', () => {
    render(<Navbar />);
    expect(screen.getByLabelText('Notifiche')).toBeInTheDocument();
  });

  it('renders hamburger button', () => {
    render(<Navbar />);
    expect(screen.getByTestId('hamburger-button')).toBeInTheDocument();
  });

  it('hamburger button has aria-expanded=false initially', () => {
    render(<Navbar />);
    const btn = screen.getByTestId('hamburger-button');
    expect(btn).toHaveAttribute('aria-expanded', 'false');
  });

  it('opens mobile drawer when hamburger is clicked', () => {
    render(<Navbar />);
    const btn = screen.getByTestId('hamburger-button');
    fireEvent.click(btn);
    expect(screen.getByTestId('mobile-drawer')).toBeInTheDocument();
  });

  it('shows Tool and Discover sections on desktop', () => {
    render(<Navbar />);
    // NavbarDropdown renders trigger buttons
    expect(screen.getByRole('button', { name: /tool/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /scopri/i })).toBeInTheDocument();
  });

  it('does NOT show Admin section for regular users', () => {
    render(<Navbar />);
    expect(screen.queryByRole('button', { name: /admin/i })).not.toBeInTheDocument();
  });

  it('shows Admin section for admin users', () => {
    mockCurrentUser.mockReturnValue({ data: adminUser, isLoading: false });
    render(<Navbar />);
    expect(screen.getByRole('button', { name: /admin/i })).toBeInTheDocument();
  });

  it('shows Admin section for superadmin users', () => {
    mockCurrentUser.mockReturnValue({
      data: { ...adminUser, role: 'superadmin' },
      isLoading: false,
    });
    render(<Navbar />);
    expect(screen.getByRole('button', { name: /admin/i })).toBeInTheDocument();
  });

  it('shows Admin section for editor users', () => {
    mockCurrentUser.mockReturnValue({
      data: { ...adminUser, role: 'editor' },
      isLoading: false,
    });
    render(<Navbar />);
    expect(screen.getByRole('button', { name: /admin/i })).toBeInTheDocument();
  });
});

// ─── NavbarDropdown Tests ─────────────────────────────────────────────────────

describe('NavbarDropdown', () => {
  const items = [
    { id: 'home', label: 'Home', href: '/home', icon: Home },
    { id: 'settings', label: 'Settings', href: '/settings', icon: Settings },
  ];

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders trigger button with label', () => {
    render(<NavbarDropdown label="Tool" items={items} />);
    expect(screen.getByRole('button', { name: /tool/i })).toBeInTheDocument();
  });

  it('dropdown is closed by default', () => {
    render(<NavbarDropdown label="Tool" items={items} />);
    expect(screen.queryByRole('menu')).not.toBeInTheDocument();
  });

  it('opens dropdown on trigger click', () => {
    render(<NavbarDropdown label="Tool" items={items} />);
    fireEvent.click(screen.getByRole('button', { name: /tool/i }));
    expect(screen.getByRole('menu')).toBeInTheDocument();
  });

  it('shows items when open', () => {
    render(<NavbarDropdown label="Tool" items={items} />);
    fireEvent.click(screen.getByRole('button', { name: /tool/i }));
    expect(screen.getByText('Home')).toBeInTheDocument();
    expect(screen.getByText('Settings')).toBeInTheDocument();
  });

  it('closes dropdown after item click', () => {
    render(<NavbarDropdown label="Tool" items={items} />);
    fireEvent.click(screen.getByRole('button', { name: /tool/i }));
    const homeLink = screen.getByRole('menuitem', { name: /home/i });
    fireEvent.click(homeLink);
    expect(screen.queryByRole('menu')).not.toBeInTheDocument();
  });

  it('trigger has aria-haspopup="menu"', () => {
    render(<NavbarDropdown label="Tool" items={items} />);
    expect(screen.getByRole('button', { name: /tool/i })).toHaveAttribute('aria-haspopup', 'menu');
  });

  it('trigger has aria-expanded=false when closed', () => {
    render(<NavbarDropdown label="Tool" items={items} />);
    expect(screen.getByRole('button', { name: /tool/i })).toHaveAttribute('aria-expanded', 'false');
  });

  it('trigger has aria-expanded=true when open', () => {
    render(<NavbarDropdown label="Tool" items={items} />);
    fireEvent.click(screen.getByRole('button', { name: /tool/i }));
    expect(screen.getByRole('button', { name: /tool/i })).toHaveAttribute('aria-expanded', 'true');
  });

  it('closes on Escape key', () => {
    render(<NavbarDropdown label="Tool" items={items} />);
    fireEvent.click(screen.getByRole('button', { name: /tool/i }));
    const menu = screen.getByRole('menu');
    fireEvent.keyDown(menu, { key: 'Escape' });
    expect(screen.queryByRole('menu')).not.toBeInTheDocument();
  });

  it('items have role="menuitem"', () => {
    render(<NavbarDropdown label="Tool" items={items} />);
    fireEvent.click(screen.getByRole('button', { name: /tool/i }));
    const menuitems = screen.getAllByRole('menuitem');
    expect(menuitems).toHaveLength(2);
  });
});

// ─── NavbarUserMenu Tests ─────────────────────────────────────────────────────

describe('NavbarUserMenu', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('shows loading skeleton when isLoading=true', () => {
    mockCurrentUser.mockReturnValue({ data: null, isLoading: true });
    render(<NavbarUserMenu />);
    const skeleton = document.querySelector('.animate-pulse');
    expect(skeleton).toBeInTheDocument();
  });

  it('shows login/register buttons for unauthenticated users', () => {
    mockCurrentUser.mockReturnValue({ data: null, isLoading: false });
    render(<NavbarUserMenu />);
    expect(screen.getByRole('link', { name: /accedi/i })).toBeInTheDocument();
  });

  it('shows user menu trigger for authenticated users', () => {
    mockCurrentUser.mockReturnValue({ data: regularUser, isLoading: false });
    render(<NavbarUserMenu />);
    expect(screen.getByTestId('user-menu-trigger')).toBeInTheDocument();
  });

  it('shows user initial in avatar', () => {
    mockCurrentUser.mockReturnValue({ data: regularUser, isLoading: false });
    render(<NavbarUserMenu />);
    expect(screen.getByText('T')).toBeInTheDocument(); // first char of "Test User"
  });

  it('renders dropdown content for authenticated user', () => {
    mockCurrentUser.mockReturnValue({ data: regularUser, isLoading: false });
    render(<NavbarUserMenu />);
    // dropdown-content is always rendered (mocked as always visible)
    expect(screen.getByTestId('dropdown-content')).toBeInTheDocument();
  });

  it('shows admin panel link for admin users', () => {
    mockCurrentUser.mockReturnValue({ data: adminUser, isLoading: false });
    render(<NavbarUserMenu />);
    // asChild mock renders the Link directly — check by link href instead of data-testid
    expect(screen.getByRole('link', { name: /admin panel/i })).toBeInTheDocument();
  });

  it('does NOT show admin panel link for regular users', () => {
    mockCurrentUser.mockReturnValue({ data: regularUser, isLoading: false });
    render(<NavbarUserMenu />);
    expect(screen.queryByRole('link', { name: /admin panel/i })).not.toBeInTheDocument();
  });

  it('shows logout button for authenticated users', () => {
    mockCurrentUser.mockReturnValue({ data: regularUser, isLoading: false });
    render(<NavbarUserMenu />);
    // DropdownMenuItem mock renders as <button> without forwarding data-testid — check by name
    expect(screen.getByRole('button', { name: /esci/i })).toBeInTheDocument();
  });
});

// ─── NavbarMobileDrawer Tests ─────────────────────────────────────────────────

describe('NavbarMobileDrawer', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockCurrentUser.mockReturnValue({ data: regularUser, isLoading: false });
  });

  it('does not render when closed', () => {
    render(<NavbarMobileDrawer open={false} onClose={() => {}} />);
    expect(screen.queryByTestId('mobile-drawer')).not.toBeInTheDocument();
  });

  it('renders drawer when open=true', () => {
    render(<NavbarMobileDrawer open={true} onClose={() => {}} />);
    expect(screen.getByTestId('mobile-drawer')).toBeInTheDocument();
  });

  it('shows Tool section when open', () => {
    render(<NavbarMobileDrawer open={true} onClose={() => {}} />);
    expect(screen.getByText('Tool')).toBeInTheDocument();
  });

  it('shows Scopri section when open', () => {
    render(<NavbarMobileDrawer open={true} onClose={() => {}} />);
    expect(screen.getByText('Scopri')).toBeInTheDocument();
  });

  it('hides Admin section for regular users', () => {
    render(<NavbarMobileDrawer open={true} onClose={() => {}} />);
    expect(screen.queryByText('Admin')).not.toBeInTheDocument();
  });

  it('shows Admin section for admin users', () => {
    mockCurrentUser.mockReturnValue({ data: adminUser, isLoading: false });
    render(<NavbarMobileDrawer open={true} onClose={() => {}} />);
    expect(screen.getByText('Admin')).toBeInTheDocument();
  });

  it('shows nav link items', () => {
    render(<NavbarMobileDrawer open={true} onClose={() => {}} />);
    expect(screen.getByText('Libreria')).toBeInTheDocument();
    expect(screen.getByText('Catalogo')).toBeInTheDocument();
  });

  it('calls onClose when a nav link is clicked', () => {
    const onClose = vi.fn();
    render(<NavbarMobileDrawer open={true} onClose={onClose} />);
    const libraryLink = screen.getByText('Libreria').closest('a');
    expect(libraryLink).not.toBeNull();
    fireEvent.click(libraryLink!);
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('shows logout button for authenticated user', () => {
    render(<NavbarMobileDrawer open={true} onClose={() => {}} />);
    expect(screen.getByTestId('mobile-drawer-logout')).toBeInTheDocument();
  });

  it('shows login/register buttons for unauthenticated user', () => {
    mockCurrentUser.mockReturnValue({ data: null, isLoading: false });
    render(<NavbarMobileDrawer open={true} onClose={() => {}} />);
    expect(screen.getByRole('link', { name: /accedi/i })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /registrati/i })).toBeInTheDocument();
  });
});
