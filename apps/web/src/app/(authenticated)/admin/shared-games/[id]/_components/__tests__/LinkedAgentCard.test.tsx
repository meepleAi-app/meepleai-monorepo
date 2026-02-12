/**
 * LinkedAgentCard Component Tests - Issue #4230
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';

import type { AgentDefinitionDto } from '@/lib/api/schemas/agent-definitions.schemas';

import { LinkedAgentCard } from '../LinkedAgentCard';

const mockAgent: AgentDefinitionDto = {
  id: 'agent-123',
  name: 'Catan Arbitro',
  description: 'AI assistant for Settlers of Catan',
  config: {
    model: 'gpt-4',
    maxTokens: 2048,
    temperature: 0.7,
  },
  prompts: [],
  tools: [],
  isActive: true,
  createdAt: '2024-01-01T00:00:00Z',
  updatedAt: null,
};

describe('LinkedAgentCard', () => {
  it('renders agent information correctly', () => {
    const onUnlink = vi.fn();
    const { container } = render(<LinkedAgentCard agent={mockAgent} onUnlink={onUnlink} />);

    expect(screen.getByText('Catan Arbitro')).toBeInTheDocument();
    expect(screen.getByText('AI assistant for Settlers of Catan')).toBeInTheDocument();
    expect(screen.getByText('gpt-4')).toBeInTheDocument();
    expect(screen.getByText('0.7')).toBeInTheDocument();
    expect(screen.getByText('Active')).toBeInTheDocument();

    // Max tokens may format differently by locale, so check container text
    expect(container.textContent).toContain('2048');
  });

  it('shows Inactive badge when agent is not active', () => {
    const inactiveAgent = { ...mockAgent, isActive: false };
    const onUnlink = vi.fn();

    render(<LinkedAgentCard agent={inactiveAgent} onUnlink={onUnlink} />);

    expect(screen.getByText('Inactive')).toBeInTheDocument();
  });

  it('renders Manage Agent link with correct href', () => {
    const onUnlink = vi.fn();

    render(<LinkedAgentCard agent={mockAgent} onUnlink={onUnlink} />);

    const link = screen.getByRole('link', { name: /manage agent/i });
    expect(link).toHaveAttribute('href', '/admin/agent-definitions/agent-123');
  });

  it('renders unlink button', () => {
    const onUnlink = vi.fn();

    render(<LinkedAgentCard agent={mockAgent} onUnlink={onUnlink} />);

    const unlinkButton = screen.getByRole('button', { name: /unlink/i });
    expect(unlinkButton).toBeInTheDocument();
    expect(unlinkButton).not.toBeDisabled();
  });

  it('disables unlink button when isUnlinking is true', () => {
    const onUnlink = vi.fn();

    render(<LinkedAgentCard agent={mockAgent} onUnlink={onUnlink} isUnlinking={true} />);

    const unlinkButton = screen.getByRole('button', { name: /unlink/i });
    expect(unlinkButton).toBeDisabled();
  });
});
