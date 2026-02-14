/**
 * MeepleCard Agent Entity Tests (Issue #4361)
 *
 * Tests for agent-specific props: agentStatus, agentModel, agentStats, capabilities.
 * Verifies integration of AgentStatusBadge, AgentModelInfo, AgentStatsDisplay
 * within the MeepleCard component.
 */

import React from 'react';
import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { MeepleCard } from '../meeple-card';

const agentBaseProps = {
  entity: 'agent' as const,
  title: 'Tutor Agent',
  subtitle: 'Board game rules assistant',
  imageUrl: 'https://example.com/agent.jpg',
};

describe('MeepleCard - Agent Entity Features (Issue #4361)', () => {
  describe('Agent Status Badge', () => {
    it('should render AgentStatusBadge when agentStatus is provided', () => {
      render(<MeepleCard {...agentBaseProps} agentStatus="active" />);

      expect(screen.getByTestId('agent-status-active')).toBeInTheDocument();
    });

    it.each(['active', 'idle', 'training', 'error'] as const)(
      'should render %s status correctly',
      (status) => {
        render(<MeepleCard {...agentBaseProps} agentStatus={status} />);

        expect(screen.getByTestId(`agent-status-${status}`)).toBeInTheDocument();
      }
    );

    it('should not render status badge when agentStatus is not provided', () => {
      render(<MeepleCard {...agentBaseProps} />);

      expect(screen.queryByTestId('agent-status-active')).not.toBeInTheDocument();
      expect(screen.queryByTestId('agent-status-idle')).not.toBeInTheDocument();
    });
  });

  describe('Agent Model Info', () => {
    it('should render AgentModelInfo when agentModel is provided', () => {
      render(
        <MeepleCard
          {...agentBaseProps}
          agentModel={{ modelName: 'GPT-4o-mini' }}
        />
      );

      expect(screen.getByTestId('agent-model-info')).toBeInTheDocument();
      expect(screen.getByText('GPT-4o-mini')).toBeInTheDocument();
    });

    it('should render model with parameters', () => {
      render(
        <MeepleCard
          {...agentBaseProps}
          agentModel={{
            modelName: 'Claude 3.5 Sonnet',
            parameters: { temperature: 0.7, maxTokens: 4096 },
          }}
        />
      );

      expect(screen.getByText('Claude 3.5 Sonnet')).toBeInTheDocument();
    });

    it('should not render model info when agentModel is not provided', () => {
      render(<MeepleCard {...agentBaseProps} />);

      expect(screen.queryByTestId('agent-model-info')).not.toBeInTheDocument();
    });
  });

  describe('Capabilities', () => {
    it('should render capability chips when capabilities are provided', () => {
      render(
        <MeepleCard
          {...agentBaseProps}
          capabilities={['rag', 'vision', 'code']}
        />
      );

      expect(screen.getByText('rag')).toBeInTheDocument();
      expect(screen.getByText('vision')).toBeInTheDocument();
      expect(screen.getByText('code')).toBeInTheDocument();
    });

    it('should not render capabilities section when empty array', () => {
      render(<MeepleCard {...agentBaseProps} capabilities={[]} />);

      const section = screen.queryByTestId('agent-info-section');
      // Section may exist if other agent props are set, but no capability chips
      if (section) {
        expect(section.querySelectorAll('.bg-amber-100')).toHaveLength(0);
      }
    });

    it('should not render capabilities when not provided', () => {
      render(<MeepleCard {...agentBaseProps} />);

      // No agent info section at all when no agent props
      expect(screen.queryByTestId('agent-info-section')).not.toBeInTheDocument();
    });
  });

  describe('Agent Stats', () => {
    it('should render AgentStatsDisplay when agentStats are provided', () => {
      render(
        <MeepleCard
          {...agentBaseProps}
          agentStats={{ invocationCount: 1500, avgResponseTimeMs: 250 }}
        />
      );

      expect(screen.getByTestId('agent-info-section')).toBeInTheDocument();
      // Stats display should show formatted count
      expect(screen.getByText('1.5K')).toBeInTheDocument();
    });

    it('should not render stats when not provided', () => {
      render(<MeepleCard {...agentBaseProps} />);

      expect(screen.queryByTestId('agent-info-section')).not.toBeInTheDocument();
    });
  });

  describe('Combined Agent Features', () => {
    it('should render all agent features together', () => {
      render(
        <MeepleCard
          {...agentBaseProps}
          agentStatus="active"
          agentModel={{ modelName: 'GPT-4o' }}
          capabilities={['rag', 'code']}
          agentStats={{ invocationCount: 342 }}
        />
      );

      const section = screen.getByTestId('agent-info-section');
      expect(section).toBeInTheDocument();

      // Status
      expect(screen.getByTestId('agent-status-active')).toBeInTheDocument();
      // Model
      expect(screen.getByText('GPT-4o')).toBeInTheDocument();
      // Capabilities
      expect(screen.getByText('rag')).toBeInTheDocument();
      expect(screen.getByText('code')).toBeInTheDocument();
    });
  });

  describe('Variant-specific behavior', () => {
    it('should not render agent info in compact variant', () => {
      render(
        <MeepleCard
          {...agentBaseProps}
          variant="compact"
          agentStatus="active"
          agentModel={{ modelName: 'GPT-4o' }}
        />
      );

      expect(screen.queryByTestId('agent-info-section')).not.toBeInTheDocument();
    });

    it('should render agent info in grid variant', () => {
      render(
        <MeepleCard
          {...agentBaseProps}
          variant="grid"
          agentStatus="idle"
        />
      );

      expect(screen.getByTestId('agent-info-section')).toBeInTheDocument();
    });

    it('should render agent info in list variant', () => {
      render(
        <MeepleCard
          {...agentBaseProps}
          variant="list"
          agentStatus="training"
        />
      );

      expect(screen.getByTestId('agent-info-section')).toBeInTheDocument();
    });
  });

  describe('Non-agent entities', () => {
    it('should not render agent info for game entity', () => {
      render(
        <MeepleCard
          entity="game"
          title="Catan"
          agentStatus="active"
          agentModel={{ modelName: 'GPT-4o' }}
        />
      );

      expect(screen.queryByTestId('agent-info-section')).not.toBeInTheDocument();
    });

    it('should not render agent info for player entity', () => {
      render(
        <MeepleCard
          entity="player"
          title="Player 1"
          agentStatus="idle"
        />
      );

      expect(screen.queryByTestId('agent-info-section')).not.toBeInTheDocument();
    });
  });
});
