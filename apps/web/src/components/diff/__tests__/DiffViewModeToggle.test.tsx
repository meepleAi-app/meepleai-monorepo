/**
 * Unit tests for DiffViewModeToggle component
 * Tests toggle between list and side-by-side views
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { DiffViewModeToggle } from '../DiffViewModeToggle';

describe('DiffViewModeToggle', () => {
  const mockOnModeChange = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Rendering', () => {
    it('should render both view mode buttons', () => {
      render(<DiffViewModeToggle currentMode="list" onModeChange={mockOnModeChange} />);

      expect(screen.getByLabelText('List view')).toBeInTheDocument();
      expect(screen.getByLabelText('Side-by-side view')).toBeInTheDocument();
    });

    it('should have radiogroup role', () => {
      render(<DiffViewModeToggle currentMode="list" onModeChange={mockOnModeChange} />);

      // shadcn ToggleGroup uses "group" role, not "radiogroup"
      expect(screen.getByRole('group')).toBeInTheDocument();
    });

    it('should display correct text and icons', () => {
      render(<DiffViewModeToggle currentMode="list" onModeChange={mockOnModeChange} />);

      // Updated: shadcn uses Lucide icons instead of emoji
      expect(screen.getByText('List')).toBeInTheDocument();
      expect(screen.getByText('Side-by-Side')).toBeInTheDocument();

      // Icons are rendered as SVG
      const listButton = screen.getByLabelText('List view');
      const sideButton = screen.getByLabelText('Side-by-side view');

      expect(listButton.querySelector('svg')).toBeInTheDocument();
      expect(sideButton.querySelector('svg')).toBeInTheDocument();
    });
  });

  describe('Active State - List Mode', () => {
    it('should mark list button as active when currentMode is list', () => {
      render(<DiffViewModeToggle currentMode="list" onModeChange={mockOnModeChange} />);

      const listButton = screen.getByLabelText('List view');

      // shadcn ToggleGroup uses data-state="on" for active state
      expect(listButton).toHaveAttribute('data-state', 'on');
    });

    it('should mark side-by-side button as inactive when currentMode is list', () => {
      render(<DiffViewModeToggle currentMode="list" onModeChange={mockOnModeChange} />);

      const sideButton = screen.getByLabelText('Side-by-side view');

      // shadcn ToggleGroup uses data-state="off" for inactive state
      expect(sideButton).toHaveAttribute('data-state', 'off');
    });
  });

  describe('Active State - Side-by-Side Mode', () => {
    it('should mark side-by-side button as active when currentMode is side-by-side', () => {
      render(<DiffViewModeToggle currentMode="side-by-side" onModeChange={mockOnModeChange} />);

      const sideButton = screen.getByLabelText('Side-by-side view');

      // shadcn ToggleGroup uses data-state="on" for active state
      expect(sideButton).toHaveAttribute('data-state', 'on');
    });

    it('should mark list button as inactive when currentMode is side-by-side', () => {
      render(<DiffViewModeToggle currentMode="side-by-side" onModeChange={mockOnModeChange} />);

      const listButton = screen.getByLabelText('List view');

      // shadcn ToggleGroup uses data-state="off" for inactive state
      expect(listButton).toHaveAttribute('data-state', 'off');
    });
  });

  describe('User Interactions', () => {
    it('should call onModeChange with "list" when list button clicked', async () => {
      const user = userEvent.setup();

      render(<DiffViewModeToggle currentMode="side-by-side" onModeChange={mockOnModeChange} />);

      const listButton = screen.getByLabelText('List view');
      await user.click(listButton);

      expect(mockOnModeChange).toHaveBeenCalledWith('list');
      expect(mockOnModeChange).toHaveBeenCalledTimes(1);
    });

    it('should call onModeChange with "side-by-side" when side-by-side button clicked', async () => {
      const user = userEvent.setup();

      render(<DiffViewModeToggle currentMode="list" onModeChange={mockOnModeChange} />);

      const sideButton = screen.getByLabelText('Side-by-side view');
      await user.click(sideButton);

      expect(mockOnModeChange).toHaveBeenCalledWith('side-by-side');
      expect(mockOnModeChange).toHaveBeenCalledTimes(1);
    });

    it('should not trigger onModeChange when clicking already active button', async () => {
      const user = userEvent.setup();

      render(<DiffViewModeToggle currentMode="list" onModeChange={mockOnModeChange} />);

      const listButton = screen.getByLabelText('List view');
      await user.click(listButton);

      // shadcn ToggleGroup does not call onChange when clicking already-selected item
      expect(mockOnModeChange).not.toHaveBeenCalled();
    });

    it('should handle rapid mode switching', async () => {
      const user = userEvent.setup();

      const { rerender } = render(<DiffViewModeToggle currentMode="list" onModeChange={mockOnModeChange} />);

      const sideButton = screen.getByLabelText('Side-by-side view');

      // Click to switch to side-by-side
      await user.click(sideButton);
      expect(mockOnModeChange).toHaveBeenNthCalledWith(1, 'side-by-side');

      // Rerender with new mode
      rerender(<DiffViewModeToggle currentMode="side-by-side" onModeChange={mockOnModeChange} />);

      // Click to switch back to list
      const listButton = screen.getByLabelText('List view');
      await user.click(listButton);
      expect(mockOnModeChange).toHaveBeenNthCalledWith(2, 'list');

      // Rerender with list mode
      rerender(<DiffViewModeToggle currentMode="list" onModeChange={mockOnModeChange} />);

      // Click to switch to side-by-side again
      const sideButtonAgain = screen.getByLabelText('Side-by-side view');
      await user.click(sideButtonAgain);

      expect(mockOnModeChange).toHaveBeenCalledTimes(3);
      expect(mockOnModeChange).toHaveBeenNthCalledWith(3, 'side-by-side');
    });
  });

  describe('Accessibility', () => {
    it('should have proper ARIA roles for buttons', () => {
      render(<DiffViewModeToggle currentMode="list" onModeChange={mockOnModeChange} />);

      const listButton = screen.getByLabelText('List view');
      const sideButton = screen.getByLabelText('Side-by-side view');

      // shadcn ToggleGroup items don't use role="radio", they use button role
      expect(listButton).toHaveAttribute('type', 'button');
      expect(sideButton).toHaveAttribute('type', 'button');
    });

    it('should have correct aria-checked attributes', () => {
      render(<DiffViewModeToggle currentMode="list" onModeChange={mockOnModeChange} />);

      // shadcn ToggleGroup uses aria-checked (like radio buttons)
      expect(screen.getByLabelText('List view')).toHaveAttribute('aria-checked', 'true');
      expect(screen.getByLabelText('Side-by-side view')).toHaveAttribute('aria-checked', 'false');
    });

    it('should update aria-checked when mode changes', () => {
      const { rerender } = render(
        <DiffViewModeToggle currentMode="list" onModeChange={mockOnModeChange} />
      );

      expect(screen.getByLabelText('List view')).toHaveAttribute('aria-checked', 'true');

      rerender(<DiffViewModeToggle currentMode="side-by-side" onModeChange={mockOnModeChange} />);

      expect(screen.getByLabelText('List view')).toHaveAttribute('aria-checked', 'false');
      expect(screen.getByLabelText('Side-by-side view')).toHaveAttribute('aria-checked', 'true');
    });

    it('should be keyboard navigable', () => {
      render(<DiffViewModeToggle currentMode="list" onModeChange={mockOnModeChange} />);

      // Radix ToggleGroup is keyboard navigable - verify group has tabindex
      const toggleGroup = screen.getByRole('group');
      expect(toggleGroup).toHaveAttribute('tabindex');

      // Verify buttons are focusable
      const listButton = screen.getByLabelText('List view');
      const sideButton = screen.getByLabelText('Side-by-side view');

      expect(listButton).toBeInTheDocument();
      expect(sideButton).toBeInTheDocument();

      // Active button should have tabindex 0, inactive should have -1 (Radix roving focus)
      expect(listButton).toHaveAttribute('tabindex');
      expect(sideButton).toHaveAttribute('tabindex');
    });

    it('should activate button on Enter key', async () => {
      const user = userEvent.setup();

      render(<DiffViewModeToggle currentMode="list" onModeChange={mockOnModeChange} />);

      const sideButton = screen.getByLabelText('Side-by-side view');
      sideButton.focus();

      await user.keyboard('{Enter}');

      expect(mockOnModeChange).toHaveBeenCalledWith('side-by-side');
    });

    it('should activate button on Space key', async () => {
      const user = userEvent.setup();

      render(<DiffViewModeToggle currentMode="list" onModeChange={mockOnModeChange} />);

      const sideButton = screen.getByLabelText('Side-by-side view');
      sideButton.focus();

      await user.keyboard(' ');

      expect(mockOnModeChange).toHaveBeenCalledWith('side-by-side');
    });
  });

  describe('CSS Classes', () => {
    it('should apply base classes to container', () => {
      const { container } = render(
        <DiffViewModeToggle currentMode="list" onModeChange={mockOnModeChange} />
      );

      expect(container.querySelector('.diff-view-mode-toggle')).toBeInTheDocument();
    });

    it('should have toggle group items', () => {
      render(
        <DiffViewModeToggle currentMode="list" onModeChange={mockOnModeChange} />
      );

      // shadcn ToggleGroup creates buttons, not custom class names
      const listButton = screen.getByLabelText('List view');
      const sideButton = screen.getByLabelText('Side-by-side view');

      expect(listButton).toBeInTheDocument();
      expect(sideButton).toBeInTheDocument();
    });

    it('should toggle active state when mode changes', () => {
      const { rerender } = render(
        <DiffViewModeToggle currentMode="list" onModeChange={mockOnModeChange} />
      );

      const listButton = screen.getByLabelText('List view');
      const sideButton = screen.getByLabelText('Side-by-side view');

      // shadcn uses data-state instead of CSS class modifiers
      expect(listButton).toHaveAttribute('data-state', 'on');
      expect(sideButton).toHaveAttribute('data-state', 'off');

      rerender(<DiffViewModeToggle currentMode="side-by-side" onModeChange={mockOnModeChange} />);

      expect(listButton).toHaveAttribute('data-state', 'off');
      expect(sideButton).toHaveAttribute('data-state', 'on');
    });
  });

  describe('Edge Cases', () => {
    it('should handle mode prop as any string type', () => {
      const mode = 'list' as 'list' | 'side-by-side';

      render(<DiffViewModeToggle currentMode={mode} onModeChange={mockOnModeChange} />);

      // shadcn ToggleGroup uses aria-checked (like radio buttons)
      expect(screen.getByLabelText('List view')).toHaveAttribute('aria-checked', 'true');
    });

    it('should render correctly with TypeScript strict mode', () => {
      const props: React.ComponentProps<typeof DiffViewModeToggle> = {
        currentMode: 'side-by-side',
        onModeChange: mockOnModeChange,
      };

      render(<DiffViewModeToggle {...props} />);

      // shadcn ToggleGroup uses aria-checked (like radio buttons)
      expect(screen.getByLabelText('Side-by-side view')).toHaveAttribute('aria-checked', 'true');
    });
  });
});
