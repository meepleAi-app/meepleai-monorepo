/**
 * AgentSelector — Unit tests
 *
 * Issue #4138: five tests here covered the per-game "custom agents" half of this component —
 * `fetches custom agents when gameId is provided`, `calls onSelectCustomAgent when custom agent
 * clicked`, `does not fetch agents when gameId is null`, `does not show custom agent section when
 * no custom agents exist`, `shows no create-agent link even when custom agents exist` — together
 * with a `vi.mock('@/lib/api')` that stubbed `getUserAgentsForGame`.
 *
 * All of it is gone, and so is the mock. The component no longer imports `@/lib/api` at all, which
 * is a stronger guarantee than any test: a surviving assertion like
 * `expect(mockGetUserAgentsForGame).not.toHaveBeenCalled()` would assert a property of the mock,
 * not of the component — it passes whether or not the component ever could call it.
 *
 * What remains is the real choice: the 5 chat personas. `auto|tutor|arbitro|stratega|narratore` is
 * `ChatThread.AgentType`, a separate concept that happens to share the name.
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { AgentSelector } from '@/components/chat/entry/AgentSelector';

describe('AgentSelector', () => {
  const onSelectSystem = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders all 5 system agents', () => {
    render(<AgentSelector onSelectSystemAgent={onSelectSystem} selectedAgentType="auto" />);

    expect(screen.getByTestId('agent-card-auto')).toBeInTheDocument();
    expect(screen.getByTestId('agent-card-qa')).toBeInTheDocument();
    expect(screen.getByTestId('agent-card-rules')).toBeInTheDocument();
    expect(screen.getByTestId('agent-card-strategy')).toBeInTheDocument();
    expect(screen.getByTestId('agent-card-narrative')).toBeInTheDocument();
  });

  it('calls onSelectSystemAgent when system agent clicked', () => {
    render(<AgentSelector onSelectSystemAgent={onSelectSystem} selectedAgentType={null} />);

    fireEvent.click(screen.getByTestId('agent-card-rules'));

    expect(onSelectSystem).toHaveBeenCalledWith('rules');
  });

  it('highlights selected system agent', () => {
    render(<AgentSelector onSelectSystemAgent={onSelectSystem} selectedAgentType="qa" />);

    expect(screen.getByTestId('agent-card-qa')).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByTestId('agent-card-auto')).toHaveAttribute('aria-pressed', 'false');
  });

  // The "I tuoi agent" section is gone with the per-game agents it listed. Asserted as an absence
  // so the section cannot come back silently: the positive assertions above would pass whatever
  // else the component rendered alongside them.
  it('shows no per-game agent section', () => {
    render(<AgentSelector onSelectSystemAgent={onSelectSystem} selectedAgentType="auto" />);

    expect(screen.queryByText(/I tuoi agent/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/Agent di sistema/i)).not.toBeInTheDocument();
  });
});
