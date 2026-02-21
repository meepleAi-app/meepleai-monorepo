/**
 * MeepleAgentCard Tests
 * Issue #5000 — Agent Card: azioni contestuali e visibilità condizionale
 *
 * Tests the action visibility matrix:
 * - Info/Dettaglio: always visible
 * - Chat: always visible, disabled if !isActive
 * - Configura: visible only for owner/admin
 * - Elimina: visible only for owner/admin
 */

import React from 'react';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';

import { MeepleAgentCard, MeepleAgentCardSkeleton } from '../MeepleAgentCard';

import type { AgentDto } from '@/lib/api';

// ============================================================================
// Mock Data
// ============================================================================

const mockActiveAgent: AgentDto = {
  id: 'agent-123',
  name: 'RulesMaster',
  type: 'Tutor',
  strategyName: 'Balanced',
  strategyParameters: {},
  isActive: true,
  createdAt: '2024-01-01T00:00:00Z',
  lastInvokedAt: '2024-06-01T12:00:00Z',
  invocationCount: 42,
  isRecentlyUsed: true,
  isIdle: false,
};

const mockInactiveAgent: AgentDto = {
  ...mockActiveAgent,
  id: 'agent-456',
  name: 'InactiveBot',
  isActive: false,
  isIdle: true,
  lastInvokedAt: null,
  invocationCount: 0,
};

// ============================================================================
// Tests
// ============================================================================

describe('MeepleAgentCard', () => {
  // --------------------------------------------------------------------------
  // Basic rendering
  // --------------------------------------------------------------------------

  it('renders agent name as title', () => {
    render(<MeepleAgentCard agent={mockActiveAgent} />);
    expect(screen.getByText('RulesMaster')).toBeInTheDocument();
  });

  it('renders agent type as subtitle', () => {
    render(<MeepleAgentCard agent={mockActiveAgent} />);
    expect(screen.getByText('Tutor agent')).toBeInTheDocument();
  });

  it('renders with correct data-testid', () => {
    render(<MeepleAgentCard agent={mockActiveAgent} />);
    expect(screen.getByTestId('agent-card-agent-123')).toBeInTheDocument();
  });

  it('renders agent status badge for active agent', () => {
    render(<MeepleAgentCard agent={mockActiveAgent} />);
    expect(screen.getByTestId('agent-status-active')).toBeInTheDocument();
  });

  it('renders idle status badge for inactive agent', () => {
    render(<MeepleAgentCard agent={mockInactiveAgent} />);
    expect(screen.getByTestId('agent-status-idle')).toBeInTheDocument();
  });

  it('renders agent stats with invocation count', () => {
    render(<MeepleAgentCard agent={mockActiveAgent} />);
    expect(screen.getByTestId('invocation-count')).toBeInTheDocument();
  });

  // --------------------------------------------------------------------------
  // AC: Chat — always visible, disabled with tooltip if !isActive
  // --------------------------------------------------------------------------

  it('Chat action is visible and enabled for active agent', () => {
    render(<MeepleAgentCard agent={mockActiveAgent} />);

    const chatButton = screen.getByRole('button', { name: 'Chat' });
    expect(chatButton).toBeInTheDocument();
    expect(chatButton).not.toBeDisabled();
  });

  it('Chat action is disabled for inactive agent with tooltip label', () => {
    render(<MeepleAgentCard agent={mockInactiveAgent} />);

    const chatButton = screen.getByRole('button', { name: 'Agente non attivo' });
    expect(chatButton).toBeInTheDocument();
    expect(chatButton).toBeDisabled();
  });

  // --------------------------------------------------------------------------
  // AC: Configura — visible only for owner/admin
  // --------------------------------------------------------------------------

  it('Configura not shown for non-owner non-admin user', () => {
    render(
      <MeepleAgentCard
        agent={mockActiveAgent}
        isOwner={false}
        isAdmin={false}
      />
    );
    expect(screen.queryByRole('button', { name: 'Configura' })).not.toBeInTheDocument();
  });

  it('Configura shown for owner', () => {
    render(
      <MeepleAgentCard
        agent={mockActiveAgent}
        isOwner={true}
        isAdmin={false}
      />
    );
    expect(screen.getByRole('button', { name: 'Configura' })).toBeInTheDocument();
  });

  it('Configura shown for admin', () => {
    render(
      <MeepleAgentCard
        agent={mockActiveAgent}
        isOwner={false}
        isAdmin={true}
      />
    );
    expect(screen.getByRole('button', { name: 'Configura' })).toBeInTheDocument();
  });

  it('Configura calls onConfigure with agent id and name when clicked', async () => {
    const user = userEvent.setup();
    const onConfigure = vi.fn();

    render(
      <MeepleAgentCard
        agent={mockActiveAgent}
        isOwner={true}
        onConfigure={onConfigure}
      />
    );

    await user.click(screen.getByRole('button', { name: 'Configura' }));
    expect(onConfigure).toHaveBeenCalledWith('agent-123', 'RulesMaster');
  });

  // --------------------------------------------------------------------------
  // AC: Elimina — visible only for owner/admin
  // --------------------------------------------------------------------------

  it('Elimina not shown for non-owner non-admin user', () => {
    render(
      <MeepleAgentCard
        agent={mockActiveAgent}
        isOwner={false}
        isAdmin={false}
      />
    );
    expect(screen.queryByRole('button', { name: 'Elimina' })).not.toBeInTheDocument();
  });

  it('Elimina shown for owner', () => {
    render(
      <MeepleAgentCard
        agent={mockActiveAgent}
        isOwner={true}
        isAdmin={false}
      />
    );
    expect(screen.getByRole('button', { name: 'Elimina' })).toBeInTheDocument();
  });

  it('Elimina shown for admin', () => {
    render(
      <MeepleAgentCard
        agent={mockActiveAgent}
        isOwner={false}
        isAdmin={true}
      />
    );
    expect(screen.getByRole('button', { name: 'Elimina' })).toBeInTheDocument();
  });

  it('Elimina calls onDelete with agent id and name when clicked', async () => {
    const user = userEvent.setup();
    const onDelete = vi.fn();

    render(
      <MeepleAgentCard
        agent={mockActiveAgent}
        isOwner={true}
        onDelete={onDelete}
      />
    );

    await user.click(screen.getByRole('button', { name: 'Elimina' }));
    expect(onDelete).toHaveBeenCalledWith('agent-123', 'RulesMaster');
  });

  // --------------------------------------------------------------------------
  // AC: No Attiva/Disattiva or Clona actions
  // --------------------------------------------------------------------------

  it('does not render Attiva/Disattiva action', () => {
    render(<MeepleAgentCard agent={mockActiveAgent} isOwner isAdmin />);
    expect(screen.queryByRole('button', { name: /attiva|disattiva/i })).not.toBeInTheDocument();
  });

  it('does not render Clona action', () => {
    render(<MeepleAgentCard agent={mockActiveAgent} isOwner isAdmin />);
    expect(screen.queryByRole('button', { name: /clona/i })).not.toBeInTheDocument();
  });

  // --------------------------------------------------------------------------
  // Unauthenticated user: only Info/Dettaglio (no owner, no admin defaults)
  // --------------------------------------------------------------------------

  it('unauthenticated user sees only Chat (disabled) and Info — no Configura or Elimina', () => {
    render(<MeepleAgentCard agent={mockInactiveAgent} />);

    // Chat is present but disabled
    expect(screen.getByRole('button', { name: 'Agente non attivo' })).toBeInTheDocument();
    // Configura and Elimina are hidden
    expect(screen.queryByRole('button', { name: 'Configura' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Elimina' })).not.toBeInTheDocument();
  });

  // --------------------------------------------------------------------------
  // Selection mode
  // --------------------------------------------------------------------------

  it('shows checkbox in selection mode', () => {
    render(
      <MeepleAgentCard
        agent={mockActiveAgent}
        selectionMode={true}
        isSelected={false}
        onSelect={vi.fn()}
      />
    );

    const checkbox = screen.getByRole('checkbox');
    expect(checkbox).toBeInTheDocument();
    expect(checkbox).not.toBeChecked();
  });

  it('shows checked checkbox when selected', () => {
    render(
      <MeepleAgentCard
        agent={mockActiveAgent}
        selectionMode={true}
        isSelected={true}
        onSelect={vi.fn()}
      />
    );

    expect(screen.getByRole('checkbox')).toBeChecked();
  });

  // --------------------------------------------------------------------------
  // Skeleton
  // --------------------------------------------------------------------------

  it('renders skeleton with correct testid', () => {
    render(<MeepleAgentCardSkeleton />);
    // MeepleCard in loading mode hardcodes data-testid="meeple-card-skeleton"
    expect(screen.getByTestId('meeple-card-skeleton')).toBeInTheDocument();
  });
});
