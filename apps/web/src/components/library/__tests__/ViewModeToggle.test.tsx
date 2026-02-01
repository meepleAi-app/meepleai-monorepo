/**
 * ViewModeToggle Component Tests (Issue #2866)
 *
 * Test Coverage:
 * - Rendering grid and list buttons
 * - Active state visual feedback
 * - Click handlers and mode switching
 * - Accessibility attributes (aria-label, aria-pressed)
 * - Custom className support
 *
 * Target: >= 90% coverage
 */

import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { ViewModeToggle, type ViewModeToggleProps, type ViewMode } from '../ViewModeToggle';

// ============================================================================
// Test Data Factory
// ============================================================================

const createProps = (overrides: Partial<ViewModeToggleProps> = {}): ViewModeToggleProps => ({
  viewMode: 'grid',
  onViewModeChange: vi.fn(),
  ...overrides,
});

// ============================================================================
// Rendering Tests
// ============================================================================

describe('ViewModeToggle', () => {
  describe('Rendering', () => {
    it('should render grid and list buttons', () => {
      render(<ViewModeToggle {...createProps()} />);

      expect(screen.getByLabelText('Vista a griglia')).toBeInTheDocument();
      expect(screen.getByLabelText('Vista a lista')).toBeInTheDocument();
    });

    it('should apply custom className', () => {
      const { container } = render(
        <ViewModeToggle {...createProps({ className: 'custom-class' })} />
      );

      expect(container.firstChild).toHaveClass('custom-class');
    });

    it('should render with container styling', () => {
      const { container } = render(<ViewModeToggle {...createProps()} />);

      expect(container.firstChild).toHaveClass('flex', 'items-center', 'gap-1', 'rounded-lg', 'border', 'p-1');
    });
  });

  // ============================================================================
  // Active State Tests
  // ============================================================================

  describe('Active State', () => {
    it('should show grid button as active when viewMode is grid', () => {
      render(<ViewModeToggle {...createProps({ viewMode: 'grid' })} />);

      const gridButton = screen.getByLabelText('Vista a griglia');
      const listButton = screen.getByLabelText('Vista a lista');

      expect(gridButton).toHaveAttribute('aria-pressed', 'true');
      expect(listButton).toHaveAttribute('aria-pressed', 'false');
    });

    it('should show list button as active when viewMode is list', () => {
      render(<ViewModeToggle {...createProps({ viewMode: 'list' })} />);

      const gridButton = screen.getByLabelText('Vista a griglia');
      const listButton = screen.getByLabelText('Vista a lista');

      expect(gridButton).toHaveAttribute('aria-pressed', 'false');
      expect(listButton).toHaveAttribute('aria-pressed', 'true');
    });
  });

  // ============================================================================
  // Interaction Tests
  // ============================================================================

  describe('Interactions', () => {
    it('should call onViewModeChange with "grid" when grid button is clicked', () => {
      const onViewModeChange = vi.fn();
      render(
        <ViewModeToggle {...createProps({ viewMode: 'list', onViewModeChange })} />
      );

      fireEvent.click(screen.getByLabelText('Vista a griglia'));

      expect(onViewModeChange).toHaveBeenCalledWith('grid');
      expect(onViewModeChange).toHaveBeenCalledTimes(1);
    });

    it('should call onViewModeChange with "list" when list button is clicked', () => {
      const onViewModeChange = vi.fn();
      render(
        <ViewModeToggle {...createProps({ viewMode: 'grid', onViewModeChange })} />
      );

      fireEvent.click(screen.getByLabelText('Vista a lista'));

      expect(onViewModeChange).toHaveBeenCalledWith('list');
      expect(onViewModeChange).toHaveBeenCalledTimes(1);
    });

    it('should still call onViewModeChange when clicking already active mode', () => {
      const onViewModeChange = vi.fn();
      render(
        <ViewModeToggle {...createProps({ viewMode: 'grid', onViewModeChange })} />
      );

      fireEvent.click(screen.getByLabelText('Vista a griglia'));

      expect(onViewModeChange).toHaveBeenCalledWith('grid');
    });
  });

  // ============================================================================
  // Accessibility Tests
  // ============================================================================

  describe('Accessibility', () => {
    it('should have correct aria-label for grid button', () => {
      render(<ViewModeToggle {...createProps()} />);

      expect(screen.getByLabelText('Vista a griglia')).toBeInTheDocument();
    });

    it('should have correct aria-label for list button', () => {
      render(<ViewModeToggle {...createProps()} />);

      expect(screen.getByLabelText('Vista a lista')).toBeInTheDocument();
    });

    it('should have aria-pressed attribute on buttons', () => {
      render(<ViewModeToggle {...createProps({ viewMode: 'grid' })} />);

      const gridButton = screen.getByLabelText('Vista a griglia');
      const listButton = screen.getByLabelText('Vista a lista');

      expect(gridButton).toHaveAttribute('aria-pressed');
      expect(listButton).toHaveAttribute('aria-pressed');
    });

    it('should be keyboard accessible', () => {
      const onViewModeChange = vi.fn();
      render(
        <ViewModeToggle {...createProps({ viewMode: 'grid', onViewModeChange })} />
      );

      const listButton = screen.getByLabelText('Vista a lista');
      listButton.focus();
      fireEvent.keyDown(listButton, { key: 'Enter' });
      fireEvent.click(listButton);

      expect(onViewModeChange).toHaveBeenCalledWith('list');
    });
  });

  // ============================================================================
  // Type Safety Tests
  // ============================================================================

  describe('Type Safety', () => {
    it('should accept valid ViewMode values', () => {
      const modes: ViewMode[] = ['grid', 'list'];

      modes.forEach(mode => {
        const { unmount } = render(
          <ViewModeToggle {...createProps({ viewMode: mode })} />
        );
        expect(screen.getByLabelText('Vista a griglia')).toBeInTheDocument();
        unmount();
      });
    });
  });
});
