import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import type { ReactNode } from 'react';

import { UsedByPanel } from '../UsedByPanel';
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

const mockUseKbDocConsumingAgents = vi.fn();
vi.mock('@/hooks/queries/useKbDocConsumingAgents', () => ({
  useKbDocConsumingAgents: (opts: unknown) => mockUseKbDocConsumingAgents(opts),
}));

const agent: KbDocConsumingAgent = {
  id: '11111111-1111-1111-1111-111111111111',
  name: 'Alpha',
  type: 'HybridSearch',
  isActive: true,
  status: 'Published',
  isSystemDefined: false,
  typologySlug: null,
  gameId: null,
  gameName: null,
  invocationCount: 0,
  lastInvokedAt: null,
};

describe('UsedByPanel', () => {
  beforeEach(() => {
    mockUseKbDocConsumingAgents.mockReset();
  });

  it('renders the loading skeleton', () => {
    mockUseKbDocConsumingAgents.mockReturnValue({
      isLoading: true,
      isError: false,
      data: undefined,
    });
    render(<UsedByPanel docId="doc-1" />);
    expect(screen.getByTestId('used-by-panel-loading')).toBeInTheDocument();
  });

  it('renders the error state', () => {
    mockUseKbDocConsumingAgents.mockReturnValue({
      isLoading: false,
      isError: true,
      error: new Error('boom'),
      data: undefined,
    });
    render(<UsedByPanel docId="doc-1" />);
    expect(screen.getByTestId('used-by-panel-error')).toBeInTheDocument();
    expect(screen.getByText(/boom/)).toBeInTheDocument();
  });

  it('renders the empty state when no consumers', () => {
    mockUseKbDocConsumingAgents.mockReturnValue({
      isLoading: false,
      isError: false,
      data: [],
    });
    render(<UsedByPanel docId="doc-1" />);
    expect(screen.getByTestId('used-by-empty')).toBeInTheDocument();
  });

  it('renders the list when consumers are present', () => {
    mockUseKbDocConsumingAgents.mockReturnValue({
      isLoading: false,
      isError: false,
      data: [agent, { ...agent, id: '22222222-2222-2222-2222-222222222222', name: 'Beta' }],
    });
    render(<UsedByPanel docId="doc-1" />);
    expect(screen.getByTestId('used-by-panel')).toBeInTheDocument();
    expect(screen.getByText(/Agent che consumano questo documento \(2\)/)).toBeInTheDocument();
    expect(screen.getByText('Alpha')).toBeInTheDocument();
    expect(screen.getByText('Beta')).toBeInTheDocument();
  });
});
