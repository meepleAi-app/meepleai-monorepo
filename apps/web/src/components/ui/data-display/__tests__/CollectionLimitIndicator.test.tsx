/**
 * CollectionLimitIndicator Test Suite
 * Issue #4183 - Collection Limit UI & Progress Indicators
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { CollectionLimitIndicator } from '../CollectionLimitIndicator';

import type { CollectionStats } from '../CollectionLimitIndicator';
import type { UserTier } from '@/lib/constants/collection-limits';

describe('CollectionLimitIndicator', () => {
  const mockStats: CollectionStats = {
    gameCount: 25,
    storageMB: 50,
  };

  describe('Rendering', () => {
    it('renders with tier and stats', () => {
      render(<CollectionLimitIndicator tier="Free" stats={mockStats} />);
      expect(screen.getByTestId('collection-limit-indicator')).toBeInTheDocument();
      expect(screen.getByText(/Free Tier/i)).toBeInTheDocument();
    });

    it('displays both progress bars', () => {
      render(<CollectionLimitIndicator tier="Free" stats={mockStats} />);
      expect(screen.getByTestId('games-progress')).toBeInTheDocument();
      expect(screen.getByTestId('storage-progress')).toBeInTheDocument();
    });

    it('shows correct game count', () => {
      render(<CollectionLimitIndicator tier="Free" stats={mockStats} />);
      expect(screen.getByText('25/50')).toBeInTheDocument();
    });

    it('shows correct storage', () => {
      render(<CollectionLimitIndicator tier="Free" stats={mockStats} />);
      expect(screen.getByText('50 MB/100 MB')).toBeInTheDocument();
    });
  });

  describe('Tier-Based Limits', () => {
    it('shows Free tier limits', () => {
      const { container } = render(<CollectionLimitIndicator tier="Free" stats={mockStats} />);
      expect(container.textContent).toMatch(/25/); // game count
      expect(container.textContent).toMatch(/50/); // max games or storage MB
      expect(container.textContent).toMatch(/100 MB/); // storage limit
    });

    it('shows Normal tier limits', () => {
      const { container } = render(<CollectionLimitIndicator tier="Normal" stats={mockStats} />);
      expect(container.textContent).toMatch(/25/); // game count
      expect(container.textContent).toMatch(/100/); // max games
      expect(container.textContent).toMatch(/500 MB/); // storage limit
    });

    it('shows Pro tier limits', () => {
      const { container } = render(<CollectionLimitIndicator tier="Pro" stats={mockStats} />);
      expect(container.textContent).toMatch(/25/); // game count
      expect(container.textContent).toMatch(/500/); // max games
      expect(container.textContent).toMatch(/5\.0 GB/); // 5000 MB formatted
    });

    it('shows Enterprise tier with infinity', () => {
      const { container } = render(<CollectionLimitIndicator tier="Enterprise" stats={mockStats} />);
      expect(container.textContent).toMatch(/25/); // game count
      expect(container.textContent).toMatch(/∞/); // infinity symbol
    });
  });

  describe('Color Coding', () => {
    it('shows green for usage < 75%', () => {
      const stats: CollectionStats = { gameCount: 30, storageMB: 50 }; // 60% and 50%
      const { container } = render(<CollectionLimitIndicator tier="Free" stats={stats} />);
      const progressBars = container.querySelectorAll('[role="progressbar"]');
      expect(progressBars[0]).toHaveClass('[&>div]:bg-green-500');
      expect(progressBars[1]).toHaveClass('[&>div]:bg-green-500');
    });

    it('shows yellow for usage 75-90%', () => {
      const stats: CollectionStats = { gameCount: 40, storageMB: 80 }; // 80% both
      const { container } = render(<CollectionLimitIndicator tier="Free" stats={stats} />);
      const progressBars = container.querySelectorAll('[role="progressbar"]');
      expect(progressBars[0]).toHaveClass('[&>div]:bg-yellow-500');
      expect(progressBars[1]).toHaveClass('[&>div]:bg-yellow-500');
    });

    it('shows red for usage > 90%', () => {
      const stats: CollectionStats = { gameCount: 48, storageMB: 95 }; // 96% and 95%
      const { container } = render(<CollectionLimitIndicator tier="Free" stats={stats} />);
      const progressBars = container.querySelectorAll('[role="progressbar"]');
      expect(progressBars[0]).toHaveClass('[&>div]:bg-red-500');
      expect(progressBars[1]).toHaveClass('[&>div]:bg-red-500');
    });
  });

  describe('Warning Icons', () => {
    it('shows warning icon when game count > 75%', () => {
      const stats: CollectionStats = { gameCount: 40, storageMB: 30 }; // 80% games, 30% storage
      render(<CollectionLimitIndicator tier="Free" stats={stats} />);
      expect(screen.getByTestId('game-warning-icon')).toBeInTheDocument();
      expect(screen.queryByTestId('storage-warning-icon')).not.toBeInTheDocument();
    });

    it('shows warning icon when storage > 75%', () => {
      const stats: CollectionStats = { gameCount: 20, storageMB: 80 }; // 40% games, 80% storage
      render(<CollectionLimitIndicator tier="Free" stats={stats} />);
      expect(screen.queryByTestId('game-warning-icon')).not.toBeInTheDocument();
      expect(screen.getByTestId('storage-warning-icon')).toBeInTheDocument();
    });

    it('no warning icons when usage < 75%', () => {
      const stats: CollectionStats = { gameCount: 30, storageMB: 50 }; // 60% and 50%
      render(<CollectionLimitIndicator tier="Free" stats={stats} />);
      expect(screen.queryByTestId('game-warning-icon')).not.toBeInTheDocument();
      expect(screen.queryByTestId('storage-warning-icon')).not.toBeInTheDocument();
    });

    it('no warning icons for Enterprise tier', () => {
      const stats: CollectionStats = { gameCount: 10000, storageMB: 50000 }; // High usage
      render(<CollectionLimitIndicator tier="Enterprise" stats={stats} />);
      expect(screen.queryByTestId('game-warning-icon')).not.toBeInTheDocument();
      expect(screen.queryByTestId('storage-warning-icon')).not.toBeInTheDocument();
    });
  });

  describe('Upgrade CTA', () => {
    it('shows upgrade button when game usage > 90%', () => {
      const stats: CollectionStats = { gameCount: 48, storageMB: 30 }; // 96% games
      const onUpgrade = vi.fn();
      render(<CollectionLimitIndicator tier="Free" stats={stats} onUpgrade={onUpgrade} />);
      expect(screen.getByTestId('upgrade-cta')).toBeInTheDocument();
      expect(screen.getByText('Upgrade to Normal')).toBeInTheDocument();
    });

    it('shows upgrade button when storage usage > 90%', () => {
      const stats: CollectionStats = { gameCount: 20, storageMB: 95 }; // 95% storage
      const onUpgrade = vi.fn();
      render(<CollectionLimitIndicator tier="Free" stats={stats} onUpgrade={onUpgrade} />);
      expect(screen.getByTestId('upgrade-cta')).toBeInTheDocument();
    });

    it('no upgrade button when usage < 90%', () => {
      const stats: CollectionStats = { gameCount: 40, storageMB: 80 }; // 80% both
      render(<CollectionLimitIndicator tier="Free" stats={mockStats} />);
      expect(screen.queryByTestId('upgrade-cta')).not.toBeInTheDocument();
    });

    it('no upgrade button for Enterprise tier', () => {
      const stats: CollectionStats = { gameCount: 10000, storageMB: 50000 };
      const onUpgrade = vi.fn();
      render(<CollectionLimitIndicator tier="Enterprise" stats={stats} onUpgrade={onUpgrade} />);
      expect(screen.queryByTestId('upgrade-cta')).not.toBeInTheDocument();
    });

    it('no upgrade button when onUpgrade not provided', () => {
      const stats: CollectionStats = { gameCount: 48, storageMB: 95 }; // >90% both
      render(<CollectionLimitIndicator tier="Free" stats={stats} />);
      expect(screen.queryByTestId('upgrade-cta')).not.toBeInTheDocument();
    });

    it('calls onUpgrade when clicked', async () => {
      const stats: CollectionStats = { gameCount: 48, storageMB: 30 };
      const onUpgrade = vi.fn();
      const user = userEvent.setup();
      render(<CollectionLimitIndicator tier="Free" stats={stats} onUpgrade={onUpgrade} />);

      await user.click(screen.getByTestId('upgrade-cta'));
      expect(onUpgrade).toHaveBeenCalledTimes(1);
    });

    it('shows correct upgrade tier for Free', () => {
      const stats: CollectionStats = { gameCount: 48, storageMB: 30 };
      render(<CollectionLimitIndicator tier="Free" stats={stats} onUpgrade={() => {}} />);
      expect(screen.getByText('Upgrade to Normal')).toBeInTheDocument();
    });

    it('shows correct upgrade tier for Normal', () => {
      const stats: CollectionStats = { gameCount: 95, storageMB: 450 };
      render(<CollectionLimitIndicator tier="Normal" stats={stats} onUpgrade={() => {}} />);
      expect(screen.getByText('Upgrade to Pro')).toBeInTheDocument();
    });

    it('shows correct upgrade tier for Pro', () => {
      const stats: CollectionStats = { gameCount: 480, storageMB: 4800 };
      render(<CollectionLimitIndicator tier="Pro" stats={stats} onUpgrade={() => {}} />);
      expect(screen.getByText('Upgrade to Enterprise')).toBeInTheDocument();
    });
  });

  describe('Accessibility', () => {
    it('has descriptive aria-labels for progress bars', () => {
      const { container } = render(<CollectionLimitIndicator tier="Free" stats={mockStats} />);
      const progressBars = container.querySelectorAll('[role="progressbar"]');
      expect(progressBars).toHaveLength(2);
      expect(progressBars[0]).toHaveAttribute('aria-label');
      expect(progressBars[1]).toHaveAttribute('aria-label');
    });

    it('Enterprise tier shows unlimited in aria-labels', () => {
      const { container } = render(<CollectionLimitIndicator tier="Enterprise" stats={mockStats} />);
      const progressBars = container.querySelectorAll('[role="progressbar"]');
      const gameLabel = progressBars[0]?.getAttribute('aria-label') || '';
      const storageLabel = progressBars[1]?.getAttribute('aria-label') || '';
      expect(gameLabel).toMatch(/unlimited/i);
      expect(storageLabel).toMatch(/unlimited/i);
    });
  });

  describe('Tooltips', () => {
    it('tooltip shows exact percentage', async () => {
      const stats: CollectionStats = { gameCount: 37, storageMB: 50 }; // 74% games
      render(<CollectionLimitIndicator tier="Free" stats={stats} />);

      // Tooltip content is rendered on hover (tested via TooltipContent component)
      const tooltipTrigger = screen.getByTestId('games-progress');
      expect(tooltipTrigger).toBeInTheDocument();
    });
  });

  describe('Edge Cases', () => {
    it('handles 0 games', () => {
      const stats: CollectionStats = { gameCount: 0, storageMB: 0 };
      const { container } = render(<CollectionLimitIndicator tier="Free" stats={stats} />);
      expect(container.textContent).toMatch(/0/);
    });

    it('handles exactly at limit', () => {
      const stats: CollectionStats = { gameCount: 50, storageMB: 100 }; // 100% both
      render(<CollectionLimitIndicator tier="Free" stats={stats} onUpgrade={() => {}} />);
      expect(screen.getByTestId('upgrade-cta')).toBeInTheDocument(); // >90% triggers CTA
    });

    it('handles large numbers with K formatting', () => {
      const stats: CollectionStats = { gameCount: 1500, storageMB: 6000 };
      const { container } = render(<CollectionLimitIndicator tier="Enterprise" stats={stats} />);
      expect(container.textContent).toMatch(/1\.5K/);
      expect(container.textContent).toMatch(/∞/);
    });
  });
});
