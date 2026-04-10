/**
 * ContextualHandSidebar — Smoke Tests
 *
 * Validates rendering in idle, loading, and active states.
 * Uses a Zustand store mock pattern consistent with the codebase.
 */

import { describe, it, expect, vi, beforeEach, type Mock } from 'vitest';
import { render, screen } from '@testing-library/react';

import type { ContextualHandStore } from '@/stores/contextual-hand/types';

// ─── Mocks ────────────────────────────────────────────────────────────────

// Mock the store module: we export useContextualHandStore as a vi.fn()
// that will be configured per-test to return the desired slice of state.
const { mockUseContextualHandStore } = vi.hoisted(() => ({
  mockUseContextualHandStore: Object.assign(vi.fn(), {
    persist: { rehydrate: vi.fn() },
  }),
}));

vi.mock('@/stores/contextual-hand', () => ({
  useContextualHandStore: mockUseContextualHandStore,
  selectContext: (s: ContextualHandStore) => s.context,
  selectIsLoading: (s: ContextualHandStore) => s.isLoading,
}));

// Mock child components to isolate sidebar logic
vi.mock('../ContextualHandSlot', () => ({
  ContextualHandSlot: ({ slotType }: { slotType: string }) => (
    <div data-testid={`slot-${slotType}`} />
  ),
}));

vi.mock('@/components/session/GamePickerDialog', () => ({
  GamePickerDialog: () => <div data-testid="game-picker-dialog" />,
}));

// Import after mocks
import { useContextualHandStore } from '@/stores/contextual-hand';
import { ContextualHandSidebar } from '../ContextualHandSidebar';

// ─── Helpers ──────────────────────────────────────────────────────────────

const mockStore = mockUseContextualHandStore as unknown as Mock;

/**
 * Configure the mock store so that each `useContextualHandStore(selector)`
 * call returns the selector applied to the given state.
 */
function setMockState(state: Partial<ContextualHandStore>) {
  const fullState = {
    context: 'idle' as const,
    currentSession: null,
    createResult: null,
    isLoading: false,
    isInitialized: false,
    error: null,
    diaryEntries: [],
    isDiaryLoading: false,
    kbReadiness: null,
    initialize: vi.fn(),
    startSession: vi.fn(),
    pauseSession: vi.fn(),
    resumeSession: vi.fn(),
    setTurnOrder: vi.fn(),
    advanceTurn: vi.fn(),
    rollDice: vi.fn(),
    upsertScore: vi.fn(),
    loadDiary: vi.fn(),
    checkKbReadiness: vi.fn(),
    reset: vi.fn(),
    ...state,
  };

  mockStore.mockImplementation((selector: (s: ContextualHandStore) => unknown) => {
    if (typeof selector === 'function') {
      return selector(fullState as ContextualHandStore);
    }
    return fullState;
  });
}

// ─── Tests ────────────────────────────────────────────────────────────────

describe('ContextualHandSidebar', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders the sidebar landmark with correct aria-label', () => {
    setMockState({ context: 'idle' });

    render(<ContextualHandSidebar />);

    const sidebar = screen.getByTestId('contextual-hand-sidebar');
    expect(sidebar).toBeInTheDocument();
    expect(sidebar).toHaveAttribute('aria-label', 'La mia mano');
  });

  it('shows loading spinner when isLoading is true', () => {
    setMockState({ isLoading: true });

    render(<ContextualHandSidebar />);

    // The spinner is an animated div with specific classes
    const spinner = document.querySelector('.animate-spin');
    expect(spinner).toBeInTheDocument();
  });

  it('shows idle message when context is idle and not loading', () => {
    setMockState({ context: 'idle', isLoading: false });

    render(<ContextualHandSidebar />);

    expect(screen.getByText('Nessuna partita attiva.')).toBeInTheDocument();
    expect(screen.getByText('Nuova partita')).toBeInTheDocument();
  });

  it('renders five slots when session is active', () => {
    setMockState({
      context: 'active',
      isLoading: false,
      currentSession: {
        sessionId: 's1',
        gameId: 'g1',
        status: 'Active',
        sessionCode: 'ABC',
        sessionDate: '',
        updatedAt: null,
        gameNightEventId: null,
      },
    });

    render(<ContextualHandSidebar />);

    expect(screen.getByTestId('slot-session')).toBeInTheDocument();
    expect(screen.getByTestId('slot-game')).toBeInTheDocument();
    expect(screen.getByTestId('slot-kb')).toBeInTheDocument();
    expect(screen.getByTestId('slot-agent')).toBeInTheDocument();
    expect(screen.getByTestId('slot-toolkit')).toBeInTheDocument();
  });

  it('renders five slots when session is paused', () => {
    setMockState({
      context: 'paused',
      isLoading: false,
      currentSession: {
        sessionId: 's1',
        gameId: 'g1',
        status: 'Paused',
        sessionCode: 'ABC',
        sessionDate: '',
        updatedAt: null,
        gameNightEventId: null,
      },
    });

    render(<ContextualHandSidebar />);

    expect(screen.getByTestId('slot-session')).toBeInTheDocument();
    expect(screen.getByTestId('slot-game')).toBeInTheDocument();
    expect(screen.getByTestId('slot-kb')).toBeInTheDocument();
  });

  it('calls initialize on mount', () => {
    const initMock = vi.fn();
    setMockState({ context: 'idle', initialize: initMock });

    render(<ContextualHandSidebar />);

    expect(initMock).toHaveBeenCalledTimes(1);
  });

  it('has a collapse/expand toggle button', () => {
    setMockState({ context: 'idle' });

    render(<ContextualHandSidebar />);

    const toggleBtn = screen.getByRole('button', { name: /comprimi pannello/i });
    expect(toggleBtn).toBeInTheDocument();
  });

  it('renders the game picker dialog', () => {
    setMockState({ context: 'idle' });

    render(<ContextualHandSidebar />);

    expect(screen.getByTestId('game-picker-dialog')).toBeInTheDocument();
  });
});
