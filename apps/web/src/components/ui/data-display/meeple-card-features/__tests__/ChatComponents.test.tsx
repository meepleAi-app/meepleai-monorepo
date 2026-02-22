/**
 * Chat Components Test Suite
 * Issue #4400 - ChatSession-Specific Metadata & Status Display
 *
 * Tests for ChatStatusBadge, ChatAgentInfo, ChatStatsDisplay, ChatGameContext, ChatUnreadBadge
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { CHAT_TEST_IDS } from '@/lib/test-ids';
import { ChatAgentInfo } from '../ChatAgentInfo';
import { ChatGameContext } from '../ChatGameContext';
import { ChatStatsDisplay, formatDuration } from '../ChatStatsDisplay';
import { ChatStatusBadge } from '../ChatStatusBadge';
import { ChatUnreadBadge } from '../ChatUnreadBadge';

import type { ChatStats } from '../ChatStatsDisplay';

// ============================================================================
// ChatStatusBadge
// ============================================================================

describe('ChatStatusBadge', () => {
  describe('Status States', () => {
    it('renders active status with blue styling', () => {
      render(<ChatStatusBadge status="active" />);
      const badge = screen.getByTestId(CHAT_TEST_IDS.statusBadge('active'));
      expect(badge).toBeInTheDocument();
      expect(badge).toHaveClass('bg-blue-50');
      expect(screen.getByText('Active')).toBeInTheDocument();
    });

    it('renders waiting status with yellow styling', () => {
      render(<ChatStatusBadge status="waiting" />);
      const badge = screen.getByTestId(CHAT_TEST_IDS.statusBadge('waiting'));
      expect(badge).toHaveClass('bg-yellow-50');
      expect(screen.getByText('Waiting')).toBeInTheDocument();
    });

    it('renders archived status with gray styling', () => {
      render(<ChatStatusBadge status="archived" />);
      const badge = screen.getByTestId(CHAT_TEST_IDS.statusBadge('archived'));
      expect(badge).toHaveClass('bg-gray-50');
      expect(screen.getByText('Archived')).toBeInTheDocument();
    });

    it('renders closed status with slate styling', () => {
      render(<ChatStatusBadge status="closed" />);
      const badge = screen.getByTestId(CHAT_TEST_IDS.statusBadge('closed'));
      expect(badge).toHaveClass('bg-slate-50');
      expect(screen.getByText('Closed')).toBeInTheDocument();
    });
  });

  describe('Pulsating Animation', () => {
    it('active status has pulsating dot', () => {
      const { container } = render(<ChatStatusBadge status="active" />);
      const pulsatingDots = container.querySelectorAll('.animate-pulse, .animate-ping');
      expect(pulsatingDots.length).toBeGreaterThan(0);
    });

    it('waiting status has pulsating dot', () => {
      const { container } = render(<ChatStatusBadge status="waiting" />);
      const pulsatingDots = container.querySelectorAll('.animate-pulse, .animate-ping');
      expect(pulsatingDots.length).toBeGreaterThan(0);
    });

    it('archived status has no pulsating animation', () => {
      const { container } = render(<ChatStatusBadge status="archived" />);
      const pulsatingDots = container.querySelectorAll('.animate-pulse, .animate-ping');
      expect(pulsatingDots.length).toBe(0);
    });

    it('closed status has no pulsating animation', () => {
      const { container } = render(<ChatStatusBadge status="closed" />);
      const pulsatingDots = container.querySelectorAll('.animate-pulse, .animate-ping');
      expect(pulsatingDots.length).toBe(0);
    });
  });

  describe('Label Display', () => {
    it('shows label by default', () => {
      render(<ChatStatusBadge status="active" showLabel={true} />);
      expect(screen.getByText('Active')).toBeInTheDocument();
    });

    it('hides label when showLabel=false', () => {
      render(<ChatStatusBadge status="active" showLabel={false} />);
      expect(screen.queryByText('Active')).not.toBeInTheDocument();
    });
  });

  describe('Accessibility', () => {
    it('has descriptive aria-label', () => {
      render(<ChatStatusBadge status="active" />);
      const badge = screen.getByTestId(CHAT_TEST_IDS.statusBadge('active'));
      expect(badge).toHaveAttribute('aria-label', 'Chat status: Active');
    });
  });
});

// ============================================================================
// ChatAgentInfo
// ============================================================================

describe('ChatAgentInfo', () => {
  const mockAgent = { name: 'Tutor Bot', modelName: 'GPT-4o-mini' };

  describe('Rendering', () => {
    it('renders with agent name and model', () => {
      render(<ChatAgentInfo agent={mockAgent} />);
      expect(screen.getByTestId(CHAT_TEST_IDS.agentInfo)).toBeInTheDocument();
      expect(screen.getByText('Tutor Bot')).toBeInTheDocument();
      expect(screen.getByText('GPT-4o-mini')).toBeInTheDocument();
    });

    it('renders bot icon', () => {
      const { container } = render(<ChatAgentInfo agent={mockAgent} />);
      const icon = container.querySelector('svg');
      expect(icon).toBeInTheDocument();
    });
  });

  describe('Model Badge Variants', () => {
    it('uses default variant for GPT-4 models', () => {
      const { container } = render(<ChatAgentInfo agent={{ name: 'Bot', modelName: 'GPT-4o' }} />);
      const badge = container.querySelector('[class*="bg-primary"]');
      expect(badge).toBeInTheDocument();
    });

    it('uses default variant for Claude models', () => {
      const { container } = render(<ChatAgentInfo agent={{ name: 'Bot', modelName: 'Claude 3.5 Sonnet' }} />);
      const badge = container.querySelector('[class*="bg-primary"]');
      expect(badge).toBeInTheDocument();
    });

    it('uses secondary variant for Gemini models', () => {
      const { container } = render(<ChatAgentInfo agent={{ name: 'Bot', modelName: 'Gemini Pro' }} />);
      const badge = container.querySelector('[class*="bg-secondary"]');
      expect(badge).toBeInTheDocument();
    });

    it('uses outline variant for unknown models', () => {
      const { container } = render(<ChatAgentInfo agent={{ name: 'Bot', modelName: 'Custom LLM' }} />);
      const badge = container.querySelector('[class*="border"]');
      expect(badge).toBeInTheDocument();
    });
  });
});

// ============================================================================
// ChatStatsDisplay
// ============================================================================

describe('ChatStatsDisplay', () => {
  const mockStats: ChatStats = {
    messageCount: 42,
    lastMessageAt: new Date(Date.now() - 2 * 60 * 60 * 1000), // 2 hours ago
    durationMinutes: 83,
  };

  describe('Rendering', () => {
    it('renders with all stats', () => {
      render(<ChatStatsDisplay stats={mockStats} />);
      expect(screen.getByTestId(CHAT_TEST_IDS.statsDisplay)).toBeInTheDocument();
      expect(screen.getByTestId(CHAT_TEST_IDS.messageCount)).toBeInTheDocument();
      expect(screen.getByTestId(CHAT_TEST_IDS.lastMessageAt)).toBeInTheDocument();
      expect(screen.getByTestId(CHAT_TEST_IDS.duration)).toBeInTheDocument();
    });

    it('renders without optional stats', () => {
      const minimalStats: ChatStats = { messageCount: 10 };
      render(<ChatStatsDisplay stats={minimalStats} />);
      expect(screen.getByTestId(CHAT_TEST_IDS.messageCount)).toBeInTheDocument();
      expect(screen.queryByTestId(CHAT_TEST_IDS.lastMessageAt)).not.toBeInTheDocument();
      expect(screen.queryByTestId(CHAT_TEST_IDS.duration)).not.toBeInTheDocument();
    });
  });

  describe('Message Count Formatting', () => {
    it('displays small numbers as-is', () => {
      render(<ChatStatsDisplay stats={{ messageCount: 42 }} />);
      expect(screen.getByText('42')).toBeInTheDocument();
    });

    it('displays formatted count for thousands', () => {
      render(<ChatStatsDisplay stats={{ messageCount: 1500 }} />);
      expect(screen.getByText('1.5K')).toBeInTheDocument();
    });
  });

  describe('Duration Formatting', () => {
    it('formats minutes only', () => {
      expect(formatDuration(45)).toBe('45m');
    });

    it('formats hours and minutes', () => {
      expect(formatDuration(83)).toBe('1h 23m');
    });

    it('formats exact hours', () => {
      expect(formatDuration(120)).toBe('2h');
    });

    it('formats single minute', () => {
      expect(formatDuration(1)).toBe('1m');
    });

    it('displays formatted duration in component', () => {
      render(<ChatStatsDisplay stats={{ messageCount: 1, durationMinutes: 83 }} />);
      expect(screen.getByText('1h 23m')).toBeInTheDocument();
    });
  });

  describe('Layout', () => {
    it('horizontal layout by default', () => {
      render(<ChatStatsDisplay stats={mockStats} />);
      const display = screen.getByTestId(CHAT_TEST_IDS.statsDisplay);
      expect(display).toHaveClass('flex-row');
    });

    it('vertical layout when specified', () => {
      render(<ChatStatsDisplay stats={mockStats} layout="vertical" />);
      const display = screen.getByTestId(CHAT_TEST_IDS.statsDisplay);
      expect(display).toHaveClass('flex-col');
    });
  });
});

// ============================================================================
// ChatGameContext
// ============================================================================

describe('ChatGameContext', () => {
  describe('Rendering', () => {
    it('renders game name with orange dot', () => {
      render(<ChatGameContext game={{ name: 'Catan' }} />);
      const chip = screen.getByTestId(CHAT_TEST_IDS.gameContext);
      expect(chip).toBeInTheDocument();
      expect(screen.getByText('Catan')).toBeInTheDocument();
    });

    it('renders orange dot with correct color', () => {
      const { container } = render(<ChatGameContext game={{ name: 'Catan' }} />);
      const dot = container.querySelector('span[style]');
      expect(dot).toHaveStyle({ backgroundColor: 'hsl(25 95% 45%)' });
    });
  });

  describe('Link behavior', () => {
    it('renders as span when no game ID', () => {
      render(<ChatGameContext game={{ name: 'Catan' }} />);
      const chip = screen.getByTestId(CHAT_TEST_IDS.gameContext);
      expect(chip.tagName).toBe('SPAN');
    });

    it('renders as link when game ID is provided', () => {
      render(<ChatGameContext game={{ name: 'Catan', id: 'abc-123' }} />);
      const chip = screen.getByTestId(CHAT_TEST_IDS.gameContext);
      expect(chip.tagName).toBe('A');
      expect(chip).toHaveAttribute('href', '/games/abc-123');
    });
  });
});

// ============================================================================
// ChatUnreadBadge
// ============================================================================

describe('ChatUnreadBadge', () => {
  describe('Rendering', () => {
    it('renders when count > 0', () => {
      render(<ChatUnreadBadge count={5} />);
      const badge = screen.getByTestId(CHAT_TEST_IDS.unreadBadge);
      expect(badge).toBeInTheDocument();
      expect(screen.getByText('5')).toBeInTheDocument();
    });

    it('does not render when count is 0', () => {
      render(<ChatUnreadBadge count={0} />);
      expect(screen.queryByTestId(CHAT_TEST_IDS.unreadBadge)).not.toBeInTheDocument();
    });

    it('does not render when count is negative', () => {
      render(<ChatUnreadBadge count={-1} />);
      expect(screen.queryByTestId(CHAT_TEST_IDS.unreadBadge)).not.toBeInTheDocument();
    });
  });

  describe('Count Display', () => {
    it('shows exact count for small numbers', () => {
      render(<ChatUnreadBadge count={3} />);
      expect(screen.getByText('3')).toBeInTheDocument();
    });

    it('shows exact count for 99', () => {
      render(<ChatUnreadBadge count={99} />);
      expect(screen.getByText('99')).toBeInTheDocument();
    });

    it('shows 99+ for 100 or more', () => {
      render(<ChatUnreadBadge count={100} />);
      expect(screen.getByText('99+')).toBeInTheDocument();
    });

    it('shows 99+ for large numbers', () => {
      render(<ChatUnreadBadge count={500} />);
      expect(screen.getByText('99+')).toBeInTheDocument();
    });
  });

  describe('Accessibility', () => {
    it('has descriptive aria-label', () => {
      render(<ChatUnreadBadge count={7} />);
      const badge = screen.getByTestId(CHAT_TEST_IDS.unreadBadge);
      expect(badge).toHaveAttribute('aria-label', '7 unread messages');
    });

    it('uses actual count in aria-label even for 99+ display', () => {
      render(<ChatUnreadBadge count={150} />);
      const badge = screen.getByTestId(CHAT_TEST_IDS.unreadBadge);
      expect(badge).toHaveAttribute('aria-label', '150 unread messages');
    });
  });
});
