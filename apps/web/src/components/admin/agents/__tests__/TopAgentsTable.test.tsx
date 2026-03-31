/**
 * TopAgentsTable Tests
 * Issue #3382: Agent Metrics Dashboard
 * Issue #4862: Updated for EntityTableView migration
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { TopAgentsTable } from '../TopAgentsTable';

const mockEntityTableView = vi.fn(() => null);
vi.mock('@/components/ui/data-display/entity-list-view', () => ({
  EntityTableView: (props: Record<string, unknown>) => {
    mockEntityTableView(props);
    return <div data-testid={props['data-testid'] as string}>EntityTableView</div>;
  },
}));

const mockAgents = [
  {
    agentDefinitionId: 'agent-1',
    typologyName: 'Rules Expert',
    invocations: 5000,
    cost: 25.5,
    avgConfidence: 0.92,
    avgLatencyMs: 750,
  },
  {
    agentDefinitionId: 'agent-2',
    typologyName: 'Strategy Advisor',
    invocations: 3500,
    cost: 18.75,
    avgConfidence: 0.85,
    avgLatencyMs: 850,
  },
  {
    agentDefinitionId: 'agent-3',
    typologyName: 'FAQ Assistant',
    invocations: 2000,
    cost: 0.005,
    avgConfidence: 0.45,
    avgLatencyMs: 1500,
  },
];

describe('TopAgentsTable', () => {
  beforeEach(() => {
    mockEntityTableView.mockClear();
  });

  it('renders with entity="agent"', () => {
    render(<TopAgentsTable agents={mockAgents} />);

    expect(mockEntityTableView).toHaveBeenCalledWith(expect.objectContaining({ entity: 'agent' }));
  });

  it('passes agents as displayItems and items', () => {
    render(<TopAgentsTable agents={mockAgents} />);

    expect(mockEntityTableView).toHaveBeenCalledWith(
      expect.objectContaining({
        displayItems: mockAgents,
        items: mockAgents,
      })
    );
  });

  it('provides 5 tableColumns with correct IDs', () => {
    render(<TopAgentsTable agents={mockAgents} />);

    const call = mockEntityTableView.mock.calls[0]?.[0] as Record<string, unknown> | undefined;
    const columns = call?.tableColumns as Array<{ id: string; header: string }>;

    expect(columns).toHaveLength(5);
    expect(columns.map(c => c.header)).toEqual([
      'Agent',
      'Invocations',
      'Cost',
      'Confidence',
      'Latency',
    ]);
  });

  it('renderItem maps agent title and id', () => {
    render(<TopAgentsTable agents={mockAgents} />);

    const call = mockEntityTableView.mock.calls[0]?.[0] as Record<string, unknown> | undefined;
    const renderItem = call?.renderItem as (a: (typeof mockAgents)[0]) => Record<string, unknown>;
    const result = renderItem(mockAgents[0]);

    expect(result.title).toBe('Rules Expert');
    expect(result.id).toBe('agent-1');
  });

  it('renderItem formats invocations as locale string', () => {
    render(<TopAgentsTable agents={mockAgents} />);

    const call = mockEntityTableView.mock.calls[0]?.[0] as Record<string, unknown> | undefined;
    const renderItem = call?.renderItem as (a: (typeof mockAgents)[0]) => Record<string, unknown>;
    const result = renderItem(mockAgents[0]);
    const metadata = result.metadata as Array<{ value: string }>;

    expect(metadata[0].value).toBe((5000).toLocaleString());
  });

  it('renderItem formats cost as dollar amount', () => {
    render(<TopAgentsTable agents={mockAgents} />);

    const call = mockEntityTableView.mock.calls[0]?.[0] as Record<string, unknown> | undefined;
    const renderItem = call?.renderItem as (a: (typeof mockAgents)[0]) => Record<string, unknown>;

    const highCost = renderItem(mockAgents[0]);
    expect((highCost.metadata as Array<{ value: string }>)[1].value).toBe('$25.50');

    const lowCost = renderItem(mockAgents[2]);
    expect((lowCost.metadata as Array<{ value: string }>)[1].value).toBe('$0.0050');
  });

  it('renderItem formats confidence as percentage', () => {
    render(<TopAgentsTable agents={mockAgents} />);

    const call = mockEntityTableView.mock.calls[0]?.[0] as Record<string, unknown> | undefined;
    const renderItem = call?.renderItem as (a: (typeof mockAgents)[0]) => Record<string, unknown>;

    const result = renderItem(mockAgents[0]);
    expect((result.metadata as Array<{ value: string }>)[2].value).toBe('92%');
  });

  it('renderItem formats latency as ms or seconds', () => {
    render(<TopAgentsTable agents={mockAgents} />);

    const call = mockEntityTableView.mock.calls[0]?.[0] as Record<string, unknown> | undefined;
    const renderItem = call?.renderItem as (a: (typeof mockAgents)[0]) => Record<string, unknown>;

    const fastAgent = renderItem(mockAgents[0]);
    expect((fastAgent.metadata as Array<{ value: string }>)[3].value).toBe('750ms');

    const slowAgent = renderItem(mockAgents[2]);
    expect((slowAgent.metadata as Array<{ value: string }>)[3].value).toBe('1.5s');
  });

  it('passes empty message', () => {
    render(<TopAgentsTable agents={mockAgents} />);

    expect(mockEntityTableView).toHaveBeenCalledWith(
      expect.objectContaining({ emptyMessage: 'No agents found' })
    );
  });

  it('sets correct data-testid', () => {
    render(<TopAgentsTable agents={mockAgents} />);

    expect(screen.getByTestId('top-agents-table')).toBeInTheDocument();
  });

  it('handles empty agents array', () => {
    render(<TopAgentsTable agents={[]} />);

    expect(mockEntityTableView).toHaveBeenCalledWith(expect.objectContaining({ displayItems: [] }));
  });
});
