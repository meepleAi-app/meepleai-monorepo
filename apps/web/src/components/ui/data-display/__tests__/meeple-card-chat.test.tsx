/**
 * MeepleCard ChatSession Entity Tests (Issue #4400)
 *
 * Tests for chatSession-specific props: chatStatus, chatAgent, chatGame, chatStats, chatPreview, unreadCount.
 * Verifies integration of ChatStatusBadge, ChatAgentInfo, ChatStatsDisplay,
 * ChatGameContext, ChatUnreadBadge within the MeepleCard component.
 */

import React from 'react';
import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { MeepleCard } from '../meeple-card';

const chatBaseProps = {
  entity: 'chatSession' as const,
  title: 'Catan Rules Help',
  subtitle: 'Chat about Catan rules',
  imageUrl: 'https://example.com/chat.jpg',
};

describe('MeepleCard - ChatSession Entity Features (Issue #4400)', () => {
  describe('Chat Status Badge', () => {
    it('should render ChatStatusBadge when chatStatus is provided', () => {
      render(<MeepleCard {...chatBaseProps} chatStatus="active" />);
      expect(screen.getByTestId('chat-status-active')).toBeInTheDocument();
    });

    it.each(['active', 'waiting', 'archived', 'closed'] as const)(
      'should render %s status correctly',
      (status) => {
        render(<MeepleCard {...chatBaseProps} chatStatus={status} />);
        expect(screen.getByTestId(`chat-status-${status}`)).toBeInTheDocument();
      }
    );

    it('should not render status badge when chatStatus is not provided', () => {
      render(<MeepleCard {...chatBaseProps} />);
      expect(screen.queryByTestId('chat-status-active')).not.toBeInTheDocument();
      expect(screen.queryByTestId('chat-status-waiting')).not.toBeInTheDocument();
    });
  });

  describe('Chat Agent Info', () => {
    it('should render ChatAgentInfo when chatAgent is provided', () => {
      render(
        <MeepleCard
          {...chatBaseProps}
          variant="featured"
          chatAgent={{ name: 'Rules Bot', modelName: 'GPT-4o-mini' }}
        />
      );
      expect(screen.getByTestId('chat-agent-info')).toBeInTheDocument();
      expect(screen.getByText('Rules Bot')).toBeInTheDocument();
      expect(screen.getByText('GPT-4o-mini')).toBeInTheDocument();
    });

    it('should not render agent info when chatAgent is not provided', () => {
      render(<MeepleCard {...chatBaseProps} />);
      expect(screen.queryByTestId('chat-agent-info')).not.toBeInTheDocument();
    });
  });

  describe('Chat Game Context', () => {
    it('should render ChatGameContext when chatGame is provided', () => {
      render(
        <MeepleCard
          {...chatBaseProps}
          variant="featured"
          chatGame={{ name: 'Catan', id: 'game-123' }}
        />
      );
      expect(screen.getByTestId('chat-game-context')).toBeInTheDocument();
      expect(screen.getByText('Catan')).toBeInTheDocument();
    });

    it('should render game context as link when game ID is provided', () => {
      render(
        <MeepleCard
          {...chatBaseProps}
          variant="featured"
          chatGame={{ name: 'Catan', id: 'game-123' }}
        />
      );
      const chip = screen.getByTestId('chat-game-context');
      expect(chip.tagName).toBe('A');
      expect(chip).toHaveAttribute('href', '/games/game-123');
    });

    it('should not render game context when chatGame is not provided', () => {
      render(<MeepleCard {...chatBaseProps} />);
      expect(screen.queryByTestId('chat-game-context')).not.toBeInTheDocument();
    });
  });

  describe('Chat Stats', () => {
    it('should render ChatStatsDisplay when chatStats are provided', () => {
      render(
        <MeepleCard
          {...chatBaseProps}
          chatStats={{ messageCount: 42, durationMinutes: 83 }}
        />
      );
      expect(screen.getByTestId('chat-info-section')).toBeInTheDocument();
      expect(screen.getByText('42')).toBeInTheDocument();
      expect(screen.getByText('1h 23m')).toBeInTheDocument();
    });

    it('should not render stats when not provided', () => {
      render(<MeepleCard {...chatBaseProps} />);
      expect(screen.queryByTestId('chat-stats-display')).not.toBeInTheDocument();
    });
  });

  describe('Chat Message Preview', () => {
    it('should render chat preview when provided with chatStatus', () => {
      render(
        <MeepleCard
          {...chatBaseProps}
          variant="featured"
          chatStatus="active"
          chatPreview={{ lastMessage: 'How do I trade resources?', sender: 'user' }}
        />
      );
      expect(screen.getByTestId('chat-preview')).toBeInTheDocument();
      expect(screen.getByText('You:')).toBeInTheDocument();
      expect(screen.getByText('How do I trade resources?')).toBeInTheDocument();
    });

    it('should show Agent prefix for agent messages', () => {
      render(
        <MeepleCard
          {...chatBaseProps}
          variant="featured"
          chatStatus="active"
          chatPreview={{ lastMessage: 'You can trade during your turn.', sender: 'agent' }}
        />
      );
      expect(screen.getByText('Agent:')).toBeInTheDocument();
    });
  });

  describe('Unread Badge', () => {
    it('should render unread badge when unreadCount > 0', () => {
      render(
        <MeepleCard
          {...chatBaseProps}
          unreadCount={5}
        />
      );
      expect(screen.getByTestId('chat-unread-badge')).toBeInTheDocument();
      expect(screen.getByText('5')).toBeInTheDocument();
    });

    it('should show 99+ for large counts', () => {
      render(
        <MeepleCard
          {...chatBaseProps}
          unreadCount={150}
        />
      );
      expect(screen.getByText('99+')).toBeInTheDocument();
    });

    it('should not render unread badge when count is 0', () => {
      render(
        <MeepleCard
          {...chatBaseProps}
          unreadCount={0}
        />
      );
      expect(screen.queryByTestId('chat-unread-badge')).not.toBeInTheDocument();
    });

    it('should not render unread badge when not provided', () => {
      render(<MeepleCard {...chatBaseProps} />);
      expect(screen.queryByTestId('chat-unread-badge')).not.toBeInTheDocument();
    });
  });

  describe('Combined Chat Features', () => {
    it('should render all chat features in featured variant', () => {
      render(
        <MeepleCard
          {...chatBaseProps}
          variant="featured"
          chatStatus="active"
          chatAgent={{ name: 'Rules Bot', modelName: 'GPT-4o' }}
          chatGame={{ name: 'Catan' }}
          chatStats={{ messageCount: 42 }}
          chatPreview={{ lastMessage: 'Hello!', sender: 'user' }}
          unreadCount={3}
        />
      );

      const section = screen.getByTestId('chat-info-section');
      expect(section).toBeInTheDocument();

      // Status
      expect(screen.getByTestId('chat-status-active')).toBeInTheDocument();
      // Agent (visible in featured)
      expect(screen.getByText('Rules Bot')).toBeInTheDocument();
      expect(screen.getByText('GPT-4o')).toBeInTheDocument();
      // Game (visible in featured)
      expect(screen.getByText('Catan')).toBeInTheDocument();
      // Stats
      expect(screen.getByText('42')).toBeInTheDocument();
      // Preview (visible in featured)
      expect(screen.getByText('Hello!')).toBeInTheDocument();
      // Unread
      expect(screen.getByTestId('chat-unread-badge')).toBeInTheDocument();
    });

    it('should render reduced info in grid variant (status + stats only)', () => {
      render(
        <MeepleCard
          {...chatBaseProps}
          variant="grid"
          chatStatus="active"
          chatAgent={{ name: 'Rules Bot', modelName: 'GPT-4o' }}
          chatGame={{ name: 'Catan' }}
          chatStats={{ messageCount: 42 }}
          chatPreview={{ lastMessage: 'Hello!', sender: 'user' }}
          unreadCount={3}
        />
      );

      const section = screen.getByTestId('chat-info-section');
      expect(section).toBeInTheDocument();

      // Status (visible in grid)
      expect(screen.getByTestId('chat-status-active')).toBeInTheDocument();
      // Stats (visible in grid)
      expect(screen.getByText('42')).toBeInTheDocument();
      // Agent hidden in grid
      expect(screen.queryByText('Rules Bot')).not.toBeInTheDocument();
      // Game hidden in grid
      expect(screen.queryByText('Catan')).not.toBeInTheDocument();
      // Preview hidden in grid
      expect(screen.queryByText('Hello!')).not.toBeInTheDocument();
      // Unread
      expect(screen.getByTestId('chat-unread-badge')).toBeInTheDocument();
    });
  });

  describe('Variant-specific behavior', () => {
    it('should not render chat info in compact variant', () => {
      render(
        <MeepleCard
          {...chatBaseProps}
          variant="compact"
          chatStatus="active"
          chatAgent={{ name: 'Bot', modelName: 'GPT-4o' }}
        />
      );
      expect(screen.queryByTestId('chat-info-section')).not.toBeInTheDocument();
    });

    it('should render chat info in grid variant', () => {
      render(
        <MeepleCard
          {...chatBaseProps}
          variant="grid"
          chatStatus="active"
        />
      );
      expect(screen.getByTestId('chat-info-section')).toBeInTheDocument();
    });

    it('should render chat info in list variant', () => {
      render(
        <MeepleCard
          {...chatBaseProps}
          variant="list"
          chatStatus="waiting"
        />
      );
      expect(screen.getByTestId('chat-info-section')).toBeInTheDocument();
    });
  });

  describe('Non-chatSession entities', () => {
    it('should not render chat info for game entity', () => {
      render(
        <MeepleCard
          entity="game"
          title="Catan"
          chatStatus="active"
          chatAgent={{ name: 'Bot', modelName: 'GPT-4o' }}
        />
      );
      expect(screen.queryByTestId('chat-info-section')).not.toBeInTheDocument();
    });

    it('should not render chat info for agent entity', () => {
      render(
        <MeepleCard
          entity="agent"
          title="Agent"
          chatStatus="active"
        />
      );
      expect(screen.queryByTestId('chat-info-section')).not.toBeInTheDocument();
    });

    it('should not render unread badge for non-chatSession entity', () => {
      render(
        <MeepleCard
          entity="game"
          title="Catan"
          unreadCount={5}
        />
      );
      expect(screen.queryByTestId('chat-unread-badge')).not.toBeInTheDocument();
    });
  });
});
