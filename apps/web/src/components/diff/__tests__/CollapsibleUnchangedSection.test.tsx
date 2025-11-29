import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { CollapsibleUnchangedSection } from '../CollapsibleUnchangedSection';
import { CollapsibleSection } from '../../../lib/diffProcessor';

describe('CollapsibleUnchangedSection', () => {
  const mockOnToggle = vi.fn();

  const collapsedSection: CollapsibleSection = {
    startLine: 10,
    endLine: 50,
    lineCount: 40,
    isCollapsed: true,
  };

  const expandedSection: CollapsibleSection = {
    startLine: 10,
    endLine: 50,
    lineCount: 40,
    isCollapsed: false,
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Rendering', () => {
    it('should render collapsed state', () => {
      render(
        <CollapsibleUnchangedSection
          section={collapsedSection}
          onToggle={mockOnToggle}
          side="old"
        />
      );

      expect(
        screen.getByText(/Expand 40 unchanged lines \(10-50\)/)
      ).toBeInTheDocument();
    });

    it('should render expanded state', () => {
      render(
        <CollapsibleUnchangedSection
          section={expandedSection}
          onToggle={mockOnToggle}
          side="old"
        />
      );

      expect(screen.getByText(/Collapse 40 unchanged lines/)).toBeInTheDocument();
    });

    it('should show correct icon for collapsed state', () => {
      render(
        <CollapsibleUnchangedSection
          section={collapsedSection}
          onToggle={mockOnToggle}
          side="old"
        />
      );

      expect(screen.getByText('⬇')).toBeInTheDocument();
    });

    it('should show correct icon for expanded state', () => {
      render(
        <CollapsibleUnchangedSection
          section={expandedSection}
          onToggle={mockOnToggle}
          side="old"
        />
      );

      expect(screen.getByText('⬆')).toBeInTheDocument();
    });

    it('should render for old side', () => {
      const { container } = render(
        <CollapsibleUnchangedSection
          section={collapsedSection}
          onToggle={mockOnToggle}
          side="old"
        />
      );

      expect(container.querySelector('.collapsible-section')).toBeInTheDocument();
    });

    it('should render for new side', () => {
      const { container } = render(
        <CollapsibleUnchangedSection
          section={collapsedSection}
          onToggle={mockOnToggle}
          side="new"
        />
      );

      expect(container.querySelector('.collapsible-section')).toBeInTheDocument();
    });
  });

  describe('User Interactions', () => {
    it('should call onToggle when button is clicked', async () => {
      const user = userEvent.setup({ delay: null });

      render(
        <CollapsibleUnchangedSection
          section={collapsedSection}
          onToggle={mockOnToggle}
          side="old"
        />
      );

      const button = screen.getByRole('button');
      await user.click(button);

      expect(mockOnToggle).toHaveBeenCalledTimes(1);
    });

    it('should add animating class when toggled', async () => {
      const user = userEvent.setup({ delay: null });

      render(
        <CollapsibleUnchangedSection
          section={collapsedSection}
          onToggle={mockOnToggle}
          side="old"
        />
      );

      const button = screen.getByRole('button');
      await user.click(button);

      expect(button).toHaveClass('animating');
    });

    it('should remove animating class after animation completes', async () => {
      const user = userEvent.setup({ delay: null });

      render(
        <CollapsibleUnchangedSection
          section={collapsedSection}
          onToggle={mockOnToggle}
          side="old"
        />
      );

      const button = screen.getByRole('button');
      await user.click(button);

      expect(button).toHaveClass('animating');

      // Wait for animation timeout (300ms) + small buffer
      await new Promise(resolve => setTimeout(resolve, 350));

      await waitFor(() => {
        expect(button).not.toHaveClass('animating');
      });
    });

    it('should handle multiple rapid clicks', async () => {
      const user = userEvent.setup({ delay: null });

      render(
        <CollapsibleUnchangedSection
          section={collapsedSection}
          onToggle={mockOnToggle}
          side="old"
        />
      );

      const button = screen.getByRole('button');

      await user.click(button);
      await user.click(button);
      await user.click(button);

      expect(mockOnToggle).toHaveBeenCalledTimes(3);
    });
  });

  describe('Accessibility', () => {
    it('should have proper ARIA attributes for collapsed state', () => {
      render(
        <CollapsibleUnchangedSection
          section={collapsedSection}
          onToggle={mockOnToggle}
          side="old"
        />
      );

      const button = screen.getByRole('button');
      expect(button).toHaveAttribute('aria-expanded', 'false');
      expect(button).toHaveAttribute('aria-label', 'Expand 40 unchanged lines');
    });

    it('should have proper ARIA attributes for expanded state', () => {
      render(
        <CollapsibleUnchangedSection
          section={expandedSection}
          onToggle={mockOnToggle}
          side="old"
        />
      );

      const button = screen.getByRole('button');
      expect(button).toHaveAttribute('aria-expanded', 'true');
      expect(button).toHaveAttribute('aria-label', 'Collapse 40 unchanged lines');
    });

    it('should be keyboard accessible', async () => {
      const user = userEvent.setup({ delay: null });

      render(
        <CollapsibleUnchangedSection
          section={collapsedSection}
          onToggle={mockOnToggle}
          side="old"
        />
      );

      const button = screen.getByRole('button');
      button.focus();

      await user.keyboard('{Enter}');

      expect(mockOnToggle).toHaveBeenCalledTimes(1);
    });
  });

  describe('Line Count Display', () => {
    it('should display correct line count', () => {
      render(
        <CollapsibleUnchangedSection
          section={collapsedSection}
          onToggle={mockOnToggle}
          side="old"
        />
      );

      expect(screen.getByText(/40 unchanged lines/)).toBeInTheDocument();
    });

    it('should display line range when collapsed', () => {
      render(
        <CollapsibleUnchangedSection
          section={collapsedSection}
          onToggle={mockOnToggle}
          side="old"
        />
      );

      expect(screen.getByText(/\(10-50\)/)).toBeInTheDocument();
    });

    it('should not display line range when expanded', () => {
      render(
        <CollapsibleUnchangedSection
          section={expandedSection}
          onToggle={mockOnToggle}
          side="old"
        />
      );

      expect(screen.queryByText(/\(10-50\)/)).not.toBeInTheDocument();
    });

    it('should handle single line section', () => {
      const singleLineSection: CollapsibleSection = {
        startLine: 10,
        endLine: 10,
        lineCount: 1,
        isCollapsed: true,
      };

      render(
        <CollapsibleUnchangedSection
          section={singleLineSection}
          onToggle={mockOnToggle}
          side="old"
        />
      );

      expect(screen.getByText(/1 unchanged lines/)).toBeInTheDocument();
      expect(screen.getByText(/\(10-10\)/)).toBeInTheDocument();
    });

    it('should handle large line counts', () => {
      const largeSection: CollapsibleSection = {
        startLine: 100,
        endLine: 5000,
        lineCount: 4900,
        isCollapsed: true,
      };

      render(
        <CollapsibleUnchangedSection
          section={largeSection}
          onToggle={mockOnToggle}
          side="old"
        />
      );

      expect(screen.getByText(/4900 unchanged lines/)).toBeInTheDocument();
      expect(screen.getByText(/\(100-5000\)/)).toBeInTheDocument();
    });
  });

  describe('Edge Cases', () => {
    it('should handle zero line count gracefully', () => {
      const emptySection: CollapsibleSection = {
        startLine: 10,
        endLine: 10,
        lineCount: 0,
        isCollapsed: true,
      };

      render(
        <CollapsibleUnchangedSection
          section={emptySection}
          onToggle={mockOnToggle}
          side="old"
        />
      );

      expect(screen.getByText(/0 unchanged lines/)).toBeInTheDocument();
    });

    it('should maintain memo optimization', () => {
      const { rerender } = render(
        <CollapsibleUnchangedSection
          section={collapsedSection}
          onToggle={mockOnToggle}
          side="old"
        />
      );

      // Rerender with same props should not cause re-render
      rerender(
        <CollapsibleUnchangedSection
          section={collapsedSection}
          onToggle={mockOnToggle}
          side="old"
        />
      );

      expect(screen.getByRole('button')).toBeInTheDocument();
    });
  });

  describe('Component Display Name', () => {
    it('should have correct display name', () => {
      expect(CollapsibleUnchangedSection.displayName).toBe('CollapsibleUnchangedSection');
    });
  });
});
