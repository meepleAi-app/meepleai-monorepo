/**
 * Wave B.2 (Issue #634) — AgentsResultsGrid v2 component tests.
 *
 * MeepleCard zero-fork (CLAUDE.md mandate): wraps the existing
 * `MeepleCard` from `@/components/ui/data-display/meeple-card` with a Next.js
 * `<Link>` to `/agents/{id}`. Component itself is dumb — no fetch, no filter,
 * no sort — orchestrator (AgentsLibraryView) owns derivation.
 *
 * Contract under test (spec §3.2 + plan §4.4):
 *   - 1 MeepleCard per agent, entity='agent', variant='grid'
 *   - Mapping: name→title, gameName→subtitle, derived status→CardStatus,
 *     [type, strategyName]→tags, no imageUrl/rating
 *   - Status derivation: attivo→active, in-setup→setup, archiviato→archived
 *   - Each card wrapped in Link → /agents/{id}
 *   - Layout: desktop = grid-cols-[repeat(auto-fit,minmax(320px,1fr))] max-w-[1280px] mx-auto
 *             compact = grid-cols-1 sm:grid-cols-2 px-4
 *   - Empty agents → container renders but no children
 *   - data-agent-id and data-agent-status attrs on link wrapper
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { AgentsResultsGrid } from '../AgentsResultsGrid';
import type { AgentDto } from '@/lib/api/schemas/agents.schemas';

const NOW = '2026-04-30T10:00:00.000Z';

function makeAgent(overrides: Partial<AgentDto> & Pick<AgentDto, 'id' | 'name'>): AgentDto {
  return {
    type: 'rag',
    strategyName: 'HybridSearch',
    strategyParameters: {},
    isActive: true,
    createdAt: NOW,
    lastInvokedAt: null,
    invocationCount: 5,
    isRecentlyUsed: true,
    isIdle: false,
    gameId: null,
    gameName: null,
    createdByUserId: null,
    ...overrides,
  };
}

const AGENTS: readonly AgentDto[] = [
  makeAgent({
    id: '00000000-0000-4000-8000-000000000001',
    name: 'Catan RAG',
    type: 'rag',
    strategyName: 'HybridSearch',
    gameName: 'Catan',
    isActive: true,
    invocationCount: 12,
  }),
  makeAgent({
    id: '00000000-0000-4000-8000-000000000002',
    name: 'Wingspan Setup',
    type: 'setup',
    strategyName: 'VectorOnly',
    gameName: 'Wingspan',
    isActive: true,
    invocationCount: 0, // → in-setup
  }),
  makeAgent({
    id: '00000000-0000-4000-8000-000000000003',
    name: 'Old Bot',
    type: 'rag',
    strategyName: 'CitationValidation',
    gameName: null, // null gameName → no subtitle
    isActive: false, // → archiviato
    invocationCount: 99,
  }),
];

describe('AgentsResultsGrid (Wave B.2)', () => {
  it('renders one MeepleCard per agent with entity="agent"', () => {
    const { container } = render(<AgentsResultsGrid agents={AGENTS} />);
    const cards = container.querySelectorAll('[data-entity="agent"]');
    expect(cards).toHaveLength(3);
  });

  it('renders title from agent.name (heading role)', () => {
    render(<AgentsResultsGrid agents={AGENTS} />);
    expect(screen.getByRole('heading', { name: 'Catan RAG' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Wingspan Setup' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Old Bot' })).toBeInTheDocument();
  });

  it('maps agent.gameName to MeepleCard subtitle when present', () => {
    render(<AgentsResultsGrid agents={AGENTS.slice(0, 1)} />);
    expect(screen.getByText('Catan')).toBeInTheDocument();
  });

  it('renders no subtitle when agent.gameName is null', () => {
    render(<AgentsResultsGrid agents={[AGENTS[2]]} />);
    expect(screen.getByRole('heading', { name: 'Old Bot' })).toBeInTheDocument();
    // Sanity: the only "Old Bot" appears as a heading, not as subtitle text.
    expect(screen.queryByText('Catan')).toBeNull();
    expect(screen.queryByText('Wingspan')).toBeNull();
  });

  it('derives status="attivo" → CardStatus="active" for isActive && invocationCount>0', () => {
    const { container } = render(<AgentsResultsGrid agents={[AGENTS[0]]} />);
    const link = container.querySelector(`[data-agent-id="${AGENTS[0].id}"]`) as HTMLElement;
    expect(link).not.toBeNull();
    expect(link.getAttribute('data-agent-status')).toBe('attivo');
  });

  it('derives status="in-setup" → CardStatus="setup" for isActive && invocationCount===0', () => {
    const { container } = render(<AgentsResultsGrid agents={[AGENTS[1]]} />);
    const link = container.querySelector(`[data-agent-id="${AGENTS[1].id}"]`) as HTMLElement;
    expect(link).not.toBeNull();
    expect(link.getAttribute('data-agent-status')).toBe('in-setup');
  });

  it('derives status="archiviato" → CardStatus="archived" for !isActive', () => {
    const { container } = render(<AgentsResultsGrid agents={[AGENTS[2]]} />);
    const link = container.querySelector(`[data-agent-id="${AGENTS[2].id}"]`) as HTMLElement;
    expect(link).not.toBeNull();
    expect(link.getAttribute('data-agent-status')).toBe('archiviato');
  });

  it('passes [agent.type, agent.strategyName] as tags to MeepleCard', () => {
    render(<AgentsResultsGrid agents={[AGENTS[0]]} />);
    // Tags surface as text in the card body.
    expect(screen.getByText('rag')).toBeInTheDocument();
    expect(screen.getByText('HybridSearch')).toBeInTheDocument();
  });

  it('wraps each card in a link to /agents/{id}', () => {
    const { container } = render(<AgentsResultsGrid agents={AGENTS} />);
    const links = container.querySelectorAll('a[href^="/agents/"]');
    expect(links).toHaveLength(3);
    const hrefs = Array.from(links).map(a => a.getAttribute('href'));
    expect(hrefs).toEqual(
      expect.arrayContaining([
        `/agents/${AGENTS[0].id}`,
        `/agents/${AGENTS[1].id}`,
        `/agents/${AGENTS[2].id}`,
      ])
    );
  });

  it('uses CSS-grid auto-fit desktop layout when not compact', () => {
    const { container } = render(<AgentsResultsGrid agents={AGENTS} />);
    const root = container.querySelector('[data-slot="agents-results-grid"]');
    expect(root).toHaveClass(
      'grid',
      'grid-cols-[repeat(auto-fit,minmax(320px,1fr))]',
      'gap-4',
      'max-w-[1280px]',
      'mx-auto'
    );
  });

  it('uses compact mobile layout (grid-cols-1 sm:grid-cols-2 px-4) when compact=true', () => {
    const { container } = render(<AgentsResultsGrid agents={AGENTS} compact />);
    const root = container.querySelector('[data-slot="agents-results-grid"]');
    expect(root).toHaveClass('grid', 'grid-cols-1', 'gap-3', 'px-4', 'sm:grid-cols-2');
  });

  it('renders the container with no card children when agents=[]', () => {
    const { container } = render(<AgentsResultsGrid agents={[]} />);
    const root = container.querySelector('[data-slot="agents-results-grid"]');
    expect(root).toBeInTheDocument();
    expect(root!.querySelectorAll('[data-entity="agent"]')).toHaveLength(0);
  });

  it('exposes data-slot="agents-results-grid-link" on each link wrapper for spec scoping', () => {
    const { container } = render(<AgentsResultsGrid agents={AGENTS} />);
    const links = container.querySelectorAll('[data-slot="agents-results-grid-link"]');
    expect(links).toHaveLength(3);
  });
});
