import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { AgentToggleStep } from '../AgentToggleStep';

function defaultStates() {
  return { rules: true, strategy: true, setup: true, narrator: false };
}

describe('AgentToggleStep', () => {
  it('renders 4 agents as switches', () => {
    render(<AgentToggleStep agentStates={defaultStates()} onToggle={vi.fn()} />);
    expect(screen.getAllByRole('switch')).toHaveLength(4);
  });

  it('reflects aria-checked per state', () => {
    render(<AgentToggleStep agentStates={defaultStates()} onToggle={vi.fn()} />);
    expect(screen.getByRole('switch', { name: /agente regole/i })).toHaveAttribute(
      'aria-checked',
      'true'
    );
    expect(screen.getByRole('switch', { name: /agente cronista/i })).toHaveAttribute(
      'aria-checked',
      'false'
    );
  });

  it('calls onToggle with agent id on click', async () => {
    const onToggle = vi.fn();
    const user = userEvent.setup();
    render(<AgentToggleStep agentStates={defaultStates()} onToggle={onToggle} />);
    await user.click(screen.getByRole('switch', { name: /agente cronista/i }));
    expect(onToggle).toHaveBeenCalledWith('narrator');
  });

  it('active-count is aria-live and matches enabled agents', () => {
    render(<AgentToggleStep agentStates={defaultStates()} onToggle={vi.fn()} />);
    const live = screen.getByText(/3 agenti attivi/i);
    expect(live).toHaveAttribute('aria-live', 'polite');
  });

  it('uses singular form when one agent active', () => {
    render(
      <AgentToggleStep
        agentStates={{ rules: true, strategy: false, setup: false, narrator: false }}
        onToggle={vi.fn()}
      />
    );
    expect(screen.getByText(/1 agente attivo/i)).toBeInTheDocument();
  });
});
