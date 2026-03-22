/**
 * @vitest-environment jsdom
 */
import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';

import { TavoloView } from '../tavolo/TavoloView';

// ---------------------------------------------------------------------------
// Mock child components — keep tests focused on composition only
// ---------------------------------------------------------------------------

vi.mock('../tavolo/ScoreboardCompact', () => ({
  ScoreboardCompact: () => <div data-testid="scoreboard-compact" />,
}));

vi.mock('../tavolo/TurnIndicator', () => ({
  TurnIndicator: () => <div data-testid="turn-indicator" />,
}));

vi.mock('../tavolo/QuickActions', () => ({
  QuickActions: ({ onAddScore, onAskAi }: { onAddScore: () => void; onAskAi: () => void }) => (
    <div data-testid="quick-actions">
      <button type="button" onClick={onAddScore}>
        Aggiungi Punteggio
      </button>
      <button type="button" onClick={onAskAi}>
        🤖
      </button>
    </div>
  ),
}));

vi.mock('../tavolo/EventLog', () => ({
  EventLog: () => <div data-testid="event-log" />,
}));

// ---------------------------------------------------------------------------
// Mock useDashboardMode hook
// ---------------------------------------------------------------------------

vi.mock('../useDashboardMode', () => ({
  useDashboardMode: () => ({
    state: 'gameMode',
    isExploration: false,
    isGameMode: true,
    isTransitioning: false,
    isExpanded: false,
    activeSessionId: 'sess-1',
    transitionTarget: null,
    send: vi.fn(),
  }),
}));

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe('TavoloView', () => {
  it('renders the tavolo-view container', () => {
    render(<TavoloView sessionId="sess-1" />);
    expect(screen.getByTestId('tavolo-view')).toBeInTheDocument();
  });

  it('renders ScoreboardCompact section', () => {
    render(<TavoloView sessionId="sess-1" />);
    expect(screen.getByTestId('scoreboard-compact')).toBeInTheDocument();
  });

  it('renders TurnIndicator section', () => {
    render(<TavoloView sessionId="sess-1" />);
    expect(screen.getByTestId('turn-indicator')).toBeInTheDocument();
  });

  it('renders QuickActions section', () => {
    render(<TavoloView sessionId="sess-1" />);
    expect(screen.getByTestId('quick-actions')).toBeInTheDocument();
  });

  it('renders EventLog section', () => {
    render(<TavoloView sessionId="sess-1" />);
    expect(screen.getByTestId('event-log')).toBeInTheDocument();
  });

  it('renders all 4 sections together', () => {
    render(<TavoloView sessionId="sess-1" />);
    expect(screen.getByTestId('scoreboard-compact')).toBeInTheDocument();
    expect(screen.getByTestId('turn-indicator')).toBeInTheDocument();
    expect(screen.getByTestId('quick-actions')).toBeInTheDocument();
    expect(screen.getByTestId('event-log')).toBeInTheDocument();
  });
});
