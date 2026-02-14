import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { AgentStatusBadge } from '../AgentStatusBadge';

describe('AgentStatusBadge', () => {
  it('renders active status', () => {
    render(<AgentStatusBadge status="active" />);
    expect(screen.getByText('Active')).toBeInTheDocument();
  });

  it('renders all status types', () => {
    const { rerender } = render(<AgentStatusBadge status="active" />);
    expect(screen.getByText('Active')).toBeInTheDocument();

    rerender(<AgentStatusBadge status="idle" />);
    expect(screen.getByText('Idle')).toBeInTheDocument();

    rerender(<AgentStatusBadge status="training" />);
    expect(screen.getByText('Training')).toBeInTheDocument();

    rerender(<AgentStatusBadge status="error" />);
    expect(screen.getByText('Error')).toBeInTheDocument();
  });
});
