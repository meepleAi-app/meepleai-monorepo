/**
 * Unit tests for DiffViewModeToggle component
 * Tests toggle between list and side-by-side views
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { DiffViewModeToggle } from '../DiffViewModeToggle';

describe('DiffViewModeToggle', () => {
  const mockOnModeChange = jest.fn();

  beforeEach(() => {
    jest.clearAllMocks();
  });

  describe('Rendering', () => {
    it('should render both view mode buttons', () => {
      render(<DiffViewModeToggle currentMode="list" onModeChange={mockOnModeChange} />);

      expect(screen.getByLabelText('List view')).toBeInTheDocument();
      expect(screen.getByLabelText('Side-by-side view')).toBeInTheDocument();
    });

    it('should have radiogroup role', () => {
      render(<DiffViewModeToggle currentMode="list" onModeChange={mockOnModeChange} />);

      expect(screen.getByRole('radiogroup', { name: 'Diff view mode' })).toBeInTheDocument();
    });

    it('should display correct text and icons', () => {
      render(<DiffViewModeToggle currentMode="list" onModeChange={mockOnModeChange} />);

      expect(screen.getByText('📋 List')).toBeInTheDocument();
      expect(screen.getByText('⇄ Side-by-Side')).toBeInTheDocument();
    });
  });

  describe('Active State - List Mode', () => {
    it('should mark list button as active when currentMode is list', () => {
      render(<DiffViewModeToggle currentMode="list" onModeChange={mockOnModeChange} />);

      const listButton = screen.getByLabelText('List view');

      expect(listButton).toHaveClass('view-mode-button--active');
      expect(listButton).toHaveAttribute('aria-checked', 'true');
    });

    it('should mark side-by-side button as inactive when currentMode is list', () => {
      render(<DiffViewModeToggle currentMode="list" onModeChange={mockOnModeChange} />);

      const sideButton = screen.getByLabelText('Side-by-side view');

      expect(sideButton).not.toHaveClass('view-mode-button--active');
      expect(sideButton).toHaveAttribute('aria-checked', 'false');
    });
  });

  describe('Active State - Side-by-Side Mode', () => {
    it('should mark side-by-side button as active when currentMode is side-by-side', () => {
      render(<DiffViewModeToggle currentMode="side-by-side" onModeChange={mockOnModeChange} />);

      const sideButton = screen.getByLabelText('Side-by-side view');

      expect(sideButton).toHaveClass('view-mode-button--active');
      expect(sideButton).toHaveAttribute('aria-checked', 'true');
    });

    it('should mark list button as inactive when currentMode is side-by-side', () => {
      render(<DiffViewModeToggle currentMode="side-by-side" onModeChange={mockOnModeChange} />);

      const listButton = screen.getByLabelText('List view');

      expect(listButton).not.toHaveClass('view-mode-button--active');
      expect(listButton).toHaveAttribute('aria-checked', 'false');
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

    it('should allow clicking already active button', async () => {
      const user = userEvent.setup();

      render(<DiffViewModeToggle currentMode="list" onModeChange={mockOnModeChange} />);

      const listButton = screen.getByLabelText('List view');
      await user.click(listButton);

      expect(mockOnModeChange).toHaveBeenCalledWith('list');
    });

    it('should handle rapid mode switching', async () => {
      const user = userEvent.setup();

      render(<DiffViewModeToggle currentMode="list" onModeChange={mockOnModeChange} />);

      const listButton = screen.getByLabelText('List view');
      const sideButton = screen.getByLabelText('Side-by-side view');

      await user.click(sideButton);
      await user.click(listButton);
      await user.click(sideButton);

      expect(mockOnModeChange).toHaveBeenCalledTimes(3);
      expect(mockOnModeChange).toHaveBeenNthCalledWith(1, 'side-by-side');
      expect(mockOnModeChange).toHaveBeenNthCalledWith(2, 'list');
      expect(mockOnModeChange).toHaveBeenNthCalledWith(3, 'side-by-side');
    });
  });

  describe('Accessibility', () => {
    it('should have proper ARIA roles for buttons', () => {
      render(<DiffViewModeToggle currentMode="list" onModeChange={mockOnModeChange} />);

      const listButton = screen.getByLabelText('List view');
      const sideButton = screen.getByLabelText('Side-by-side view');

      expect(listButton).toHaveAttribute('role', 'radio');
      expect(sideButton).toHaveAttribute('role', 'radio');
    });

    it('should have correct aria-checked attributes', () => {
      render(<DiffViewModeToggle currentMode="list" onModeChange={mockOnModeChange} />);

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

    it('should be keyboard navigable', async () => {
      const user = userEvent.setup();

      render(<DiffViewModeToggle currentMode="list" onModeChange={mockOnModeChange} />);

      const listButton = screen.getByLabelText('List view');

      listButton.focus();
      expect(listButton).toHaveFocus();

      await user.keyboard('{Tab}');

      const sideButton = screen.getByLabelText('Side-by-side view');
      expect(sideButton).toHaveFocus();
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

    it('should apply view-mode-button class to both buttons', () => {
      const { container } = render(
        <DiffViewModeToggle currentMode="list" onModeChange={mockOnModeChange} />
      );

      const buttons = container.querySelectorAll('.view-mode-button');

      expect(buttons).toHaveLength(2);
    });

    it('should toggle active class when mode changes', () => {
      const { rerender } = render(
        <DiffViewModeToggle currentMode="list" onModeChange={mockOnModeChange} />
      );

      const listButton = screen.getByLabelText('List view');
      const sideButton = screen.getByLabelText('Side-by-side view');

      expect(listButton).toHaveClass('view-mode-button--active');
      expect(sideButton).not.toHaveClass('view-mode-button--active');

      rerender(<DiffViewModeToggle currentMode="side-by-side" onModeChange={mockOnModeChange} />);

      expect(listButton).not.toHaveClass('view-mode-button--active');
      expect(sideButton).toHaveClass('view-mode-button--active');
    });
  });

  describe('Edge Cases', () => {
    it('should handle mode prop as any string type', () => {
      const mode = 'list' as 'list' | 'side-by-side';

      render(<DiffViewModeToggle currentMode={mode} onModeChange={mockOnModeChange} />);

      expect(screen.getByLabelText('List view')).toHaveAttribute('aria-checked', 'true');
    });

    it('should render correctly with TypeScript strict mode', () => {
      const props: React.ComponentProps<typeof DiffViewModeToggle> = {
        currentMode: 'side-by-side',
        onModeChange: mockOnModeChange,
      };

      render(<DiffViewModeToggle {...props} />);

      expect(screen.getByLabelText('Side-by-side view')).toHaveAttribute('aria-checked', 'true');
    });
  });
});
