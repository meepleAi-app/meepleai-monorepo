/**
 * TopAgentsTable Tests
 * Issue #3382: Agent Metrics Dashboard
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { TopAgentsTable } from '../TopAgentsTable';

const mockAgents = [
  {
    typologyId: 'agent-1',
    typologyName: 'Rules Expert',
    invocations: 5000,
    cost: 25.50,
    avgConfidence: 0.92,
    avgLatencyMs: 750,
  },
  {
    typologyId: 'agent-2',
    typologyName: 'Strategy Advisor',
    invocations: 3500,
    cost: 18.75,
    avgConfidence: 0.85,
    avgLatencyMs: 850,
  },
  {
    typologyId: 'agent-3',
    typologyName: 'FAQ Assistant',
    invocations: 2000,
    cost: 8.25,
    avgConfidence: 0.65,
    avgLatencyMs: 450,
  },
];

describe('TopAgentsTable', () => {
  it('renders agent names', () => {
    render(<TopAgentsTable agents={mockAgents} />);

    expect(screen.getByText('Rules Expert')).toBeInTheDocument();
    expect(screen.getByText('Strategy Advisor')).toBeInTheDocument();
    expect(screen.getByText('FAQ Assistant')).toBeInTheDocument();
  });

  it('renders invocation counts', () => {
    render(<TopAgentsTable agents={mockAgents} />);

    // toLocaleString() formatting varies by environment
    expect(screen.getByText(/5[,.]?000/)).toBeInTheDocument();
    expect(screen.getByText(/3[,.]?500/)).toBeInTheDocument();
    expect(screen.getByText(/2[,.]?000/)).toBeInTheDocument();
  });

  it('renders cost values', () => {
    render(<TopAgentsTable agents={mockAgents} />);

    expect(screen.getByText('$25.50')).toBeInTheDocument();
    expect(screen.getByText('$18.75')).toBeInTheDocument();
    expect(screen.getByText('$8.25')).toBeInTheDocument();
  });

  it('renders confidence badges', () => {
    render(<TopAgentsTable agents={mockAgents} />);

    expect(screen.getByText('92%')).toBeInTheDocument();
    expect(screen.getByText('85%')).toBeInTheDocument();
    expect(screen.getByText('65%')).toBeInTheDocument();
  });

  it('renders latency values', () => {
    render(<TopAgentsTable agents={mockAgents} />);

    expect(screen.getByText('750ms')).toBeInTheDocument();
    expect(screen.getByText('850ms')).toBeInTheDocument();
    expect(screen.getByText('450ms')).toBeInTheDocument();
  });

  it('renders table headers', () => {
    render(<TopAgentsTable agents={mockAgents} />);

    expect(screen.getByText('Agent')).toBeInTheDocument();
    expect(screen.getByText('Invocations')).toBeInTheDocument();
    expect(screen.getByText('Cost')).toBeInTheDocument();
    expect(screen.getByText('Confidence')).toBeInTheDocument();
    expect(screen.getByText('Latency')).toBeInTheDocument();
  });

  it('renders ranking numbers', () => {
    render(<TopAgentsTable agents={mockAgents} />);

    expect(screen.getByText('1')).toBeInTheDocument();
    expect(screen.getByText('2')).toBeInTheDocument();
    expect(screen.getByText('3')).toBeInTheDocument();
  });

  it('renders empty state for no agents', () => {
    render(<TopAgentsTable agents={[]} />);

    expect(screen.getByText('No agents found')).toBeInTheDocument();
  });

  it('formats latency over 1 second correctly', () => {
    const agentWithLongLatency = [
      {
        ...mockAgents[0],
        avgLatencyMs: 1500,
      },
    ];

    render(<TopAgentsTable agents={agentWithLongLatency} />);

    expect(screen.getByText('1.5s')).toBeInTheDocument();
  });
});
