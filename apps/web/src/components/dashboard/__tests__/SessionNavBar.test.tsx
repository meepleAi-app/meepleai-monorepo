/**
 * @vitest-environment jsdom
 */
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { SessionNavBar } from '../session-nav/SessionNavBar';
import type { SheetContext } from '../DashboardEngine';

// ---------------------------------------------------------------------------
// Fixtures
// ---------------------------------------------------------------------------

const STARTED_AT = new Date('2026-01-01T10:00:00.000Z');

function renderNavBar(overrides?: Partial<React.ComponentProps<typeof SessionNavBar>>) {
  const onExit = vi.fn();
  const onOpenSheet = vi.fn();

  const props = {
    gameName: 'Catan',
    sessionStartedAt: STARTED_AT,
    isPaused: false,
    activeSheet: null as SheetContext | null,
    onOpenSheet,
    onExit,
    ...overrides,
  };

  const result = render(<SessionNavBar {...props} />);
  return { ...result, onExit, onOpenSheet };
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe('SessionNavBar', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('renders the game name', () => {
    renderNavBar({ gameName: 'Terraforming Mars' });
    expect(screen.getByTestId('session-game-name')).toHaveTextContent('Terraforming Mars');
  });

  it('renders the exit button', () => {
    renderNavBar();
    expect(screen.getByTestId('session-exit-button')).toBeInTheDocument();
    expect(screen.getByTestId('session-exit-button')).toHaveTextContent('← Esci');
  });

  it('calls onExit when the exit button is clicked', () => {
    const { onExit } = renderNavBar();
    fireEvent.click(screen.getByTestId('session-exit-button'));
    expect(onExit).toHaveBeenCalledTimes(1);
  });

  it('renders 5 sub-context icons', () => {
    renderNavBar();
    const icons = screen.getByTestId('session-sub-context-icons');
    // Each icon has a data-testid of sub-context-icon-{context}
    const allContexts: SheetContext[] = ['scores', 'rules-ai', 'timer', 'photos', 'players'];
    for (const ctx of allContexts) {
      expect(screen.getByTestId(`sub-context-icon-${ctx}`)).toBeInTheDocument();
    }
    expect(icons.children).toHaveLength(5);
  });

  it('calls onOpenSheet with the correct context when an icon is clicked', () => {
    const { onOpenSheet } = renderNavBar();
    fireEvent.click(screen.getByTestId('sub-context-icon-scores'));
    expect(onOpenSheet).toHaveBeenCalledWith('scores');
  });

  it('calls onOpenSheet with rules-ai context when that icon is clicked', () => {
    const { onOpenSheet } = renderNavBar();
    fireEvent.click(screen.getByTestId('sub-context-icon-rules-ai'));
    expect(onOpenSheet).toHaveBeenCalledWith('rules-ai');
  });

  it('highlights the active sheet icon via aria-pressed', () => {
    renderNavBar({ activeSheet: 'timer' });
    const timerIcon = screen.getByTestId('sub-context-icon-timer');
    expect(timerIcon).toHaveAttribute('aria-pressed', 'true');
  });

  it('does not highlight non-active sheet icons', () => {
    renderNavBar({ activeSheet: 'timer' });
    const scoresIcon = screen.getByTestId('sub-context-icon-scores');
    expect(scoresIcon).toHaveAttribute('aria-pressed', 'false');
  });

  it('renders the live timer', () => {
    renderNavBar();
    expect(screen.getByTestId('live-timer')).toBeInTheDocument();
  });
});
