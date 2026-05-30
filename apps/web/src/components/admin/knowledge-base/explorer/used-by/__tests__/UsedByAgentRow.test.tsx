import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import type { ReactNode } from 'react';

import { UsedByAgentRow } from '../UsedByAgentRow';
import type { KbDocConsumingAgent } from '@/lib/api/schemas/kb-consuming-agents.schemas';

vi.mock('next/link', () => ({
  default: ({
    href,
    children,
    ...rest
  }: {
    href: string;
    children: ReactNode;
    [k: string]: unknown;
  }) => (
    <a href={href} {...rest}>
      {children}
    </a>
  ),
}));

const baseAgent: KbDocConsumingAgent = {
  id: '11111111-1111-1111-1111-111111111111',
  name: 'Alpha',
  type: 'HybridSearch',
  isActive: true,
  status: 'Published',
  isSystemDefined: false,
  typologySlug: null,
  gameId: '22222222-2222-2222-2222-222222222222',
  gameName: 'Wingspan',
  invocationCount: 42,
  lastInvokedAt: '2026-05-20T10:30:00Z',
};

function renderRow(overrides: Partial<KbDocConsumingAgent> = {}) {
  return render(
    <ul>
      <UsedByAgentRow agent={{ ...baseAgent, ...overrides }} />
    </ul>
  );
}

describe('UsedByAgentRow', () => {
  it('renders the agent name and game name', () => {
    renderRow();
    expect(screen.getByText('Alpha')).toBeInTheDocument();
    expect(screen.getByText('Wingspan')).toBeInTheDocument();
  });

  it('links to the agent detail in the AI Lab', () => {
    renderRow();
    const link = screen.getByRole('link');
    expect(link).toHaveAttribute(
      'href',
      '/admin/agents/definitions/11111111-1111-1111-1111-111111111111'
    );
  });

  it('shows "KB globale" label when gameName is null', () => {
    renderRow({ gameName: null, gameId: null });
    expect(screen.getByText('KB globale')).toBeInTheDocument();
  });

  it('shows the system badge for system agents', () => {
    renderRow({ isSystemDefined: true, typologySlug: 'arbitro' });
    expect(screen.getByTestId('used-by-system-badge')).toBeInTheDocument();
  });

  it('hides the system badge for custom agents', () => {
    renderRow({ isSystemDefined: false });
    expect(screen.queryByTestId('used-by-system-badge')).not.toBeInTheDocument();
  });

  it('renders the status chip with the correct status label', () => {
    renderRow({ status: 'Draft' });
    expect(screen.getByText('Draft')).toBeInTheDocument();
  });

  it('renders "mai" when lastInvokedAt is null', () => {
    renderRow({ lastInvokedAt: null });
    expect(screen.getByText(/ultimo mai/i)).toBeInTheDocument();
  });
});
