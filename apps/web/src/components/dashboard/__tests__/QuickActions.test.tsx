/**
 * QuickActions Unit Tests (Issue #1834: UI-007)
 *
 * Coverage areas:
 * - Default actions rendering
 * - Custom actions prop
 * - Grid layout verification
 * - Integration with QuickActionCard
 * - Router navigation
 * - Accessibility
 *
 * Target: 95%+ coverage
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QuickActions } from '../QuickActions';
import { PlusCircle, Settings, Upload } from 'lucide-react';
import { useRouter } from 'next/router';

// Mock Next.js router
vi.mock('next/router', () => ({
  useRouter: vi.fn(),
}));

describe('QuickActions', () => {
  const mockPush = vi.fn();
  const mockRouter = {
    push: mockPush,
    pathname: '/',
    route: '/',
    query: {},
    asPath: '/',
    basePath: '',
    isLocaleDomain: false,
    isReady: true,
    isPreview: false,
  };

  beforeEach(() => {
    vi.clearAllMocks();
    (useRouter as ReturnType<typeof vi.fn>).mockReturnValue(mockRouter);
  });

  // ============================================================================
  // Default Actions Tests
  // ============================================================================

  describe('Default Actions', () => {
    it('renders two default actions', () => {
      render(<QuickActions />);

      expect(screen.getByRole('button', { name: /Add Game/i })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /New Chat/i })).toBeInTheDocument();
    });

    it('renders Add Game action with correct text', () => {
      render(<QuickActions />);

      expect(screen.getByText('Add Game')).toBeInTheDocument();
      expect(screen.getByText('Add a new board game to your collection')).toBeInTheDocument();
    });

    it('renders New Chat action with correct text', () => {
      render(<QuickActions />);

      expect(screen.getByText('New Chat')).toBeInTheDocument();
      expect(screen.getByText('Start a conversation about game rules')).toBeInTheDocument();
    });

    it('navigates to /games/add when Add Game is clicked', async () => {
      const user = userEvent.setup();
      render(<QuickActions />);

      const addGameButton = screen.getByRole('button', { name: /Add Game/i });
      await user.click(addGameButton);

      expect(mockPush).toHaveBeenCalledWith('/games/add');
      expect(mockPush).toHaveBeenCalledTimes(1);
    });

    it('navigates to /chat/new when New Chat is clicked', async () => {
      const user = userEvent.setup();
      render(<QuickActions />);

      const newChatButton = screen.getByRole('button', { name: /New Chat/i });
      await user.click(newChatButton);

      expect(mockPush).toHaveBeenCalledWith('/chat/new');
      expect(mockPush).toHaveBeenCalledTimes(1);
    });
  });

  // ============================================================================
  // Custom Actions Tests
  // ============================================================================

  describe('Custom Actions', () => {
    it('renders custom actions when provided', () => {
      const customActions = [
        {
          id: 'settings',
          icon: Settings,
          title: 'Settings',
          description: 'Configure preferences',
          onClick: vi.fn(),
        },
        {
          id: 'upload',
          icon: Upload,
          title: 'Upload',
          description: 'Upload files',
          onClick: vi.fn(),
        },
      ];

      render(<QuickActions actions={customActions} />);

      expect(screen.getByText('Settings')).toBeInTheDocument();
      expect(screen.getByText('Upload')).toBeInTheDocument();
      expect(screen.queryByText('Add Game')).not.toBeInTheDocument();
      expect(screen.queryByText('New Chat')).not.toBeInTheDocument();
    });

    it('calls custom onClick handlers', async () => {
      const user = userEvent.setup();
      const onClick1 = vi.fn();
      const onClick2 = vi.fn();

      const customActions = [
        {
          id: 'action1',
          icon: Settings,
          title: 'Action 1',
          onClick: onClick1,
        },
        {
          id: 'action2',
          icon: Upload,
          title: 'Action 2',
          onClick: onClick2,
        },
      ];

      render(<QuickActions actions={customActions} />);

      await user.click(screen.getByRole('button', { name: 'Action 1' }));
      expect(onClick1).toHaveBeenCalledTimes(1);

      await user.click(screen.getByRole('button', { name: 'Action 2' }));
      expect(onClick2).toHaveBeenCalledTimes(1);
    });

    it('renders custom actions with variants', () => {
      const customActions = [
        {
          id: 'default',
          icon: Settings,
          title: 'Default Variant',
          onClick: vi.fn(),
          variant: 'default' as const,
        },
        {
          id: 'secondary',
          icon: Upload,
          title: 'Secondary Variant',
          onClick: vi.fn(),
          variant: 'secondary' as const,
        },
      ];

      const { container } = render(<QuickActions actions={customActions} />);

      const cards = container.querySelectorAll('[role="button"]');
      expect(cards).toHaveLength(2);
      expect(cards[0]).toHaveClass('bg-card');
      expect(cards[1]).toHaveClass('bg-secondary/10');
    });

    it('renders single custom action', () => {
      const customActions = [
        {
          id: 'single',
          icon: Settings,
          title: 'Single Action',
          onClick: vi.fn(),
        },
      ];

      render(<QuickActions actions={customActions} />);

      const buttons = screen.getAllByRole('button');
      expect(buttons).toHaveLength(1);
      expect(screen.getByText('Single Action')).toBeInTheDocument();
    });

    it('renders many custom actions (4+)', () => {
      const customActions = [
        { id: '1', icon: PlusCircle, title: 'Action 1', onClick: vi.fn() },
        { id: '2', icon: Settings, title: 'Action 2', onClick: vi.fn() },
        { id: '3', icon: Upload, title: 'Action 3', onClick: vi.fn() },
        { id: '4', icon: PlusCircle, title: 'Action 4', onClick: vi.fn() },
      ];

      render(<QuickActions actions={customActions} />);

      const buttons = screen.getAllByRole('button');
      expect(buttons).toHaveLength(4);
    });
  });

  // ============================================================================
  // Layout Tests
  // ============================================================================

  describe('Layout', () => {
    it('renders grid container', () => {
      const { container } = render(<QuickActions />);

      const grid = container.querySelector('.grid');
      expect(grid).toBeInTheDocument();
    });

    it('applies 2-column grid on desktop', () => {
      const { container } = render(<QuickActions />);

      const grid = container.querySelector('.grid');
      expect(grid).toHaveClass('sm:grid-cols-2');
    });

    it('applies 1-column grid on mobile', () => {
      const { container } = render(<QuickActions />);

      const grid = container.querySelector('.grid');
      expect(grid).toHaveClass('grid-cols-1');
    });

    it('applies gap between items', () => {
      const { container } = render(<QuickActions />);

      const grid = container.querySelector('.grid');
      expect(grid).toHaveClass('gap-4');
    });

    it('applies custom className', () => {
      const { container } = render(<QuickActions className="custom-class" />);

      const grid = container.querySelector('.custom-class');
      expect(grid).toBeInTheDocument();
    });
  });

  // ============================================================================
  // Accessibility Tests
  // ============================================================================

  describe('Accessibility', () => {
    it('has region role with aria-label', () => {
      render(<QuickActions />);

      const region = screen.getByRole('region', { name: 'Quick actions' });
      expect(region).toBeInTheDocument();
    });

    it('all action cards are keyboard accessible', () => {
      render(<QuickActions />);

      const buttons = screen.getAllByRole('button');
      buttons.forEach(button => {
        expect(button).toHaveAttribute('tabIndex', '0');
      });
    });

    it('all action cards have proper ARIA labels', () => {
      render(<QuickActions />);

      expect(
        screen.getByRole('button', { name: 'Add Game: Add a new board game to your collection' })
      ).toBeInTheDocument();
      expect(
        screen.getByRole('button', { name: 'New Chat: Start a conversation about game rules' })
      ).toBeInTheDocument();
    });

    it('maintains semantic HTML structure', () => {
      const { container } = render(<QuickActions />);

      // Should have h3 headings for titles
      const headings = container.querySelectorAll('h3');
      expect(headings.length).toBeGreaterThanOrEqual(2);
    });
  });

  // ============================================================================
  // Integration Tests
  // ============================================================================

  describe('Integration with QuickActionCard', () => {
    it('passes icon prop to QuickActionCard', () => {
      const { container } = render(<QuickActions />);

      // Should have SVG icons (lucide-react renders as SVG)
      const icons = container.querySelectorAll('svg');
      expect(icons.length).toBeGreaterThanOrEqual(2);
    });

    it('passes title prop to QuickActionCard', () => {
      render(<QuickActions />);

      expect(screen.getByText('Add Game')).toBeInTheDocument();
      expect(screen.getByText('New Chat')).toBeInTheDocument();
    });

    it('passes description prop to QuickActionCard', () => {
      render(<QuickActions />);

      expect(screen.getByText(/Add a new board game/i)).toBeInTheDocument();
      expect(screen.getByText(/Start a conversation/i)).toBeInTheDocument();
    });

    it('passes onClick prop to QuickActionCard', async () => {
      const user = userEvent.setup();
      render(<QuickActions />);

      const addGameButton = screen.getByRole('button', { name: /Add Game/i });
      await user.click(addGameButton);

      expect(mockPush).toHaveBeenCalled();
    });
  });

  // ============================================================================
  // Edge Cases
  // ============================================================================

  describe('Edge Cases', () => {
    it('renders with empty actions array', () => {
      const { container } = render(<QuickActions actions={[]} />);

      const grid = container.querySelector('[role="region"]');
      expect(grid).toBeInTheDocument();
      expect(screen.queryAllByRole('button')).toHaveLength(0);
    });

    it('handles actions without description', () => {
      const customActions = [
        {
          id: 'no-desc',
          icon: Settings,
          title: 'No Description',
          onClick: vi.fn(),
        },
      ];

      render(<QuickActions actions={customActions} />);

      expect(screen.getByText('No Description')).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'No Description' })).toBeInTheDocument();
    });

    it('handles actions with variant undefined', () => {
      const customActions = [
        {
          id: 'no-variant',
          icon: Settings,
          title: 'No Variant',
          onClick: vi.fn(),
          variant: undefined,
        },
      ];

      render(<QuickActions actions={customActions} />);

      expect(screen.getByText('No Variant')).toBeInTheDocument();
    });

    it('memoizes default actions correctly', () => {
      const { rerender } = render(<QuickActions />);

      const firstRenderButtons = screen.getAllByRole('button');

      rerender(<QuickActions />);

      const secondRenderButtons = screen.getAllByRole('button');
      expect(firstRenderButtons.length).toBe(secondRenderButtons.length);
    });
  });

  // ============================================================================
  // Router Integration Tests
  // ============================================================================

  describe('Router Integration', () => {
    it('uses router from useRouter hook', () => {
      render(<QuickActions />);

      expect(useRouter).toHaveBeenCalled();
    });

    it('default actions depend on router', async () => {
      const user = userEvent.setup();
      render(<QuickActions />);

      const addGameButton = screen.getByRole('button', { name: /Add Game/i });
      await user.click(addGameButton);

      expect(mockRouter.push).toHaveBeenCalledWith('/games/add');
    });

    it('default actions are stable across renders', () => {
      const { rerender } = render(<QuickActions />);

      const firstButtons = screen.getAllByRole('button');
      expect(firstButtons).toHaveLength(2);

      rerender(<QuickActions />);

      const secondButtons = screen.getAllByRole('button');
      expect(secondButtons).toHaveLength(2);
      expect(firstButtons[0].textContent).toBe(secondButtons[0].textContent);
    });
  });
});
