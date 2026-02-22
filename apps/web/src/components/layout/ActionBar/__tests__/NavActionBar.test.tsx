/**
 * NavActionBar Tests
 * Issue #5038 — ActionBar Component
 *
 * Tests: NavActionBar (context-driven toolbar), ActionBarButton, ActionBarOverflow
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { Plus, Download, Filter, Settings, Search, Share, Trash2, Edit } from 'lucide-react';

import { NavActionBar } from '../NavActionBar';
import { ActionBarButton } from '../ActionBarButton';
import { ActionBarOverflow } from '../ActionBarOverflow';
import type { NavAction } from '@/types/navigation';

// ─── Mocks ────────────────────────────────────────────────────────────────────

const mockActions = vi.fn<[], NavAction[]>(() => []);

vi.mock('@/context/NavigationContext', () => ({
  useNavigation: () => ({
    miniNavTabs: [],
    actionBarActions: mockActions(),
    activeZone: null,
    setNavConfig: vi.fn(),
    clearNavConfig: vi.fn(),
  }),
}));

// Mock Tooltip to avoid Radix portal issues
vi.mock('@/components/ui/overlays/tooltip', () => ({
  TooltipProvider: ({ children }: { children: React.ReactNode }) => <>{children}</>,
  Tooltip: ({ children }: { children: React.ReactNode }) => <>{children}</>,
  TooltipTrigger: ({ children, asChild }: { children: React.ReactNode; asChild?: boolean }) =>
    asChild ? <>{children}</> : <div>{children}</div>,
  TooltipContent: ({ children }: { children: React.ReactNode }) => (
    <div data-testid="tooltip-content">{children}</div>
  ),
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
  DropdownMenuItem: ({
    children,
    onClick,
    disabled,
  }: {
    children: React.ReactNode;
    onClick?: () => void;
    disabled?: boolean;
  }) => (
    <button onClick={onClick} disabled={disabled}>
      {children}
    </button>
  ),
}));

// ─── Fixtures ─────────────────────────────────────────────────────────────────

const primaryAction: NavAction = {
  id: 'add-game',
  label: 'Add Game',
  icon: Plus,
  onClick: vi.fn(),
  variant: 'primary',
};

const secondaryAction: NavAction = {
  id: 'import',
  label: 'Import BGG',
  icon: Download,
  onClick: vi.fn(),
  variant: 'secondary',
};

const ghostAction: NavAction = {
  id: 'filter',
  label: 'Filter',
  icon: Filter,
  onClick: vi.fn(),
  variant: 'ghost',
};

const disabledAction: NavAction = {
  id: 'delete',
  label: 'Delete',
  icon: Trash2,
  onClick: vi.fn(),
  variant: 'ghost',
  disabled: true,
  disabledTooltip: 'Select items first',
};

const hiddenAction: NavAction = {
  id: 'hidden-action',
  label: 'Hidden',
  icon: Settings,
  onClick: vi.fn(),
  hidden: true,
};

const badgeAction: NavAction = {
  id: 'approve',
  label: 'Approve',
  icon: Filter,
  onClick: vi.fn(),
  badge: 3,
};

const manyActions: NavAction[] = [
  { id: 'a1', label: 'Action 1', icon: Plus, onClick: vi.fn() },
  { id: 'a2', label: 'Action 2', icon: Download, onClick: vi.fn() },
  { id: 'a3', label: 'Action 3', icon: Filter, onClick: vi.fn() },
  { id: 'a4', label: 'Action 4', icon: Search, onClick: vi.fn() },
  { id: 'a5', label: 'Action 5', icon: Share, onClick: vi.fn() },
  { id: 'a6', label: 'Action 6', icon: Edit, onClick: vi.fn() },
  { id: 'a7', label: 'Action 7', icon: Settings, onClick: vi.fn() }, // overflow on desktop
];

// ─── NavActionBar Tests ───────────────────────────────────────────────────────

describe('NavActionBar', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockActions.mockReturnValue([]);
  });

  // ── Visibility ──────────────────────────────────────────────────────────────

  it('renders nothing when there are no actions', () => {
    mockActions.mockReturnValue([]);
    const { container } = render(<NavActionBar />);
    expect(container.firstChild).toBeNull();
  });

  it('renders when actions are provided', () => {
    mockActions.mockReturnValue([primaryAction]);
    render(<NavActionBar />);
    expect(screen.getByTestId('nav-action-bar-desktop')).toBeInTheDocument();
    expect(screen.getByTestId('nav-action-bar-mobile')).toBeInTheDocument();
  });

  it('filters out hidden actions', () => {
    mockActions.mockReturnValue([primaryAction, hiddenAction]);
    render(<NavActionBar />);
    expect(screen.queryByTestId('action-bar-btn-hidden-action')).not.toBeInTheDocument();
  });

  it('renders nothing when all actions are hidden', () => {
    mockActions.mockReturnValue([hiddenAction]);
    const { container } = render(<NavActionBar />);
    expect(container.firstChild).toBeNull();
  });

  // ── ARIA ────────────────────────────────────────────────────────────────────

  it('desktop bar has role="toolbar"', () => {
    mockActions.mockReturnValue([primaryAction]);
    render(<NavActionBar />);
    const bars = screen.getAllByRole('toolbar');
    expect(bars.length).toBeGreaterThanOrEqual(1);
  });

  it('toolbar has aria-label="Page actions"', () => {
    mockActions.mockReturnValue([primaryAction]);
    render(<NavActionBar />);
    const bars = screen.getAllByRole('toolbar', { name: /page actions/i });
    expect(bars.length).toBeGreaterThanOrEqual(1);
  });

  // ── Action rendering ────────────────────────────────────────────────────────

  it('renders action labels', () => {
    mockActions.mockReturnValue([primaryAction, secondaryAction]);
    render(<NavActionBar />);
    expect(screen.getAllByText('Add Game').length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText('Import BGG').length).toBeGreaterThanOrEqual(1);
  });

  // ── Overflow (desktop > 6, mobile > 4) ─────────────────────────────────────

  it('shows overflow button when actions > 6 (desktop limit)', () => {
    mockActions.mockReturnValue(manyActions); // 7 actions
    render(<NavActionBar />);
    // Both desktop and mobile bars render an overflow button
    const overflowBtns = screen.getAllByTestId('action-bar-overflow');
    expect(overflowBtns.length).toBeGreaterThanOrEqual(1);
  });

  it('overflow button has aria-label', () => {
    mockActions.mockReturnValue(manyActions);
    render(<NavActionBar />);
    const overflow = screen.getAllByTestId('action-bar-overflow')[0];
    expect(overflow).toHaveAttribute('aria-label', 'Altre azioni');
  });

  // ── Custom className ────────────────────────────────────────────────────────

  it('applies custom className to desktop bar', () => {
    mockActions.mockReturnValue([primaryAction]);
    render(<NavActionBar className="test-class" />);
    const desktopBar = screen.getByTestId('nav-action-bar-desktop');
    expect(desktopBar).toHaveClass('test-class');
  });
});

// ─── ActionBarButton Tests ────────────────────────────────────────────────────

describe('ActionBarButton', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders button with label', () => {
    render(<ActionBarButton action={primaryAction} />);
    expect(screen.getByRole('button', { name: /add game/i })).toBeInTheDocument();
  });

  it('renders icon', () => {
    render(<ActionBarButton action={primaryAction} />);
    const btn = screen.getByRole('button', { name: /add game/i });
    expect(btn.querySelector('svg')).not.toBeNull();
  });

  it('has correct data-testid', () => {
    render(<ActionBarButton action={primaryAction} />);
    expect(screen.getByTestId('action-bar-btn-add-game')).toBeInTheDocument();
  });

  it('calls onClick when clicked', () => {
    const handler = vi.fn();
    render(<ActionBarButton action={{ ...primaryAction, onClick: handler }} />);
    fireEvent.click(screen.getByRole('button', { name: /add game/i }));
    expect(handler).toHaveBeenCalledTimes(1);
  });

  it('does not call onClick when disabled', () => {
    const handler = vi.fn();
    render(<ActionBarButton action={{ ...disabledAction, onClick: handler }} />);
    fireEvent.click(screen.getByRole('button', { name: /delete/i }));
    expect(handler).not.toHaveBeenCalled();
  });

  it('has aria-disabled when disabled', () => {
    render(<ActionBarButton action={disabledAction} />);
    expect(screen.getByRole('button', { name: /delete/i })).toHaveAttribute('aria-disabled', 'true');
  });

  it('shows badge when action has badge', () => {
    render(<ActionBarButton action={badgeAction} />);
    expect(screen.getByText('3')).toBeInTheDocument();
  });

  it('renders primary variant styling classes', () => {
    render(<ActionBarButton action={primaryAction} />);
    const btn = screen.getByTestId('action-bar-btn-add-game');
    expect(btn).toHaveClass('bg-primary');
  });

  it('renders ghost variant', () => {
    render(<ActionBarButton action={ghostAction} />);
    const btn = screen.getByTestId('action-bar-btn-filter');
    // ghost doesn't have bg-primary or bg-secondary
    expect(btn).not.toHaveClass('bg-primary');
    expect(btn).not.toHaveClass('bg-secondary');
  });

  it('compact prop applies smaller text', () => {
    render(<ActionBarButton action={primaryAction} compact />);
    const btn = screen.getByTestId('action-bar-btn-add-game');
    expect(btn).toHaveClass('text-xs');
  });

  it('shows tooltip content for disabled with disabledTooltip', () => {
    render(<ActionBarButton action={disabledAction} />);
    expect(screen.getByTestId('tooltip-content')).toBeInTheDocument();
    expect(screen.getByText('Select items first')).toBeInTheDocument();
  });
});

// ─── ActionBarOverflow Tests ──────────────────────────────────────────────────

describe('ActionBarOverflow', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders nothing when no actions', () => {
    const { container } = render(<ActionBarOverflow actions={[]} />);
    expect(container.firstChild).toBeNull();
  });

  it('renders trigger button', () => {
    render(<ActionBarOverflow actions={[secondaryAction]} />);
    expect(screen.getByTestId('action-bar-overflow')).toBeInTheDocument();
  });

  it('trigger has aria-label', () => {
    render(<ActionBarOverflow actions={[secondaryAction]} />);
    expect(screen.getByTestId('action-bar-overflow')).toHaveAttribute('aria-label', 'Altre azioni');
  });

  it('trigger has aria-haspopup', () => {
    render(<ActionBarOverflow actions={[secondaryAction]} />);
    expect(screen.getByTestId('action-bar-overflow')).toHaveAttribute('aria-haspopup', 'menu');
  });

  it('renders overflow items in dropdown content', () => {
    render(<ActionBarOverflow actions={[secondaryAction, ghostAction]} />);
    const content = screen.getByTestId('dropdown-content');
    expect(content).toBeInTheDocument();
    expect(content).toHaveTextContent('Import BGG');
    expect(content).toHaveTextContent('Filter');
  });

  it('renders badge in overflow item', () => {
    render(<ActionBarOverflow actions={[badgeAction]} />);
    expect(screen.getByText('3')).toBeInTheDocument();
  });

  it('overflow items are rendered in dropdown content', () => {
    render(<ActionBarOverflow actions={[secondaryAction]} />);
    // DropdownMenuItem mock renders as <button> without forwarding data-testid — check by text
    const content = screen.getByTestId('dropdown-content');
    expect(content).toHaveTextContent('Import BGG');
  });
});
