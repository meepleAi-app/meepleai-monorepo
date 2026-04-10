/**
 * ContextualHandSlot — Unit Tests
 *
 * Tests the slot component in different modes: collapsed icon-only,
 * idle placeholder, and active card rendering for each slot type.
 */

import { describe, it, expect, vi, beforeEach, type Mock } from 'vitest';
import { render, screen } from '@testing-library/react';

import type { ContextualHandStore } from '@/stores/contextual-hand/types';

// ─── Mocks ────────────────────────────────────────────────────────────────

vi.mock('@/stores/contextual-hand', () => ({
  useContextualHandStore: vi.fn(),
  selectContext: (s: ContextualHandStore) => s.context,
  selectCurrentSession: (s: ContextualHandStore) => s.currentSession,
  selectCreateResult: (s: ContextualHandStore) => s.createResult,
}));

vi.mock('@/components/ui/data-display/meeple-card/MeepleCard', () => ({
  MeepleCard: ({ entity, title, badge, status }: Record<string, string>) => (
    <div data-testid={`meeple-card-${entity}`} data-title={title} data-badge={badge} data-status={status} />
  ),
}));

import { useContextualHandStore } from '@/stores/contextual-hand';
import { ContextualHandSlot } from '../ContextualHandSlot';

// ─── Helpers ──────────────────────────────────────────────────────────────

const mockStore = useContextualHandStore as unknown as Mock;

function setMockState(state: Partial<ContextualHandStore>) {
  const fullState = {
    context: 'idle' as const,
    currentSession: null,
    createResult: null,
    isLoading: false,
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

const activeSession = {
  sessionId: 's1',
  gameId: 'g1',
  status: 'Active',
  sessionCode: 'ABC123',
  sessionDate: '2026-04-10T12:00:00Z',
  updatedAt: null,
  gameNightEventId: null,
};

// ─── Tests ────────────────────────────────────────────────────────────────

describe('ContextualHandSlot', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  // ── Collapsed mode ──────────────────────────────────────────────────

  describe('collapsed mode', () => {
    it('renders icon-only pill in collapsed mode', () => {
      setMockState({ context: 'active', currentSession: activeSession });

      const { container } = render(<ContextualHandSlot slotType="session" collapsed />);

      // Should render a small container with title attribute
      const pill = container.querySelector('[title="Partita"]');
      expect(pill).toBeInTheDocument();
    });

    it('renders muted icon when idle', () => {
      setMockState({ context: 'idle' });

      const { container } = render(<ContextualHandSlot slotType="game" collapsed />);

      const pill = container.querySelector('[title="Gioco"]');
      expect(pill).toBeInTheDocument();
      // Should have muted styling (no primary bg)
      expect(pill?.className).toContain('muted-foreground');
    });
  });

  // ── Idle state ──────────────────────────────────────────────────────

  describe('idle state', () => {
    it('renders empty slot placeholder with label', () => {
      setMockState({ context: 'idle' });

      render(<ContextualHandSlot slotType="session" />);

      expect(screen.getByText('Partita')).toBeInTheDocument();
    });

    it('renders correct label for each slot type', () => {
      const labels: Record<string, string> = {
        game: 'Gioco',
        agent: 'Agente AI',
        toolkit: 'Toolkit',
        session: 'Partita',
      };

      for (const [type, label] of Object.entries(labels)) {
        setMockState({ context: 'idle' });
        const { unmount } = render(<ContextualHandSlot slotType={type as 'game' | 'agent' | 'toolkit' | 'session'} />);
        expect(screen.getByText(label)).toBeInTheDocument();
        unmount();
      }
    });
  });

  // ── Session slot (active) ───────────────────────────────────────────

  describe('session slot — active', () => {
    it('renders MeepleCard with session code and active badge', () => {
      setMockState({ context: 'active', currentSession: activeSession });

      render(<ContextualHandSlot slotType="session" />);

      const card = screen.getByTestId('meeple-card-session');
      expect(card).toHaveAttribute('data-title', '#ABC123');
      expect(card).toHaveAttribute('data-badge', 'Attiva');
      expect(card).toHaveAttribute('data-status', 'active');
    });

    it('shows Pausa button when active', () => {
      setMockState({ context: 'active', currentSession: activeSession });

      render(<ContextualHandSlot slotType="session" />);

      expect(screen.getByText('Pausa')).toBeInTheDocument();
    });
  });

  // ── Session slot (paused) ───────────────────────────────────────────

  describe('session slot — paused', () => {
    it('renders with paused badge and Riprendi button', () => {
      setMockState({
        context: 'paused',
        currentSession: { ...activeSession, status: 'Paused' },
      });

      render(<ContextualHandSlot slotType="session" />);

      const card = screen.getByTestId('meeple-card-session');
      expect(card).toHaveAttribute('data-badge', 'In pausa');
      expect(screen.getByText('Riprendi')).toBeInTheDocument();
    });
  });

  // ── Game slot ───────────────────────────────────────────────────────

  describe('game slot', () => {
    it('renders game card when session has gameId', () => {
      setMockState({ context: 'active', currentSession: activeSession });

      render(<ContextualHandSlot slotType="game" />);

      const card = screen.getByTestId('meeple-card-game');
      expect(card).toHaveAttribute('data-title', 'Gioco corrente');
    });
  });

  // ── Agent slot ──────────────────────────────────────────────────────

  describe('agent slot', () => {
    it('renders agent ready badge when agentDefinitionId exists', () => {
      setMockState({
        context: 'active',
        currentSession: activeSession,
        createResult: {
          sessionId: 's1',
          sessionCode: 'XYZ',
          participants: [],
          gameNightEventId: 'gn1',
          gameNightWasCreated: true,
          agentDefinitionId: 'a1',
          toolkitId: 't1',
        },
      });

      render(<ContextualHandSlot slotType="agent" />);

      const card = screen.getByTestId('meeple-card-agent');
      expect(card).toHaveAttribute('data-badge', 'Pronto');
      expect(screen.getByText('Chiedi')).toBeInTheDocument();
    });

    it('renders no-agent state when agentDefinitionId is null', () => {
      setMockState({
        context: 'active',
        currentSession: activeSession,
        createResult: {
          sessionId: 's1',
          sessionCode: 'XYZ',
          participants: [],
          gameNightEventId: 'gn1',
          gameNightWasCreated: true,
          agentDefinitionId: null,
          toolkitId: null,
        },
      });

      render(<ContextualHandSlot slotType="agent" />);

      const card = screen.getByTestId('meeple-card-agent');
      expect(card).toHaveAttribute('data-title', 'Nessun agente');
      expect(card).toHaveAttribute('data-badge', '--');
    });
  });

  // ── Toolkit slot ────────────────────────────────────────────────────

  describe('toolkit slot', () => {
    it('renders toolkit with Tira dado button when toolkitId exists', () => {
      setMockState({
        context: 'active',
        currentSession: activeSession,
        createResult: {
          sessionId: 's1',
          sessionCode: 'XYZ',
          participants: [],
          gameNightEventId: 'gn1',
          gameNightWasCreated: true,
          agentDefinitionId: null,
          toolkitId: 'tk1',
        },
      });

      render(<ContextualHandSlot slotType="toolkit" />);

      const card = screen.getByTestId('meeple-card-toolkit');
      expect(card).toHaveAttribute('data-badge', 'Attivo');
      expect(screen.getByText('Tira dado')).toBeInTheDocument();
    });
  });
});
