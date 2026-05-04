/**
 * Wave C.1 (Issue #581) — GameDetailAgentsList unit tests.
 */

import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import {
  GameDetailAgentsList,
  type GameDetailAgentEntry,
  type GameDetailAgentsListLabels,
} from '../GameDetailAgentsList';

const labels: GameDetailAgentsListLabels = {
  title: 'Agenti AI',
  subtitle: 'Agenti collegati.',
  empty: 'Nessun agente',
  emptySubtitle: 'Crea un agente AI.',
  createCta: '+ Crea',
  openAriaLabel: 'Apri agente {name}',
  indexedLabel: '{count} KB',
  invocationsLabel: '{count} invocazioni',
};

const agents: ReadonlyArray<GameDetailAgentEntry> = [
  {
    id: 'a1',
    name: 'Wingspan Rules',
    model: 'Claude Sonnet',
    kbCount: 2,
    invocations: 230,
    isActive: true,
  },
  {
    id: 'a2',
    name: 'Setup Helper',
    model: 'GPT-4',
    kbCount: 1,
    invocations: 45,
    isActive: false,
  },
];

describe('GameDetailAgentsList (Wave C.1)', () => {
  it('renders empty state when agents is empty', () => {
    render(<GameDetailAgentsList agents={[]} labels={labels} />);
    expect(screen.getByText('Nessun agente')).toBeInTheDocument();
  });

  it('renders one row per agent', () => {
    const { container } = render(<GameDetailAgentsList agents={agents} labels={labels} />);
    expect(container.querySelectorAll('[data-slot="game-detail-agent-row"]')).toHaveLength(2);
  });

  it('substitutes {count} in indexed/invocations labels', () => {
    render(<GameDetailAgentsList agents={agents} labels={labels} />);
    expect(screen.getByText(/2 KB/)).toBeInTheDocument();
    expect(screen.getByText(/230 invocazioni/)).toBeInTheDocument();
  });

  it('exposes a per-agent open link with localized aria-label', () => {
    render(<GameDetailAgentsList agents={agents} labels={labels} />);
    expect(screen.getByRole('link', { name: 'Apri agente Wingspan Rules' })).toHaveAttribute(
      'href',
      '/agents/a1'
    );
  });

  it('exposes data-active on the status pip', () => {
    const { container } = render(<GameDetailAgentsList agents={agents} labels={labels} />);
    const pips = container.querySelectorAll('[data-slot="game-detail-agent-status"]');
    expect(pips[0]).toHaveAttribute('data-active', 'true');
    expect(pips[1]).toHaveAttribute('data-active', 'false');
  });

  it('calls onCreateAgent when the create button is clicked', () => {
    const onCreate = vi.fn();
    render(<GameDetailAgentsList agents={agents} labels={labels} onCreateAgent={onCreate} />);
    fireEvent.click(screen.getByRole('button', { name: '+ Crea' }));
    expect(onCreate).toHaveBeenCalledTimes(1);
  });
});
