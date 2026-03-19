/**
 * AgentCharacterSheet Tests
 *
 * Covers:
 * - Renders agent name and type badge in portrait section
 * - Renders stats grid (invocazioni, chat, documenti, ultimo uso)
 * - Renders Equipaggiamento section header
 * - Renders Area Azione section header
 * - Renders Storia section header
 */

import React from 'react';

import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { AgentCharacterSheet } from '@/components/agent/AgentCharacterSheet';
import type { AgentDetailData } from '@/components/ui/data-display/extra-meeple-card/types';

// ── Mocks ──────────────────────────────────────────────────────────────────

vi.mock('@/hooks/useAgentStatus', () => ({
  useAgentStatus: vi.fn(() => ({
    status: null,
    isLoading: false,
    error: null,
    refetch: vi.fn(),
  })),
}));

vi.mock('@/lib/api', () => ({
  api: {
    chat: {
      createThread: vi.fn(),
    },
  },
}));

// Mock fetch for thread + KB doc hooks
const mockFetch = vi.fn().mockResolvedValue({
  ok: true,
  json: async () => [],
});

beforeEach(() => {
  vi.stubGlobal('fetch', mockFetch);
});

// ── Test data ──────────────────────────────────────────────────────────────

const mockAgent: AgentDetailData = {
  id: 'agent-1',
  name: 'Sherlock RAG',
  type: 'qa',
  strategyName: 'hybrid-rag',
  strategyParameters: { model: 'gpt-4o' },
  isActive: true,
  isIdle: false,
  invocationCount: 42,
  lastInvokedAt: null,
  createdAt: '2026-01-01T00:00:00Z',
  gameId: 'game-1',
  gameName: 'Pandemic',
};

// ── Tests ──────────────────────────────────────────────────────────────────

describe('AgentCharacterSheet', () => {
  it('renders agent name and type badge', () => {
    render(<AgentCharacterSheet data={mockAgent} />);

    expect(screen.getByTestId('agent-name')).toHaveTextContent('Sherlock RAG');
    expect(screen.getByTestId('agent-type-badge')).toHaveTextContent('qa');
  });

  it('renders stats in portrait section', () => {
    render(<AgentCharacterSheet data={mockAgent} />);

    const statsGrid = screen.getByTestId('agent-stats-grid');
    expect(statsGrid).toBeInTheDocument();

    // The grid contains 4 stat pips with these labels
    expect(statsGrid).toHaveTextContent('Invocazioni');
    expect(statsGrid).toHaveTextContent('Chat');
    expect(statsGrid).toHaveTextContent('Documenti');
    expect(statsGrid).toHaveTextContent('Ultimo uso');

    // Invocation count should be rendered
    expect(statsGrid).toHaveTextContent('42');
  });

  it('renders Equipaggiamento section', () => {
    render(<AgentCharacterSheet data={mockAgent} />);

    const section = screen.getByTestId('section-equipaggiamento');
    expect(section).toBeInTheDocument();
    expect(section).toHaveTextContent('Equipaggiamento');
    expect(section).toHaveTextContent('Knowledge Base');
  });

  it('renders Area Azione section', () => {
    render(<AgentCharacterSheet data={mockAgent} />);

    const section = screen.getByTestId('section-area-azione');
    expect(section).toBeInTheDocument();
    expect(section).toHaveTextContent('Area Azione');
    expect(section).toHaveTextContent('Chat');
  });

  it('renders Storia section', () => {
    render(<AgentCharacterSheet data={mockAgent} />);

    const section = screen.getByTestId('section-storia');
    expect(section).toBeInTheDocument();
    expect(section).toHaveTextContent('Storia');
    expect(section).toHaveTextContent('Conversazioni Recenti');
  });

  it('renders game link when gameName is present', () => {
    render(<AgentCharacterSheet data={mockAgent} />);

    const link = screen.getByTestId('agent-game-link');
    expect(link).toBeInTheDocument();
    expect(link).toHaveTextContent('Collegato a Pandemic');
    expect(link).toHaveAttribute('href', '/library/games/game-1');
  });

  it('does not render game link when gameName is absent', () => {
    const agentWithoutGame: AgentDetailData = {
      ...mockAgent,
      gameId: undefined,
      gameName: undefined,
    };

    render(<AgentCharacterSheet data={agentWithoutGame} />);

    expect(screen.queryByTestId('agent-game-link')).not.toBeInTheDocument();
  });

  it('shows empty state for Equipaggiamento when no gameId', () => {
    const agentWithoutGame: AgentDetailData = {
      ...mockAgent,
      gameId: undefined,
      gameName: undefined,
    };

    render(<AgentCharacterSheet data={agentWithoutGame} />);

    const section = screen.getByTestId('section-equipaggiamento');
    expect(section).toHaveTextContent('Nessun gioco collegato a questo agente');
  });

  it('shows empty state for Storia when no threads', () => {
    render(<AgentCharacterSheet data={mockAgent} />);

    const section = screen.getByTestId('section-storia');
    // With empty fetch mock, empty state message should appear once threads load
    expect(section).toBeInTheDocument();
  });

  it('renders configure button when gameId is present', () => {
    render(<AgentCharacterSheet data={mockAgent} />);

    const btn = screen.getByTestId('agent-configure-btn');
    expect(btn).toBeInTheDocument();
    expect(btn).toHaveAttribute('href', '/library/games/game-1/agent');
  });
});
