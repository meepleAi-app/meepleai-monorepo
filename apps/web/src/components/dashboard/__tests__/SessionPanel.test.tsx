/**
 * @vitest-environment jsdom
 */
import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { SessionPanel } from '../SessionPanel';
import { SessionPanelCollapsed } from '../SessionPanelCollapsed';

import type { SessionSlotData } from '../useSessionSlot';

// ---------------------------------------------------------------------------
// Mocks
// ---------------------------------------------------------------------------

const mockSessionSlot = vi.fn<() => SessionSlotData>();

vi.mock('../useSessionSlot', () => ({
  useSessionSlot: () => mockSessionSlot(),
}));

vi.mock('@/lib/stores/sessionStore', () => ({
  useSessionStore: (selector: (s: Record<string, unknown>) => unknown) =>
    selector({
      pauseSession: vi.fn(),
      resumeSession: vi.fn(),
    }),
}));

vi.mock('@/lib/stores/cascadeNavigationStore', () => ({
  useCascadeNavigationStore: (selector: (s: Record<string, unknown>) => unknown) =>
    selector({
      openDeckStack: vi.fn(),
    }),
}));

// Mock next/link as a simple <a>
vi.mock('next/link', () => ({
  __esModule: true,
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

// ---------------------------------------------------------------------------
// Fixtures
// ---------------------------------------------------------------------------

function visibleSlot(overrides?: Partial<SessionSlotData>): SessionSlotData {
  return {
    isVisible: true,
    gameName: 'Catan',
    gameImageUrl: '/catan.png',
    sessionId: 'sess-1',
    duration: 42,
    players: [
      { name: 'Alice', score: 10 },
      { name: 'Bob', score: 8 },
      { name: 'Charlie', score: 6 },
    ],
    sessionStatus: 'inProgress',
    gameId: 'game-1',
    ...overrides,
  };
}

function hiddenSlot(): SessionSlotData {
  return {
    isVisible: false,
    gameName: '',
    sessionId: '',
    duration: 0,
    players: [],
    sessionStatus: 'inProgress',
    gameId: '',
  };
}

// ---------------------------------------------------------------------------
// Tests — SessionPanel
// ---------------------------------------------------------------------------

describe('SessionPanel', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders session-panel data-testid', () => {
    mockSessionSlot.mockReturnValue(visibleSlot());
    render(<SessionPanel />);
    expect(screen.getByTestId('session-panel')).toBeInTheDocument();
  });

  it('shows game name', () => {
    mockSessionSlot.mockReturnValue(visibleSlot({ gameName: 'Terraforming Mars' }));
    render(<SessionPanel />);
    expect(screen.getByText('Terraforming Mars')).toBeInTheDocument();
  });

  it('shows live dot', () => {
    mockSessionSlot.mockReturnValue(visibleSlot());
    render(<SessionPanel />);
    expect(screen.getByLabelText('Live session')).toBeInTheDocument();
  });

  it('renders 3 mana pips', () => {
    mockSessionSlot.mockReturnValue(visibleSlot());
    render(<SessionPanel />);
    expect(screen.getByTestId('mana-pip-kb')).toBeInTheDocument();
    expect(screen.getByTestId('mana-pip-agent')).toBeInTheDocument();
    expect(screen.getByTestId('mana-pip-player')).toBeInTheDocument();
  });

  it('shows pause button when session is in progress', () => {
    mockSessionSlot.mockReturnValue(visibleSlot({ sessionStatus: 'inProgress' }));
    render(<SessionPanel />);
    const btn = screen.getByTestId('session-pause-resume');
    expect(btn).toHaveTextContent('Pause');
  });

  it('shows resume button when session is paused', () => {
    mockSessionSlot.mockReturnValue(visibleSlot({ sessionStatus: 'paused' }));
    render(<SessionPanel />);
    const btn = screen.getByTestId('session-pause-resume');
    expect(btn).toHaveTextContent('Resume');
  });

  it('shows "Vai allo scoreboard" link', () => {
    mockSessionSlot.mockReturnValue(visibleSlot());
    render(<SessionPanel />);
    const link = screen.getByTestId('session-scoreboard-link');
    expect(link).toHaveTextContent('Vai allo scoreboard');
    expect(link).toHaveAttribute('href', '/sessions/sess-1');
  });

  it('returns null when not in game mode', () => {
    mockSessionSlot.mockReturnValue(hiddenSlot());
    const { container } = render(<SessionPanel />);
    expect(container.innerHTML).toBe('');
  });

  it('shows session duration', () => {
    mockSessionSlot.mockReturnValue(visibleSlot({ duration: 42 }));
    render(<SessionPanel />);
    expect(screen.getByText('42min')).toBeInTheDocument();
  });

  it('renders mini scoreboard with top 3 players', () => {
    mockSessionSlot.mockReturnValue(visibleSlot());
    render(<SessionPanel />);
    expect(screen.getByTestId('mini-scoreboard')).toBeInTheDocument();
    expect(screen.getByText('Alice')).toBeInTheDocument();
    expect(screen.getByText('Bob')).toBeInTheDocument();
    expect(screen.getByText('Charlie')).toBeInTheDocument();
  });
});

// ---------------------------------------------------------------------------
// Tests — SessionPanelCollapsed
// ---------------------------------------------------------------------------

describe('SessionPanelCollapsed', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders with data-testid when visible', () => {
    mockSessionSlot.mockReturnValue(visibleSlot());
    render(<SessionPanelCollapsed />);
    expect(screen.getByTestId('session-panel-collapsed')).toBeInTheDocument();
  });

  it('returns null when not in game mode', () => {
    mockSessionSlot.mockReturnValue(hiddenSlot());
    const { container } = render(<SessionPanelCollapsed />);
    expect(container.innerHTML).toBe('');
  });

  it('shows the hourglass icon', () => {
    mockSessionSlot.mockReturnValue(visibleSlot());
    render(<SessionPanelCollapsed />);
    expect(screen.getByText('⏳')).toBeInTheDocument();
  });

  it('shows the live dot overlay', () => {
    mockSessionSlot.mockReturnValue(visibleSlot());
    render(<SessionPanelCollapsed />);
    expect(screen.getByLabelText('Live session')).toBeInTheDocument();
  });
});
