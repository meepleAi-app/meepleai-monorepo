import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { AgentStatusBadge } from '../AgentStatusBadge';
import { AgentStatus } from '@/types/agent';

describe('AgentStatusBadge', () => {
  it('renders active status', () => {
    render(<AgentStatusBadge status={AgentStatus.Active} />);
    expect(screen.getByText('Active')).toBeInTheDocument();
  });

  it('renders all status types', () => {
    const { rerender } = render(<AgentStatusBadge status={AgentStatus.Active} />);
    expect(screen.getByText('Active')).toBeInTheDocument();

    rerender(<AgentStatusBadge status={AgentStatus.Idle} />);
    expect(screen.getByText('Idle')).toBeInTheDocument();

    rerender(<AgentStatusBadge status={AgentStatus.Training} />);
    expect(screen.getByText('Training')).toBeInTheDocument();

    rerender(<AgentStatusBadge status={AgentStatus.Error} />);
    expect(screen.getByText('Error')).toBeInTheDocument();
  });
});
