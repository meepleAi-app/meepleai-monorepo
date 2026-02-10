/**
 * QuickActionsGrid Unit Tests (Issue #3313)
 *
 * Coverage areas:
 * - Rendering with default actions
 * - Custom actions
 * - Loading state
 * - Navigation links
 * - Touch-friendly sizing
 * - Responsive grid
 *
 * Target: 90%+ coverage
 */

import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { Library, Dices } from 'lucide-react';
import { QuickActionsGrid, type QuickAction } from '../QuickActionsGrid';

// Mock analytics
vi.mock('@/lib/analytics/track-event', () => ({
  trackEvent: vi.fn(),
}));

// Mock next/link
vi.mock('next/link', () => ({
  default: ({ children, href, onClick, ...props }: { children: React.ReactNode; href: string; onClick?: () => void; [key: string]: unknown }) => (
    <a href={href} onClick={onClick} {...props}>{children}</a>
  ),
}));

// Mock framer-motion
vi.mock('framer-motion', () => ({
  motion: {
    div: ({ children, ...props }: { children: React.ReactNode; [key: string]: unknown }) => (
      <div {...props}>{children}</div>
    ),
  },
}));

// ============================================================================
// Test Data
// ============================================================================

const customActions: QuickAction[] = [
  {
    id: 'custom-1',
    icon: Library,
    label: 'Custom Action 1',
    href: '/custom-1',
    iconColor: 'text-red-600',
    bgColor: 'bg-red-500/20',
  },
  {
    id: 'custom-2',
    icon: Dices,
    label: 'Custom Action 2',
    href: '/custom-2',
    iconColor: 'text-green-600',
    bgColor: 'bg-green-500/20',
  },
];

describe('QuickActionsGrid', () => {
  // ============================================================================
  // Rendering Tests
  // ============================================================================

  describe('Rendering', () => {
    it('renders grid container', () => {
      render(<QuickActionsGrid />);

      expect(screen.getByTestId('quick-actions-grid')).toBeInTheDocument();
    });

    it('renders all 5 default action buttons', () => {
      render(<QuickActionsGrid />);

      expect(screen.getByTestId('quick-action-library')).toBeInTheDocument();
      expect(screen.getByTestId('quick-action-session')).toBeInTheDocument();
      expect(screen.getByTestId('quick-action-chat')).toBeInTheDocument();
      expect(screen.getByTestId('quick-action-catalog')).toBeInTheDocument();
      expect(screen.getByTestId('quick-action-settings')).toBeInTheDocument();
    });

    it('renders action labels', () => {
      render(<QuickActionsGrid />);

      expect(screen.getByTestId('quick-action-label-library')).toHaveTextContent('Vai alla Collezione');
      expect(screen.getByTestId('quick-action-label-session')).toHaveTextContent('Nuova Sessione');
      expect(screen.getByTestId('quick-action-label-chat')).toHaveTextContent('Chat AI');
      expect(screen.getByTestId('quick-action-label-catalog')).toHaveTextContent('Esplora Catalogo');
      expect(screen.getByTestId('quick-action-label-settings')).toHaveTextContent('Impostazioni');
    });

    it('applies custom className', () => {
      const { container } = render(<QuickActionsGrid className="custom-grid" />);

      expect(container.querySelector('.custom-grid')).toBeInTheDocument();
    });
  });

  // ============================================================================
  // Navigation Tests
  // ============================================================================

  describe('Navigation', () => {
    it('library action links to /library', () => {
      render(<QuickActionsGrid />);

      const action = screen.getByTestId('quick-action-library');
      expect(action).toHaveAttribute('href', '/library');
    });

    it('session action links to /sessions/new', () => {
      render(<QuickActionsGrid />);

      const action = screen.getByTestId('quick-action-session');
      expect(action).toHaveAttribute('href', '/sessions/new');
    });

    it('chat action links to /chat', () => {
      render(<QuickActionsGrid />);

      const action = screen.getByTestId('quick-action-chat');
      expect(action).toHaveAttribute('href', '/chat');
    });

    it('catalog action links to /games/catalog', () => {
      render(<QuickActionsGrid />);

      const action = screen.getByTestId('quick-action-catalog');
      expect(action).toHaveAttribute('href', '/games/catalog');
    });

    it('settings action links to /settings', () => {
      render(<QuickActionsGrid />);

      const action = screen.getByTestId('quick-action-settings');
      expect(action).toHaveAttribute('href', '/settings');
    });
  });

  // ============================================================================
  // Custom Actions Tests
  // ============================================================================

  describe('Custom Actions', () => {
    it('renders custom actions when provided', () => {
      render(<QuickActionsGrid actions={customActions} />);

      expect(screen.getByTestId('quick-action-custom-1')).toBeInTheDocument();
      expect(screen.getByTestId('quick-action-custom-2')).toBeInTheDocument();
      // Should not render default actions
      expect(screen.queryByTestId('quick-action-library')).not.toBeInTheDocument();
    });

    it('custom action labels are displayed', () => {
      render(<QuickActionsGrid actions={customActions} />);

      expect(screen.getByTestId('quick-action-label-custom-1')).toHaveTextContent('Custom Action 1');
      expect(screen.getByTestId('quick-action-label-custom-2')).toHaveTextContent('Custom Action 2');
    });

    it('custom action links work correctly', () => {
      render(<QuickActionsGrid actions={customActions} />);

      expect(screen.getByTestId('quick-action-custom-1')).toHaveAttribute('href', '/custom-1');
      expect(screen.getByTestId('quick-action-custom-2')).toHaveAttribute('href', '/custom-2');
    });
  });

  // ============================================================================
  // Loading State Tests
  // ============================================================================

  describe('Loading State', () => {
    it('renders skeleton when isLoading', () => {
      render(<QuickActionsGrid isLoading />);

      expect(screen.getByTestId('quick-actions-skeleton')).toBeInTheDocument();
    });

    it('does not render content when loading', () => {
      render(<QuickActionsGrid isLoading />);

      expect(screen.queryByTestId('quick-actions-grid')).not.toBeInTheDocument();
      expect(screen.queryByTestId('quick-action-library')).not.toBeInTheDocument();
    });

    it('skeleton has glassmorphic styling', () => {
      render(<QuickActionsGrid isLoading />);

      const skeleton = screen.getByTestId('quick-actions-skeleton');
      expect(skeleton).toHaveClass('backdrop-blur-xl');
    });
  });

  // ============================================================================
  // Styling Tests
  // ============================================================================

  describe('Styling', () => {
    it('grid has glassmorphic styling', () => {
      render(<QuickActionsGrid />);

      const grid = screen.getByTestId('quick-actions-grid');
      expect(grid).toHaveClass('backdrop-blur-xl');
      expect(grid).toHaveClass('rounded-2xl');
    });

    it('action buttons are touch-friendly (min-h and min-w)', () => {
      render(<QuickActionsGrid />);

      const action = screen.getByTestId('quick-action-library');
      expect(action).toHaveClass('min-h-[88px]');
      expect(action).toHaveClass('min-w-[88px]');
    });

    it('action buttons have hover scale effect class', () => {
      render(<QuickActionsGrid />);

      const action = screen.getByTestId('quick-action-library');
      expect(action).toHaveClass('hover:scale-105');
    });
  });

  // ============================================================================
  // Grid Layout Tests
  // ============================================================================

  describe('Grid Layout', () => {
    it('actions container has grid layout classes', () => {
      render(<QuickActionsGrid />);

      const container = screen.getByTestId('actions-container');
      expect(container).toHaveClass('grid');
      expect(container).toHaveClass('grid-cols-2'); // Mobile
      expect(container).toHaveClass('sm:grid-cols-3'); // Tablet
      expect(container).toHaveClass('md:grid-cols-5'); // Desktop
    });
  });

  // ============================================================================
  // Default Props Tests
  // ============================================================================

  describe('Default Props', () => {
    it('uses default actions when not provided', () => {
      render(<QuickActionsGrid />);

      // Should render 5 default actions
      const actions = screen.getAllByTestId(/^quick-action-(?!label)/);
      expect(actions.length).toBe(5);
    });

    it('isLoading defaults to false', () => {
      render(<QuickActionsGrid />);

      expect(screen.queryByTestId('quick-actions-skeleton')).not.toBeInTheDocument();
      expect(screen.getByTestId('quick-actions-grid')).toBeInTheDocument();
    });
  });

  // ============================================================================
  // Analytics Tracking Tests
  // ============================================================================

  describe('Analytics Tracking', () => {
    it('fires trackEvent on action click', async () => {
      const { trackEvent } = await import('@/lib/analytics/track-event');

      render(<QuickActionsGrid />);

      const libraryAction = screen.getByTestId('quick-action-library');
      fireEvent.click(libraryAction);

      expect(trackEvent).toHaveBeenCalledWith(
        'dashboard_quick_action_library',
        expect.objectContaining({
          action_name: 'Vai alla Collezione',
          destination: '/library',
          source: 'dashboard',
        })
      );
    });

    it('fires trackEvent with correct args for each action', async () => {
      const { trackEvent } = await import('@/lib/analytics/track-event');

      render(<QuickActionsGrid />);

      fireEvent.click(screen.getByTestId('quick-action-chat'));

      expect(trackEvent).toHaveBeenCalledWith(
        'dashboard_quick_action_chat',
        expect.objectContaining({
          action_name: 'Chat AI',
          destination: '/chat',
          source: 'dashboard',
        })
      );
    });
  });

  // ============================================================================
  // Accessibility Tests
  // ============================================================================

  describe('Accessibility', () => {
    it('action buttons have focus ring classes', () => {
      render(<QuickActionsGrid />);

      const action = screen.getByTestId('quick-action-library');
      expect(action).toHaveClass('focus:ring-2');
      expect(action).toHaveClass('focus:outline-none');
    });

    it('all actions are links', () => {
      render(<QuickActionsGrid />);

      const links = screen.getAllByRole('link');
      expect(links.length).toBe(5);
    });
  });
});
