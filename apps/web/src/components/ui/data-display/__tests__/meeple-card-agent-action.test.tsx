/**
 * MeepleCard Agent Action Tests (Issue #4777)
 *
 * Tests for CardAgentAction footer component:
 * - "Crea Agente" button for games without agent
 * - "Chat" link for games with agent
 * - Disabled state when 0 slots available
 * - Conditional rendering based on entity type
 */

import React from 'react';
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { MeepleCard } from '../meeple-card';

// Mock useHasAvailableSlots
const mockHasAvailableSlots = vi.fn();
vi.mock('@/hooks/queries/useAgentSlots', () => ({
  useHasAvailableSlots: () => mockHasAvailableSlots(),
}));

// Mock next/link
vi.mock('next/link', () => ({
  default: ({ children, href, ...props }: React.PropsWithChildren<{ href: string }>) => (
    <a href={href} {...props}>{children}</a>
  ),
}));

// Mock next/image
vi.mock('next/image', () => ({
  default: ({ src, alt, ...props }: { src: string; alt: string }) => (
    // eslint-disable-next-line @next/next/no-img-element
    <img src={src} alt={alt} {...props} />
  ),
}));

const gameBaseProps = {
  id: 'game-123',
  entity: 'game' as const,
  title: 'Catan',
  subtitle: 'Kosmos · 1995',
};

describe('MeepleCard - Agent Action Footer (Issue #4777)', () => {
  beforeEach(() => {
    mockHasAvailableSlots.mockReturnValue({
      hasAvailableSlots: true,
      isLoading: false,
      slotsData: { total: 3, used: 1, available: 2, slots: [] },
    });
  });

  describe('Conditional rendering', () => {
    it('should render agent action when hasAgent is defined and entity is game', () => {
      render(
        <MeepleCard
          {...gameBaseProps}
          hasAgent={false}
          onCreateAgent={vi.fn()}
        />
      );

      expect(screen.getByTestId('card-agent-action')).toBeInTheDocument();
    });

    it('should NOT render agent action when hasAgent is undefined', () => {
      render(<MeepleCard {...gameBaseProps} />);

      expect(screen.queryByTestId('card-agent-action')).not.toBeInTheDocument();
    });

    it('should NOT render agent action for non-game entities', () => {
      render(
        <MeepleCard
          id="agent-1"
          entity="agent"
          title="Test Agent"
          hasAgent={false}
          onCreateAgent={vi.fn()}
        />
      );

      expect(screen.queryByTestId('card-agent-action')).not.toBeInTheDocument();
    });

    it('should NOT render for compact variant', () => {
      render(
        <MeepleCard
          {...gameBaseProps}
          variant="compact"
          hasAgent={false}
          onCreateAgent={vi.fn()}
        />
      );

      expect(screen.queryByTestId('card-agent-action')).not.toBeInTheDocument();
    });
  });

  describe('"Crea Agente" button (no agent)', () => {
    it('should render "Crea Agente" button when hasAgent is false', () => {
      render(
        <MeepleCard
          {...gameBaseProps}
          hasAgent={false}
          onCreateAgent={vi.fn()}
        />
      );

      expect(screen.getByTestId('card-agent-create-button')).toBeInTheDocument();
      expect(screen.getByText('Crea Agente')).toBeInTheDocument();
    });

    it('should call onCreateAgent when clicked', () => {
      const onCreateAgent = vi.fn();
      render(
        <MeepleCard
          {...gameBaseProps}
          hasAgent={false}
          onCreateAgent={onCreateAgent}
        />
      );

      fireEvent.click(screen.getByTestId('card-agent-create-button'));
      expect(onCreateAgent).toHaveBeenCalledOnce();
    });

    it('should be disabled when no slots available', () => {
      mockHasAvailableSlots.mockReturnValue({
        hasAvailableSlots: false,
        isLoading: false,
        slotsData: { total: 3, used: 3, available: 0, slots: [] },
      });

      render(
        <MeepleCard
          {...gameBaseProps}
          hasAgent={false}
          onCreateAgent={vi.fn()}
        />
      );

      const button = screen.getByTestId('card-agent-create-button');
      expect(button).toBeDisabled();
    });

    it('should wrap button in tooltip when disabled (0 slots)', () => {
      mockHasAvailableSlots.mockReturnValue({
        hasAvailableSlots: false,
        isLoading: false,
        slotsData: { total: 3, used: 3, available: 0, slots: [] },
      });

      render(
        <MeepleCard
          {...gameBaseProps}
          hasAgent={false}
          onCreateAgent={vi.fn()}
        />
      );

      // Button should be disabled and wrapped in Radix tooltip (data-state attr)
      const button = screen.getByTestId('card-agent-create-button');
      expect(button).toBeDisabled();
      expect(button).toHaveAttribute('data-state');
    });

    it('should be disabled when onCreateAgent is not provided', () => {
      render(
        <MeepleCard
          {...gameBaseProps}
          hasAgent={false}
        />
      );

      const button = screen.getByTestId('card-agent-create-button');
      expect(button).toBeDisabled();
    });
  });

  describe('"Chat" link (has agent)', () => {
    it('should render "Chat" link when hasAgent is true', () => {
      render(
        <MeepleCard
          {...gameBaseProps}
          hasAgent={true}
          agentId="agent-456"
        />
      );

      expect(screen.getByTestId('card-agent-chat-link')).toBeInTheDocument();
      expect(screen.getByText('Chat')).toBeInTheDocument();
    });

    it('should link to chat with agentId when provided', () => {
      render(
        <MeepleCard
          {...gameBaseProps}
          hasAgent={true}
          agentId="agent-456"
        />
      );

      const link = screen.getByTestId('card-agent-chat-link');
      expect(link).toHaveAttribute('href', '/chat/new?agentId=agent-456');
    });

    it('should fallback to gameId in chat link when no agentId', () => {
      render(
        <MeepleCard
          {...gameBaseProps}
          hasAgent={true}
        />
      );

      const link = screen.getByTestId('card-agent-chat-link');
      expect(link).toHaveAttribute('href', '/chat/new?gameId=game-123');
    });
  });

  describe('Works with all variants', () => {
    it.each(['grid', 'list', 'featured'] as const)(
      'should render agent action in %s variant',
      (variant) => {
        render(
          <MeepleCard
            {...gameBaseProps}
            variant={variant}
            hasAgent={false}
            onCreateAgent={vi.fn()}
          />
        );

        expect(screen.getByTestId('card-agent-action')).toBeInTheDocument();
      }
    );
  });
});
