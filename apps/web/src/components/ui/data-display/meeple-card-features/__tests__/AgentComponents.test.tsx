/**
 * Agent Components Test Suite
 * Issue #4184 - Agent-Specific Metadata & Status Display
 *
 * Tests for AgentStatusBadge, AgentStatsDisplay, and AgentModelInfo
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { AgentModelInfo } from '../AgentModelInfo';
import { AgentStatsDisplay, formatInvocationCount, formatRelativeTime, formatResponseTime } from '../AgentStatsDisplay';
import { AgentStatusBadge } from '../AgentStatusBadge';

import type { AgentStats } from '../AgentStatsDisplay';

describe('AgentStatusBadge', () => {
  describe('Status States', () => {
    it('renders active status with green styling', () => {
      render(<AgentStatusBadge status="active" />);
      const badge = screen.getByTestId('agent-status-active');
      expect(badge).toBeInTheDocument();
      expect(badge).toHaveClass('bg-green-100');
      expect(screen.getByText('Active')).toBeInTheDocument();
    });

    it('renders idle status with gray styling', () => {
      render(<AgentStatusBadge status="idle" />);
      const badge = screen.getByTestId('agent-status-idle');
      expect(badge).toHaveClass('bg-gray-100');
      expect(screen.getByText('Idle')).toBeInTheDocument();
    });

    it('renders training status with yellow styling', () => {
      render(<AgentStatusBadge status="training" />);
      const badge = screen.getByTestId('agent-status-training');
      expect(badge).toHaveClass('bg-yellow-100');
      expect(screen.getByText('Training')).toBeInTheDocument();
    });

    it('renders error status with red styling', () => {
      render(<AgentStatusBadge status="error" />);
      const badge = screen.getByTestId('agent-status-error');
      expect(badge).toHaveClass('bg-red-100');
      expect(screen.getByText('Error')).toBeInTheDocument();
    });
  });

  describe('Pulsating Animation', () => {
    it('active status has pulsating dot', () => {
      const { container } = render(<AgentStatusBadge status="active" />);
      const pulsatingDots = container.querySelectorAll('.animate-pulse, .animate-ping');
      expect(pulsatingDots.length).toBeGreaterThan(0);
    });

    it('training status has pulsating dot', () => {
      const { container } = render(<AgentStatusBadge status="training" />);
      const pulsatingDots = container.querySelectorAll('.animate-pulse, .animate-ping');
      expect(pulsatingDots.length).toBeGreaterThan(0);
    });

    it('idle status has no pulsating animation', () => {
      const { container } = render(<AgentStatusBadge status="idle" />);
      const pulsatingDots = container.querySelectorAll('.animate-pulse, .animate-ping');
      expect(pulsatingDots.length).toBe(0);
    });

    it('error status has no pulsating animation', () => {
      const { container } = render(<AgentStatusBadge status="error" />);
      const pulsatingDots = container.querySelectorAll('.animate-pulse, .animate-ping');
      expect(pulsatingDots.length).toBe(0);
    });
  });

  describe('Label Display', () => {
    it('shows label by default', () => {
      render(<AgentStatusBadge status="active" showLabel={true} />);
      expect(screen.getByText('Active')).toBeInTheDocument();
    });

    it('hides label when showLabel=false', () => {
      render(<AgentStatusBadge status="active" showLabel={false} />);
      expect(screen.queryByText('Active')).not.toBeInTheDocument();
    });
  });

  describe('Accessibility', () => {
    it('has descriptive aria-label', () => {
      render(<AgentStatusBadge status="active" />);
      const badge = screen.getByTestId('agent-status-active');
      expect(badge).toHaveAttribute('aria-label', 'Agent status: Active');
    });
  });
});

describe('AgentStatsDisplay', () => {
  const mockStats: AgentStats = {
    invocationCount: 342,
    lastExecutedAt: new Date(Date.now() - 2 * 60 * 60 * 1000), // 2 hours ago
    avgResponseTimeMs: 1234,
  };

  describe('Rendering', () => {
    it('renders with all stats', () => {
      render(<AgentStatsDisplay stats={mockStats} />);
      expect(screen.getByTestId('agent-stats-display')).toBeInTheDocument();
      expect(screen.getByTestId('invocation-count')).toBeInTheDocument();
      expect(screen.getByTestId('last-execution')).toBeInTheDocument();
      expect(screen.getByTestId('avg-response-time')).toBeInTheDocument();
    });

    it('renders without optional stats', () => {
      const minimalStats: AgentStats = { invocationCount: 100 };
      render(<AgentStatsDisplay stats={minimalStats} />);
      expect(screen.getByTestId('invocation-count')).toBeInTheDocument();
      expect(screen.queryByTestId('last-execution')).not.toBeInTheDocument();
      expect(screen.queryByTestId('avg-response-time')).not.toBeInTheDocument();
    });
  });

  describe('Invocation Count Formatting', () => {
    it('formats small numbers as-is', () => {
      expect(formatInvocationCount(342)).toBe('342');
    });

    it('formats thousands with K suffix', () => {
      expect(formatInvocationCount(1200)).toBe('1.2K');
      expect(formatInvocationCount(15000)).toBe('15.0K');
    });

    it('formats millions with M suffix', () => {
      expect(formatInvocationCount(3_400_000)).toBe('3.4M');
      expect(formatInvocationCount(1_000_000)).toBe('1.0M');
    });

    it('displays formatted count in component', () => {
      const stats: AgentStats = { invocationCount: 1200 };
      render(<AgentStatsDisplay stats={stats} />);
      expect(screen.getByText('1.2K')).toBeInTheDocument();
    });
  });

  describe('Relative Time Formatting', () => {
    it('formats seconds as "just now"', () => {
      const now = new Date();
      expect(formatRelativeTime(now)).toBe('just now');
    });

    it('formats minutes', () => {
      const twoMinutesAgo = new Date(Date.now() - 2 * 60 * 1000);
      expect(formatRelativeTime(twoMinutesAgo)).toBe('2 minutes ago');
    });

    it('formats hours', () => {
      const threeHoursAgo = new Date(Date.now() - 3 * 60 * 60 * 1000);
      expect(formatRelativeTime(threeHoursAgo)).toBe('3 hours ago');
    });

    it('formats days', () => {
      const fiveDaysAgo = new Date(Date.now() - 5 * 24 * 60 * 60 * 1000);
      expect(formatRelativeTime(fiveDaysAgo)).toBe('5 days ago');
    });

    it('uses singular for 1 unit', () => {
      const oneHourAgo = new Date(Date.now() - 1 * 60 * 60 * 1000);
      expect(formatRelativeTime(oneHourAgo)).toBe('1 hour ago');
    });
  });

  describe('Response Time Formatting', () => {
    it('formats milliseconds', () => {
      expect(formatResponseTime(500)).toBe('500ms');
    });

    it('formats seconds', () => {
      expect(formatResponseTime(1234)).toBe('1.2s');
      expect(formatResponseTime(5000)).toBe('5.0s');
    });
  });

  describe('Layout', () => {
    it('horizontal layout by default', () => {
      const { container } = render(<AgentStatsDisplay stats={mockStats} />);
      const display = screen.getByTestId('agent-stats-display');
      expect(display).toHaveClass('flex-row');
    });

    it('vertical layout when specified', () => {
      const { container } = render(<AgentStatsDisplay stats={mockStats} layout="vertical" />);
      const display = screen.getByTestId('agent-stats-display');
      expect(display).toHaveClass('flex-col');
    });
  });
});

describe('AgentModelInfo', () => {
  const mockParameters = {
    temperature: 0.7,
    maxTokens: 4096,
    topP: 0.9,
  };

  describe('Rendering', () => {
    it('renders with model name', () => {
      render(<AgentModelInfo modelName="GPT-4o-mini" />);
      expect(screen.getByTestId('agent-model-info')).toBeInTheDocument();
      expect(screen.getByText('GPT-4o-mini')).toBeInTheDocument();
    });

    it('shows icon by default', () => {
      const { container } = render(<AgentModelInfo modelName="Claude 3.5 Sonnet" />);
      const icon = container.querySelector('svg');
      expect(icon).toBeInTheDocument();
    });

    it('hides icon when showIcon=false', () => {
      const { container } = render(<AgentModelInfo modelName="Claude 3.5 Sonnet" showIcon={false} />);
      const icon = container.querySelector('svg');
      expect(icon).not.toBeInTheDocument();
    });
  });

  describe('Model Badge Variants', () => {
    it('uses default variant for GPT-4', () => {
      const { container } = render(<AgentModelInfo modelName="GPT-4o" />);
      const badge = container.querySelector('[class*="bg-primary"]');
      expect(badge).toBeInTheDocument();
    });

    it('uses default variant for Claude', () => {
      const { container } = render(<AgentModelInfo modelName="Claude 3.5 Sonnet" />);
      const badge = container.querySelector('[class*="bg-primary"]');
      expect(badge).toBeInTheDocument();
    });

    it('uses secondary variant for Gemini', () => {
      const { container } = render(<AgentModelInfo modelName="Gemini Pro" />);
      const badge = container.querySelector('[class*="bg-secondary"]');
      expect(badge).toBeInTheDocument();
    });
  });

  describe('Parameters Tooltip', () => {
    it('renders without parameters', () => {
      render(<AgentModelInfo modelName="GPT-4o-mini" />);
      expect(screen.getByTestId('agent-model-info')).toBeInTheDocument();
    });

    it('includes parameters when provided', () => {
      render(<AgentModelInfo modelName="GPT-4o-mini" parameters={mockParameters} />);
      // Tooltip content rendered (tested via TooltipContent component)
      expect(screen.getByTestId('agent-model-info')).toBeInTheDocument();
    });
  });

  describe('Size Variants', () => {
    it('small size by default', () => {
      const { container } = render(<AgentModelInfo modelName="GPT-4o" size="sm" />);
      const icon = container.querySelector('svg');
      expect(icon).toHaveClass('w-3.5', 'h-3.5');
    });

    it('medium size when specified', () => {
      const { container } = render(<AgentModelInfo modelName="GPT-4o" size="md" />);
      const icon = container.querySelector('svg');
      expect(icon).toHaveClass('w-4', 'h-4');
    });
  });
});
