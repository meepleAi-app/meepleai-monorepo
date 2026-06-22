/**
 * SessionToolsRail unit tests — Wave D.2 Interactions sub-PR (Issue #750)
 *
 * Coverage:
 * - Render shape (data-slot, structure)
 * - Role variant matrix (Spectator hidden, Player+Host visible)
 * - Click handler fires onToolExecute with correct toolId
 * - Compact layout (2-col vs 3-col)
 * - aria-label from executeAriaTemplate
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import type { SessionToolsRailLabels, SessionToolsRailProps } from '../SessionToolsRail';
import { SessionToolsRail } from '../SessionToolsRail';

// ─── Fixtures ─────────────────────────────────────────────────────────────────

const LABELS: SessionToolsRailLabels = {
  title: 'Strumenti',
  toolDiceLabel: 'Dado',
  toolTimerLabel: 'Timer',
  toolCardLabel: 'Carta',
  executeAriaTemplate: 'Esegui {toolName}',
  disabledSpectatorTooltip: 'Solo i giocatori possono usare gli strumenti',
};

const TOOLS: SessionToolsRailProps['tools'] = [
  { id: 'dice-1', name: 'Dado D6', icon: 'dice' },
  { id: 'timer-1', name: 'Conto alla rovescia', icon: 'timer' },
  { id: 'card-1', name: 'Mazzo Segreto', icon: 'card' },
];

function renderRail(overrides: Partial<SessionToolsRailProps> = {}) {
  const onToolExecute = vi.fn();
  const props: SessionToolsRailProps = {
    tools: TOOLS,
    viewerRole: 'Player',
    onToolExecute,
    labels: LABELS,
    ...overrides,
  };
  const result = render(<SessionToolsRail {...props} />);
  return { ...result, onToolExecute };
}

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('SessionToolsRail — render shape', () => {
  it('renders data-slot="session-tools-rail" for Player', () => {
    renderRail({ viewerRole: 'Player' });
    expect(screen.getByRole('region', { name: 'Strumenti' })).toHaveAttribute(
      'data-slot',
      'session-tools-rail'
    );
  });

  it('renders data-viewer-role attribute', () => {
    renderRail({ viewerRole: 'Host' });
    expect(screen.getByRole('region', { name: 'Strumenti' })).toHaveAttribute(
      'data-viewer-role',
      'Host'
    );
  });

  it('renders section title heading', () => {
    renderRail({ viewerRole: 'Player' });
    expect(screen.getByText('Strumenti')).toBeInTheDocument();
  });

  it('renders all tool buttons for Player', () => {
    renderRail({ viewerRole: 'Player' });
    expect(screen.getByRole('button', { name: 'Esegui Dado D6' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Esegui Conto alla rovescia' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Esegui Mazzo Segreto' })).toBeInTheDocument();
  });

  it('renders tool names as button labels', () => {
    renderRail({ viewerRole: 'Host' });
    expect(screen.getByText('Dado D6')).toBeInTheDocument();
    expect(screen.getByText('Conto alla rovescia')).toBeInTheDocument();
    expect(screen.getByText('Mazzo Segreto')).toBeInTheDocument();
  });
});

describe('SessionToolsRail — role variant matrix', () => {
  it('returns null for Spectator (rail hidden entirely)', () => {
    const { container } = renderRail({ viewerRole: 'Spectator' });
    // No rail content rendered
    expect(container.firstChild).toBeNull();
  });

  it('renders for Player role', () => {
    renderRail({ viewerRole: 'Player' });
    expect(screen.getByRole('region', { name: 'Strumenti' })).toBeInTheDocument();
  });

  it('renders for Host role', () => {
    renderRail({ viewerRole: 'Host' });
    expect(screen.getByRole('region', { name: 'Strumenti' })).toBeInTheDocument();
  });
});

describe('SessionToolsRail — click handlers', () => {
  it('calls onToolExecute with toolId on button click', async () => {
    const user = userEvent.setup();
    const { onToolExecute } = renderRail({ viewerRole: 'Player' });

    await user.click(screen.getByRole('button', { name: 'Esegui Dado D6' }));
    expect(onToolExecute).toHaveBeenCalledOnce();
    expect(onToolExecute).toHaveBeenCalledWith('dice-1');
  });

  it('calls onToolExecute with correct toolId for each tool', async () => {
    const user = userEvent.setup();
    const { onToolExecute } = renderRail({ viewerRole: 'Host' });

    await user.click(screen.getByRole('button', { name: 'Esegui Conto alla rovescia' }));
    expect(onToolExecute).toHaveBeenCalledWith('timer-1');
  });

  it('does not fire for Spectator (no buttons rendered)', () => {
    const { onToolExecute } = renderRail({ viewerRole: 'Spectator' });
    expect(screen.queryAllByRole('button')).toHaveLength(0);
    expect(onToolExecute).not.toHaveBeenCalled();
  });
});

describe('SessionToolsRail — aria-label from template', () => {
  it('substitutes {toolName} in executeAriaTemplate', () => {
    renderRail({ viewerRole: 'Player' });
    expect(screen.getByRole('button', { name: 'Esegui Dado D6' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Esegui Mazzo Segreto' })).toBeInTheDocument();
  });
});

describe('SessionToolsRail — compact layout', () => {
  it('renders with compact prop without error', () => {
    renderRail({ viewerRole: 'Player', compact: true });
    expect(screen.getByRole('region', { name: 'Strumenti' })).toBeInTheDocument();
  });
});
