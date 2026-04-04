import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';

import { YourAgents, type AgentItem } from '../YourAgents';

vi.mock('@/components/ui/data-display/meeple-card', () => ({
  MeepleCard: (props: any) => (
    <div data-testid={`meeple-card-${props.entity}`}>{props.loading ? 'loading' : props.title}</div>
  ),
}));

vi.mock('next/link', () => ({
  default: ({ href, children, ...rest }: any) => (
    <a href={href} {...rest}>
      {children}
    </a>
  ),
}));

const MOCK_AGENTS: AgentItem[] = [
  {
    id: 'agent-1',
    name: 'Tutor Catan',
    status: 'active',
    gameTitle: 'Catan',
  },
  {
    id: 'agent-2',
    name: 'Arbitro Scacchi',
    status: 'idle',
  },
];

describe('YourAgents', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders section title and "Gestisci" link', () => {
    render(<YourAgents agents={[]} />);

    expect(screen.getByTestId('your-agents')).toBeInTheDocument();
    expect(screen.getByText(/I Tuoi Agenti/)).toBeInTheDocument();

    const link = screen.getByTestId('agents-manage-link');
    expect(link).toBeInTheDocument();
    expect(link).toHaveAttribute('href', '/agents');
    expect(link).toHaveTextContent('Gestisci →');
  });

  it('renders agent cards for each agent', () => {
    render(<YourAgents agents={MOCK_AGENTS} />);

    const cards = screen.getAllByTestId('meeple-card-agent');
    // 2 agents + no skeletons; CTA is a div, not a MeepleCard
    expect(cards).toHaveLength(2);
    expect(cards[0]).toHaveTextContent('Tutor Catan');
    expect(cards[1]).toHaveTextContent('Arbitro Scacchi');
  });

  it('renders CTA card at the end', () => {
    render(<YourAgents agents={MOCK_AGENTS} onCreateAgent={vi.fn()} />);

    const cta = screen.getByTestId('create-agent-cta');
    expect(cta).toBeInTheDocument();
    expect(cta).toHaveTextContent('Crea agente');
    expect(cta).toHaveTextContent('Configura nuovo');
  });

  it('shows only CTA when no agents are provided', () => {
    render(<YourAgents agents={[]} />);

    // No agent cards
    expect(screen.queryAllByTestId('meeple-card-agent')).toHaveLength(0);

    // CTA still present
    expect(screen.getByTestId('create-agent-cta')).toBeInTheDocument();
  });

  it('renders 3 loading skeletons when loading is true', () => {
    render(<YourAgents agents={[]} loading={true} />);

    const skeletons = screen.getAllByTestId('meeple-card-agent');
    expect(skeletons).toHaveLength(3);
    skeletons.forEach(skeleton => {
      expect(skeleton).toHaveTextContent('loading');
    });

    // CTA should not be visible during loading
    expect(screen.queryByTestId('create-agent-cta')).not.toBeInTheDocument();
  });
});
