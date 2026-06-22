/**
 * DirtyStateBar Component Tests (Issue #1836)
 *
 * Render rules, click handlers, loading state, and accessibility.
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, beforeEach, vi } from 'vitest';

import { DirtyStateBar } from '../DirtyStateBar';

describe('DirtyStateBar', () => {
  const mockRevert = vi.fn();
  const mockApply = vi.fn();
  const mockPreview = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Conditional rendering', () => {
    it('renders nothing when dirtyCount is 0', () => {
      const { container } = render(
        <DirtyStateBar dirtyCount={0} onRevert={mockRevert} onApply={mockApply} />
      );

      expect(container.firstChild).toBeNull();
    });

    it('renders nothing when dirtyCount is negative', () => {
      const { container } = render(
        <DirtyStateBar dirtyCount={-1} onRevert={mockRevert} onApply={mockApply} />
      );

      expect(container.firstChild).toBeNull();
    });

    it('renders the bar when dirtyCount > 0', () => {
      render(<DirtyStateBar dirtyCount={2} onRevert={mockRevert} onApply={mockApply} />);

      expect(screen.getByTestId('dirty-state-bar')).toBeInTheDocument();
      expect(screen.getByText('2 unsaved changes')).toBeInTheDocument();
    });

    it('uses singular label when dirtyCount is 1', () => {
      render(<DirtyStateBar dirtyCount={1} onRevert={mockRevert} onApply={mockApply} />);

      expect(screen.getByText('1 unsaved change')).toBeInTheDocument();
    });

    it('uses custom item labels when provided', () => {
      render(
        <DirtyStateBar
          dirtyCount={3}
          onRevert={mockRevert}
          onApply={mockApply}
          itemLabel={{ singular: 'flag', plural: 'flags' }}
        />
      );

      expect(screen.getByText('3 unsaved flags')).toBeInTheDocument();
    });
  });

  describe('Action buttons', () => {
    it('renders Discard and Apply buttons by default', () => {
      render(<DirtyStateBar dirtyCount={1} onRevert={mockRevert} onApply={mockApply} />);

      expect(screen.getByTestId('dirty-state-bar-revert')).toBeInTheDocument();
      expect(screen.getByTestId('dirty-state-bar-apply')).toBeInTheDocument();
    });

    it('hides Preview button when onPreview is not provided', () => {
      render(<DirtyStateBar dirtyCount={1} onRevert={mockRevert} onApply={mockApply} />);

      expect(screen.queryByTestId('dirty-state-bar-preview')).not.toBeInTheDocument();
    });

    it('shows Preview button when onPreview is provided', () => {
      render(
        <DirtyStateBar
          dirtyCount={1}
          onRevert={mockRevert}
          onApply={mockApply}
          onPreview={mockPreview}
        />
      );

      expect(screen.getByTestId('dirty-state-bar-preview')).toBeInTheDocument();
    });

    it('calls onRevert when Discard is clicked', async () => {
      const user = userEvent.setup();

      render(<DirtyStateBar dirtyCount={1} onRevert={mockRevert} onApply={mockApply} />);

      await user.click(screen.getByTestId('dirty-state-bar-revert'));

      expect(mockRevert).toHaveBeenCalledTimes(1);
      expect(mockApply).not.toHaveBeenCalled();
    });

    it('calls onApply when Apply is clicked', async () => {
      const user = userEvent.setup();

      render(<DirtyStateBar dirtyCount={1} onRevert={mockRevert} onApply={mockApply} />);

      await user.click(screen.getByTestId('dirty-state-bar-apply'));

      expect(mockApply).toHaveBeenCalledTimes(1);
      expect(mockRevert).not.toHaveBeenCalled();
    });

    it('calls onPreview when Preview is clicked', async () => {
      const user = userEvent.setup();

      render(
        <DirtyStateBar
          dirtyCount={1}
          onRevert={mockRevert}
          onApply={mockApply}
          onPreview={mockPreview}
        />
      );

      await user.click(screen.getByTestId('dirty-state-bar-preview'));

      expect(mockPreview).toHaveBeenCalledTimes(1);
    });
  });

  describe('Applying state', () => {
    it('disables all buttons when applying', () => {
      render(
        <DirtyStateBar
          dirtyCount={2}
          onRevert={mockRevert}
          onApply={mockApply}
          onPreview={mockPreview}
          applying
        />
      );

      expect(screen.getByTestId('dirty-state-bar-revert')).toBeDisabled();
      expect(screen.getByTestId('dirty-state-bar-preview')).toBeDisabled();
      expect(screen.getByTestId('dirty-state-bar-apply')).toBeDisabled();
    });

    it('swaps Apply label for Applying… and exposes aria-busy', () => {
      render(<DirtyStateBar dirtyCount={1} onRevert={mockRevert} onApply={mockApply} applying />);

      const applyBtn = screen.getByTestId('dirty-state-bar-apply');
      expect(applyBtn).toHaveTextContent(/Applying/);
      expect(applyBtn).toHaveAttribute('aria-busy', 'true');
    });

    it('does not invoke handlers while applying', async () => {
      const user = userEvent.setup();

      render(<DirtyStateBar dirtyCount={1} onRevert={mockRevert} onApply={mockApply} applying />);

      await user.click(screen.getByTestId('dirty-state-bar-revert'));
      await user.click(screen.getByTestId('dirty-state-bar-apply'));

      expect(mockRevert).not.toHaveBeenCalled();
      expect(mockApply).not.toHaveBeenCalled();
    });
  });

  describe('Accessibility', () => {
    it('exposes a region landmark with descriptive label', () => {
      render(<DirtyStateBar dirtyCount={1} onRevert={mockRevert} onApply={mockApply} />);

      const region = screen.getByRole('region', { name: /unsaved changes/i });
      expect(region).toBeInTheDocument();
    });

    it('provides aria-labels on every action button', () => {
      render(
        <DirtyStateBar
          dirtyCount={1}
          onRevert={mockRevert}
          onApply={mockApply}
          onPreview={mockPreview}
        />
      );

      expect(screen.getByTestId('dirty-state-bar-revert')).toHaveAttribute('aria-label');
      expect(screen.getByTestId('dirty-state-bar-preview')).toHaveAttribute('aria-label');
      expect(screen.getByTestId('dirty-state-bar-apply')).toHaveAttribute('aria-label');
    });
  });
});
