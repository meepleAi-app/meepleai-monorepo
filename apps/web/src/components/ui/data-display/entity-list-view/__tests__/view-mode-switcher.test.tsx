/**
 * Tests for ViewModeSwitcher component
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ViewModeSwitcher } from '../components/view-mode-switcher';
import type { ViewMode } from '../entity-list-view.types';
import { vi } from 'vitest';

describe('ViewModeSwitcher', () => {
  const mockOnChange = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Rendering', () => {
    it('should render all available modes', () => {
      render(
        <ViewModeSwitcher
          value="grid"
          onChange={mockOnChange}
          availableModes={['grid', 'list', 'carousel']}
        />
      );

      expect(screen.getByRole('radio', { name: /grid view/i })).toBeInTheDocument();
      expect(screen.getByRole('radio', { name: /list view/i })).toBeInTheDocument();
      expect(screen.getByRole('radio', { name: /carousel view/i })).toBeInTheDocument();
    });

    it('should render only specified available modes', () => {
      render(
        <ViewModeSwitcher value="grid" onChange={mockOnChange} availableModes={['grid', 'list']} />
      );

      expect(screen.getByRole('radio', { name: /grid view/i })).toBeInTheDocument();
      expect(screen.getByRole('radio', { name: /list view/i })).toBeInTheDocument();
      expect(screen.queryByRole('radio', { name: /carousel view/i })).not.toBeInTheDocument();
    });

    it('should mark active mode with aria-checked="true"', () => {
      render(<ViewModeSwitcher value="list" onChange={mockOnChange} />);

      expect(screen.getByRole('radio', { name: /list view/i })).toHaveAttribute(
        'aria-checked',
        'true'
      );
      expect(screen.getByRole('radio', { name: /grid view/i })).toHaveAttribute(
        'aria-checked',
        'false'
      );
    });

    it('should apply active state styling to current mode', () => {
      render(<ViewModeSwitcher value="grid" onChange={mockOnChange} />);

      const gridButton = screen.getByRole('radio', { name: /grid view/i });

      // Active button should have primary text and background
      expect(gridButton).toHaveClass('text-primary');
      expect(gridButton).toHaveClass('bg-primary/10');
      expect(gridButton).toHaveClass('border-primary');
    });
  });

  describe('Click Interaction', () => {
    it('should call onChange when mode button is clicked', async () => {
      const user = userEvent.setup();

      render(<ViewModeSwitcher value="grid" onChange={mockOnChange} />);

      await user.click(screen.getByRole('radio', { name: /list view/i }));

      expect(mockOnChange).toHaveBeenCalledWith('list');
      expect(mockOnChange).toHaveBeenCalledTimes(1);
    });

    it('should allow clicking active mode (no-op but valid)', async () => {
      const user = userEvent.setup();

      render(<ViewModeSwitcher value="grid" onChange={mockOnChange} />);

      await user.click(screen.getByRole('radio', { name: /grid view/i }));

      expect(mockOnChange).toHaveBeenCalledWith('grid');
    });
  });

  describe('Keyboard Navigation', () => {
    it('should navigate to next mode with ArrowRight', async () => {
      const user = userEvent.setup();

      render(<ViewModeSwitcher value="grid" onChange={mockOnChange} />);

      const activeButton = screen.getByRole('radio', { checked: true });
      activeButton.focus();

      await user.keyboard('{ArrowRight}');

      expect(mockOnChange).toHaveBeenCalledWith('list');
    });

    it('should navigate to previous mode with ArrowLeft', async () => {
      const user = userEvent.setup();

      render(<ViewModeSwitcher value="list" onChange={mockOnChange} />);

      const activeButton = screen.getByRole('radio', { checked: true });
      activeButton.focus();

      await user.keyboard('{ArrowLeft}');

      expect(mockOnChange).toHaveBeenCalledWith('grid');
    });

    it('should wrap around at edges with ArrowRight', async () => {
      const user = userEvent.setup();

      render(<ViewModeSwitcher value="carousel" onChange={mockOnChange} />);

      const activeButton = screen.getByRole('radio', { checked: true });
      activeButton.focus();

      await user.keyboard('{ArrowRight}');

      // Should wrap to first mode
      expect(mockOnChange).toHaveBeenCalledWith('grid');
    });

    it('should wrap around at edges with ArrowLeft', async () => {
      const user = userEvent.setup();

      render(<ViewModeSwitcher value="grid" onChange={mockOnChange} />);

      const activeButton = screen.getByRole('radio', { checked: true });
      activeButton.focus();

      await user.keyboard('{ArrowLeft}');

      // Should wrap to last mode
      expect(mockOnChange).toHaveBeenCalledWith('carousel');
    });

    it('should navigate with ArrowDown (alternative)', async () => {
      const user = userEvent.setup();

      render(<ViewModeSwitcher value="grid" onChange={mockOnChange} />);

      const activeButton = screen.getByRole('radio', { checked: true });
      activeButton.focus();

      await user.keyboard('{ArrowDown}');

      expect(mockOnChange).toHaveBeenCalledWith('list');
    });

    it('should navigate with ArrowUp (alternative)', async () => {
      const user = userEvent.setup();

      render(<ViewModeSwitcher value="list" onChange={mockOnChange} />);

      const activeButton = screen.getByRole('radio', { checked: true });
      activeButton.focus();

      await user.keyboard('{ArrowUp}');

      expect(mockOnChange).toHaveBeenCalledWith('grid');
    });
  });

  describe('Focus Management', () => {
    it('should set tabIndex 0 on active mode button', () => {
      render(<ViewModeSwitcher value="list" onChange={mockOnChange} />);

      const listButton = screen.getByRole('radio', { name: /list view/i });
      const gridButton = screen.getByRole('radio', { name: /grid view/i });

      expect(listButton).toHaveAttribute('tabindex', '0');
      expect(gridButton).toHaveAttribute('tabindex', '-1');
    });

    it('should show focus ring on keyboard focus', async () => {
      const user = userEvent.setup();

      render(<ViewModeSwitcher value="grid" onChange={mockOnChange} />);

      const gridButton = screen.getByRole('radio', { name: /grid view/i });

      await user.tab(); // Focus first button

      expect(gridButton).toHaveFocus();
      // Focus ring is applied via Tailwind focus-visible pseudoclass (visual only)
    });
  });

  describe('Accessibility', () => {
    it('should have radiogroup role', () => {
      render(<ViewModeSwitcher value="grid" onChange={mockOnChange} />);

      expect(screen.getByRole('radiogroup')).toBeInTheDocument();
      expect(screen.getByRole('radiogroup')).toHaveAttribute(
        'aria-label',
        'View mode selection'
      );
    });

    it('should have radio role on mode buttons', () => {
      render(<ViewModeSwitcher value="grid" onChange={mockOnChange} />);

      expect(screen.getAllByRole('radio')).toHaveLength(3);
    });

    it('should have descriptive aria-label for each button', () => {
      render(<ViewModeSwitcher value="grid" onChange={mockOnChange} />);

      expect(screen.getByRole('radio', { name: /grid view/i })).toHaveAttribute(
        'aria-label',
        'Grid view'
      );
      expect(screen.getByRole('radio', { name: /list view/i })).toHaveAttribute(
        'aria-label',
        'List view'
      );
      expect(screen.getByRole('radio', { name: /carousel view/i })).toHaveAttribute(
        'aria-label',
        'Carousel view'
      );
    });

    it('should have aria-hidden on icons', () => {
      render(<ViewModeSwitcher value="grid" onChange={mockOnChange} />);

      const icons = screen.getAllByRole('radio')[0].querySelector('svg');
      expect(icons).toHaveAttribute('aria-hidden', 'true');
    });
  });

  describe('Responsive Behavior', () => {
    it('should render icons for all modes', () => {
      render(<ViewModeSwitcher value="grid" onChange={mockOnChange} />);

      // All buttons should have icons (svgs)
      const buttons = screen.getAllByRole('radio');
      buttons.forEach((button) => {
        expect(button.querySelector('svg')).toBeInTheDocument();
      });
    });

    it('should render labels with sm:inline class', () => {
      const { container } = render(<ViewModeSwitcher value="grid" onChange={mockOnChange} />);

      // Labels should have hidden sm:inline classes (mobile hide, desktop show)
      const labels = container.querySelectorAll('span.hidden.sm\\:inline');
      expect(labels).toHaveLength(3); // One per mode
    });
  });

  describe('Custom Props', () => {
    it('should apply custom className', () => {
      render(
        <ViewModeSwitcher value="grid" onChange={mockOnChange} className="custom-class" />
      );

      expect(screen.getByRole('radiogroup')).toHaveClass('custom-class');
    });

    it('should use custom testId', () => {
      render(
        <ViewModeSwitcher value="grid" onChange={mockOnChange} data-testid="custom-switcher" />
      );

      expect(screen.getByTestId('custom-switcher')).toBeInTheDocument();
    });
  });

  describe('Edge Cases', () => {
    it('should handle empty availableModes array gracefully', () => {
      render(<ViewModeSwitcher value="grid" onChange={mockOnChange} availableModes={[]} />);

      // Should render empty switcher without errors
      expect(screen.getByRole('radiogroup')).toBeInTheDocument();
      expect(screen.queryAllByRole('radio')).toHaveLength(0);
    });

    it('should handle single mode', () => {
      render(<ViewModeSwitcher value="grid" onChange={mockOnChange} availableModes={['grid']} />);

      expect(screen.getAllByRole('radio')).toHaveLength(1);
      expect(screen.getByRole('radio', { name: /grid view/i })).toHaveAttribute(
        'aria-checked',
        'true'
      );
    });
  });
});
