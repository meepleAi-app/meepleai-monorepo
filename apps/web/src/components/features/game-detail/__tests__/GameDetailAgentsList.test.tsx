/**
 * Wave C.1 (Issue #581) — GameDetailAgentsList unit tests.
 *
 * Critical: tests all 4 discriminated union kinds per Phase 0.5 contract sez. 4.3.
 * The discriminated union prevents `data + loading` co-occurrence.
 *
 * Covers:
 *  1. kind='loading' — skeleton rendered, no agent rows
 *  2. kind='error' — error banner + retry button wired
 *  3. kind='empty' — empty state + create CTA
 *  4. kind='success' — agent rows rendered, open links, status pip
 */

import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import {
  GameDetailAgentsList,
  type AgentsState,
  type GameDetailAgentEntry,
  type GameDetailAgentsListLabels,
} from '../GameDetailAgentsList';

const labels: GameDetailAgentsListLabels = {
  title: 'Agenti AI',
  subtitle: 'Agenti collegati.',
  loadingLabel: 'Caricamento agenti',
  errorLabel: 'Errore nel caricamento agenti',
  errorSubtitle: 'Riprova più tardi.',
  retryLabel: 'Riprova',
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

describe('GameDetailAgentsList (Wave C.1) — discriminated union', () => {
  it('renders loading skeleton for kind="loading"', () => {
    const state: AgentsState = { kind: 'loading' };
    const { container } = render(<GameDetailAgentsList state={state} labels={labels} />);
    const loadingEl = container.querySelector('[data-slot="game-detail-agents-loading"]');
    expect(loadingEl).toBeInTheDocument();
    expect(loadingEl).toHaveAttribute('aria-busy', 'true');
    // No agent rows in loading state
    expect(container.querySelectorAll('[data-slot="game-detail-agent-row"]')).toHaveLength(0);
  });

  it('renders error banner with retry CTA for kind="error"', () => {
    const retry = vi.fn();
    const state: AgentsState = { kind: 'error', retry };
    render(<GameDetailAgentsList state={state} labels={labels} />);

    expect(screen.getByRole('alert')).toBeInTheDocument();
    expect(screen.getByText('Errore nel caricamento agenti')).toBeInTheDocument();
    const retryBtn = screen.getByRole('button', { name: 'Riprova' });
    fireEvent.click(retryBtn);
    expect(retry).toHaveBeenCalledTimes(1);
  });

  it('renders empty state with create CTA for kind="empty"', () => {
    const ctaCreate = vi.fn();
    const state: AgentsState = { kind: 'empty', ctaCreate };
    render(<GameDetailAgentsList state={state} labels={labels} />);

    expect(screen.getByText('Nessun agente')).toBeInTheDocument();
    const createBtn = screen.getByRole('button', { name: '+ Crea' });
    fireEvent.click(createBtn);
    expect(ctaCreate).toHaveBeenCalledTimes(1);
  });

  it('renders agent rows for kind="success"', () => {
    const state: AgentsState = { kind: 'success', agents };
    const { container } = render(<GameDetailAgentsList state={state} labels={labels} />);
    expect(container.querySelectorAll('[data-slot="game-detail-agent-row"]')).toHaveLength(2);
  });

  it('substitutes {count} in indexed/invocations labels for kind="success"', () => {
    const state: AgentsState = { kind: 'success', agents };
    render(<GameDetailAgentsList state={state} labels={labels} />);
    expect(screen.getByText(/2 KB/)).toBeInTheDocument();
    expect(screen.getByText(/230 invocazioni/)).toBeInTheDocument();
  });

  it('exposes a per-agent open link with localized aria-label for kind="success"', () => {
    const state: AgentsState = { kind: 'success', agents };
    render(<GameDetailAgentsList state={state} labels={labels} />);
    expect(screen.getByRole('link', { name: 'Apri agente Wingspan Rules' })).toHaveAttribute(
      'href',
      '/agents/a1'
    );
  });

  it('exposes data-active on status pip for kind="success"', () => {
    const state: AgentsState = { kind: 'success', agents };
    const { container } = render(<GameDetailAgentsList state={state} labels={labels} />);
    const pips = container.querySelectorAll('[data-slot="game-detail-agent-status"]');
    expect(pips[0]).toHaveAttribute('data-active', 'true');
    expect(pips[1]).toHaveAttribute('data-active', 'false');
  });

  it('exposes data-slot="game-detail-agents-list" for E2E selector', () => {
    const state: AgentsState = { kind: 'loading' };
    const { container } = render(<GameDetailAgentsList state={state} labels={labels} />);
    expect(container.querySelector('[data-slot="game-detail-agents-list"]')).toBeInTheDocument();
  });
});
