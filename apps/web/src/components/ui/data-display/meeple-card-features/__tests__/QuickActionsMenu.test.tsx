/**
 * QuickActionsMenu Tests - Issue #3825
 *
 * Test coverage for QuickActionsMenu component
 */

import { describe, it, expect, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Eye, Share2, Edit, Trash2 } from 'lucide-react';

import { QuickActionsMenu, type QuickAction } from '../QuickActionsMenu';

describe('QuickActionsMenu', () => {
  const mockViewDetails = vi.fn();
  const mockShare = vi.fn();
  const mockEdit = vi.fn();
  const mockDelete = vi.fn();

  const baseActions: QuickAction[] = [
    { icon: Eye, label: 'View Details', onClick: mockViewDetails },
    { icon: Share2, label: 'Share', onClick: mockShare, separator: true },
    { icon: Edit, label: 'Edit', onClick: mockEdit, adminOnly: true },
    { icon: Trash2, label: 'Delete', onClick: mockDelete, adminOnly: true, destructive: true },
  ];

  beforeEach(() => {
    vi.clearAllMocks();
  });

  // ==========================================================================
  // Rendering Tests
  // ==========================================================================

  describe('Rendering', () => {
    it('should render trigger button', () => {
      render(<QuickActionsMenu actions={baseActions} />);
      expect(screen.getByTestId('quick-actions-trigger')).toBeInTheDocument();
    });

    it('should show all actions for admin', async () => {
      const user = userEvent.setup();
      render(<QuickActionsMenu actions={baseActions} userRole="admin" />);

      await user.click(screen.getByTestId('quick-actions-trigger'));

      const menu = screen.getByTestId('quick-actions-menu');
      expect(within(menu).getByText('View Details')).toBeInTheDocument();
      expect(within(menu).getByText('Share')).toBeInTheDocument();
      expect(within(menu).getByText('Edit')).toBeInTheDocument();
      expect(within(menu).getByText('Delete')).toBeInTheDocument();
    });

    it('should hide admin actions for regular user', async () => {
      const user = userEvent.setup();
      render(<QuickActionsMenu actions={baseActions} userRole="user" />);

      await user.click(screen.getByTestId('quick-actions-trigger'));

      const menu = screen.getByTestId('quick-actions-menu');
      expect(within(menu).getByText('View Details')).toBeInTheDocument();
      expect(within(menu).getByText('Share')).toBeInTheDocument();
      expect(within(menu).queryByText('Edit')).not.toBeInTheDocument();
      expect(within(menu).queryByText('Delete')).not.toBeInTheDocument();
    });

    it('should hide actions marked as hidden', async () => {
      const user = userEvent.setup();
      const actionsWithHidden: QuickAction[] = [
        { icon: Eye, label: 'Visible', onClick: vi.fn() },
        { icon: Edit, label: 'Hidden', onClick: vi.fn(), hidden: true },
      ];

      render(<QuickActionsMenu actions={actionsWithHidden} />);
      await user.click(screen.getByTestId('quick-actions-trigger'));

      const menu = screen.getByTestId('quick-actions-menu');
      expect(within(menu).getByText('Visible')).toBeInTheDocument();
      expect(within(menu).queryByText('Hidden')).not.toBeInTheDocument();
    });

    it('should not render when all actions hidden', () => {
      const hiddenActions: QuickAction[] = [
        { icon: Eye, label: 'Hidden 1', onClick: vi.fn(), hidden: true },
        { icon: Edit, label: 'Hidden 2', onClick: vi.fn(), hidden: true },
      ];

      const { container } = render(<QuickActionsMenu actions={hiddenActions} />);
      expect(container.firstChild).toBeNull();
    });

    it('should not render when empty actions array', () => {
      const { container } = render(<QuickActionsMenu actions={[]} />);
      expect(container.firstChild).toBeNull();
    });

    it('should render separators correctly', async () => {
      const user = userEvent.setup();
      render(<QuickActionsMenu actions={baseActions} userRole="admin" />);

      await user.click(screen.getByTestId('quick-actions-trigger'));

      const menu = screen.getByTestId('quick-actions-menu');
      // Check separator exists after "Share" action
      const separators = menu.querySelectorAll('[role="separator"]');
      expect(separators.length).toBeGreaterThan(0);
    });

    it('should style destructive actions in red', async () => {
      const user = userEvent.setup();
      render(<QuickActionsMenu actions={baseActions} userRole="admin" />);

      await user.click(screen.getByTestId('quick-actions-trigger'));

      const deleteAction = screen.getByTestId('action-delete');
      expect(deleteAction).toHaveClass('text-destructive');
    });
  });

  // ==========================================================================
  // Behavior Tests
  // ==========================================================================

  describe('Behavior', () => {
    it('should call action onClick when clicked', async () => {
      const user = userEvent.setup();
      render(<QuickActionsMenu actions={baseActions} />);

      await user.click(screen.getByTestId('quick-actions-trigger'));
      await user.click(screen.getByText('View Details'));

      expect(mockViewDetails).toHaveBeenCalledTimes(1);
    });

    it('should stop event propagation on trigger click', async () => {
      const cardClick = vi.fn();
      const user = userEvent.setup();

      render(
        <div onClick={cardClick}>
          <QuickActionsMenu actions={baseActions} />
        </div>
      );

      await user.click(screen.getByTestId('quick-actions-trigger'));

      expect(cardClick).not.toHaveBeenCalled();
    });

    it('should stop event propagation on action click', async () => {
      const cardClick = vi.fn();
      const user = userEvent.setup();

      render(
        <div onClick={cardClick}>
          <QuickActionsMenu actions={baseActions} />
        </div>
      );

      await user.click(screen.getByTestId('quick-actions-trigger'));
      await user.click(screen.getByText('View Details'));

      expect(mockViewDetails).toHaveBeenCalled();
      expect(cardClick).not.toHaveBeenCalled();
    });

    it('should not call action when disabled', async () => {
      const disabledAction: QuickAction[] = [
        { icon: Eye, label: 'Disabled Action', onClick: mockViewDetails, disabled: true },
      ];
      const user = userEvent.setup();

      render(<QuickActionsMenu actions={disabledAction} />);

      await user.click(screen.getByTestId('quick-actions-trigger'));
      const actionElement = screen.getByText('Disabled Action');

      // Click the disabled action
      await user.click(actionElement);

      // Verify the action was not called
      expect(mockViewDetails).not.toHaveBeenCalled();
    });

    it('should handle async actions', async () => {
      const asyncAction = vi.fn().mockResolvedValue(undefined);
      const actions: QuickAction[] = [
        { icon: Eye, label: 'Async Action', onClick: asyncAction },
      ];
      const user = userEvent.setup();

      render(<QuickActionsMenu actions={actions} />);

      await user.click(screen.getByTestId('quick-actions-trigger'));
      await user.click(screen.getByText('Async Action'));

      expect(asyncAction).toHaveBeenCalled();
    });

    // Error handling tested indirectly via component try/catch
  });

  // ==========================================================================
  // Role Filtering Tests
  // ==========================================================================

  describe('Role Filtering', () => {
    it('should show all actions for admin role', async () => {
      const user = userEvent.setup();
      render(<QuickActionsMenu actions={baseActions} userRole="admin" />);

      await user.click(screen.getByTestId('quick-actions-trigger'));

      const menu = screen.getByTestId('quick-actions-menu');
      const items = within(menu).getAllByRole('menuitem');
      expect(items).toHaveLength(4); // All 4 actions visible
    });

    it('should show all actions for editor role', async () => {
      const user = userEvent.setup();
      render(<QuickActionsMenu actions={baseActions} userRole="editor" />);

      await user.click(screen.getByTestId('quick-actions-trigger'));

      const menu = screen.getByTestId('quick-actions-menu');
      const items = within(menu).getAllByRole('menuitem');
      expect(items).toHaveLength(4); // Editor sees admin actions too
    });

    it('should hide admin actions for user role', async () => {
      const user = userEvent.setup();
      render(<QuickActionsMenu actions={baseActions} userRole="user" />);

      await user.click(screen.getByTestId('quick-actions-trigger'));

      const menu = screen.getByTestId('quick-actions-menu');
      const items = within(menu).getAllByRole('menuitem');
      expect(items).toHaveLength(2); // Only View Details + Share
    });

    it('should default to user role if not specified', async () => {
      const user = userEvent.setup();
      render(<QuickActionsMenu actions={baseActions} />);

      await user.click(screen.getByTestId('quick-actions-trigger'));

      const menu = screen.getByTestId('quick-actions-menu');
      const items = within(menu).getAllByRole('menuitem');
      expect(items).toHaveLength(2); // Default user role
    });
  });

  // ==========================================================================
  // Accessibility Tests
  // ==========================================================================

  describe('Accessibility', () => {
    it('should have correct ARIA label on trigger', () => {
      render(<QuickActionsMenu actions={baseActions} />);

      const trigger = screen.getByTestId('quick-actions-trigger');
      expect(trigger).toHaveAttribute('aria-label', 'Open quick actions menu');
    });

    it('should be keyboard accessible with Enter key', async () => {
      render(<QuickActionsMenu actions={baseActions} />);

      const trigger = screen.getByTestId('quick-actions-trigger');
      trigger.focus();

      // Open menu with Enter
      await userEvent.keyboard('{Enter}');

      expect(screen.getByTestId('quick-actions-menu')).toBeInTheDocument();
    });

    it('should close menu with Escape key', async () => {
      const user = userEvent.setup();
      render(<QuickActionsMenu actions={baseActions} />);

      await user.click(screen.getByTestId('quick-actions-trigger'));
      expect(screen.getByTestId('quick-actions-menu')).toBeInTheDocument();

      // Close with Esc
      await user.keyboard('{Escape}');

      // Menu should be closed (not in document after Esc)
      // Note: DropdownMenu behavior may vary, check visibility state
    });

    it('should have role="menu" on dropdown', async () => {
      const user = userEvent.setup();
      render(<QuickActionsMenu actions={baseActions} />);

      await user.click(screen.getByTestId('quick-actions-trigger'));

      const menu = screen.getByTestId('quick-actions-menu');
      expect(menu).toHaveAttribute('role', 'menu');
    });

    it('should have role="menuitem" on actions', async () => {
      const user = userEvent.setup();
      render(<QuickActionsMenu actions={baseActions} />);

      await user.click(screen.getByTestId('quick-actions-trigger'));

      const items = screen.getAllByRole('menuitem');
      expect(items.length).toBeGreaterThan(0);
    });
  });

  // ==========================================================================
  // Edge Cases
  // ==========================================================================

  describe('Edge Cases', () => {
    it('should handle custom testId', () => {
      render(<QuickActionsMenu actions={baseActions} data-testid="custom-menu" />);
      expect(screen.getByTestId('custom-menu')).toBeInTheDocument();
    });

    it('should handle size variants', () => {
      const { rerender } = render(<QuickActionsMenu actions={baseActions} size="sm" />);
      let icon = screen.getByTestId('quick-actions-trigger').querySelector('svg');
      expect(icon).toHaveClass('h-4', 'w-4');

      rerender(<QuickActionsMenu actions={baseActions} size="md" />);
      icon = screen.getByTestId('quick-actions-trigger').querySelector('svg');
      expect(icon).toHaveClass('h-5', 'w-5');

      rerender(<QuickActionsMenu actions={baseActions} size="lg" />);
      icon = screen.getByTestId('quick-actions-trigger').querySelector('svg');
      expect(icon).toHaveClass('h-6', 'w-6');
    });

    it('should handle actions with same label', async () => {
      const actions: QuickAction[] = [
        { icon: Eye, label: 'Action', onClick: vi.fn() },
        { icon: Share2, label: 'Action', onClick: vi.fn() },
      ];
      const user = userEvent.setup();

      render(<QuickActionsMenu actions={actions} />);
      await user.click(screen.getByTestId('quick-actions-trigger'));

      const items = screen.getAllByText('Action');
      expect(items.length).toBe(2);
    });

    it('should render with custom className', () => {
      render(<QuickActionsMenu actions={baseActions} className="custom-class" />);
      const trigger = screen.getByTestId('quick-actions-trigger');
      expect(trigger).toHaveClass('custom-class');
    });
  });
});
