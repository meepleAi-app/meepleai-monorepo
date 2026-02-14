/**
 * LibraryDropdown Component Tests (Issue #4064)
 *
 * Tests for Library dropdown navigation with Collezione and Giochi Privati sub-items.
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { usePathname } from 'next/navigation';
import { describe, it, expect, vi, beforeEach, Mock } from 'vitest';
import { axe } from 'jest-axe';

import { LibraryDropdown } from '../LibraryDropdown';

// Mock Next.js navigation
vi.mock('next/navigation', () => ({
  usePathname: vi.fn(),
}));

const mockUsePathname = usePathname as Mock;

describe('LibraryDropdown', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockUsePathname.mockReturnValue('/dashboard');
  });

  describe('Rendering', () => {
    it('renders dropdown trigger with Libreria label', () => {
      render(<LibraryDropdown />);
      expect(screen.getByText('Libreria')).toBeInTheDocument();
    });

    it('renders BookOpen icon', () => {
      const { container } = render(<LibraryDropdown />);
      const trigger = screen.getByTestId('library-dropdown-trigger');
      expect(trigger.querySelector('svg')).toBeInTheDocument();
    });

    it('has chevron down icon', () => {
      render(<LibraryDropdown />);
      const trigger = screen.getByTestId('library-dropdown-trigger');
      expect(trigger).toBeInTheDocument();
    });
  });

  describe('Dropdown Interaction', () => {
    it('opens dropdown on click', async () => {
      const user = userEvent.setup();
      render(<LibraryDropdown />);

      await user.click(screen.getByTestId('library-dropdown-trigger'));

      expect(screen.getByText('Collezione')).toBeInTheDocument();
      expect(screen.getByText('Giochi Privati')).toBeInTheDocument();
    });

    it('shows both sub-items when opened', async () => {
      const user = userEvent.setup();
      render(<LibraryDropdown />);

      await user.click(screen.getByTestId('library-dropdown-trigger'));

      expect(screen.getByTestId('library-item-collection')).toBeInTheDocument();
      expect(screen.getByTestId('library-item-private')).toBeInTheDocument();
    });

    it('closes dropdown on item click', async () => {
      const user = userEvent.setup();
      render(<LibraryDropdown />);

      await user.click(screen.getByTestId('library-dropdown-trigger'));
      await user.click(screen.getByText('Collezione'));

      // Dropdown should close (items disappear)
      expect(screen.queryByText('Giochi Privati')).not.toBeInTheDocument();
    });

    it('closes on Escape key', async () => {
      const user = userEvent.setup();
      render(<LibraryDropdown />);

      await user.click(screen.getByTestId('library-dropdown-trigger'));
      expect(screen.getByText('Collezione')).toBeInTheDocument();

      await user.keyboard('{Escape}');
      expect(screen.queryByText('Collezione')).not.toBeInTheDocument();
    });
  });

  describe('Active State', () => {
    it('highlights trigger when on /library route', () => {
      mockUsePathname.mockReturnValue('/library');
      render(<LibraryDropdown />);

      const trigger = screen.getByTestId('library-dropdown-trigger');
      expect(trigger).toHaveClass('text-[hsl(262_83%_62%)]');
    });

    it('highlights trigger when on /library/private route', () => {
      mockUsePathname.mockReturnValue('/library/private');
      render(<LibraryDropdown />);

      const trigger = screen.getByTestId('library-dropdown-trigger');
      expect(trigger).toHaveClass('text-[hsl(262_83%_62%)]');
    });

    it('highlights Collezione when active', async () => {
      mockUsePathname.mockReturnValue('/library');
      const user = userEvent.setup();
      render(<LibraryDropdown />);

      await user.click(screen.getByTestId('library-dropdown-trigger'));

      const collectionLink = screen.getByTestId('library-item-collection');
      expect(collectionLink).toHaveClass('text-[hsl(262_83%_62%)]');
      expect(collectionLink).toHaveAttribute('aria-current', 'page');
    });

    it('highlights Giochi Privati when active', async () => {
      mockUsePathname.mockReturnValue('/library/private');
      const user = userEvent.setup();
      render(<LibraryDropdown />);

      await user.click(screen.getByTestId('library-dropdown-trigger'));

      const privateLink = screen.getByTestId('library-item-private');
      expect(privateLink).toHaveClass('text-[hsl(262_83%_62%)]');
      expect(privateLink).toHaveAttribute('aria-current', 'page');
    });

    it('does not highlight when on different route', () => {
      mockUsePathname.mockReturnValue('/dashboard');
      render(<LibraryDropdown />);

      const trigger = screen.getByTestId('library-dropdown-trigger');
      expect(trigger).not.toHaveClass('text-[hsl(262_83%_62%)]');
      expect(trigger).toHaveClass('text-muted-foreground');
    });
  });

  describe('Accessibility', () => {
    it('has no axe violations when closed', async () => {
      const { container } = render(<LibraryDropdown />);
      const results = await axe(container);
      expect(results).toHaveNoViolations();
    });

    it('has no axe violations when open', async () => {
      const user = userEvent.setup();
      const { container } = render(<LibraryDropdown />);

      await user.click(screen.getByTestId('library-dropdown-trigger'));

      const results = await axe(container);
      expect(results).toHaveNoViolations();
    });

    it('has correct ARIA attributes on trigger', () => {
      render(<LibraryDropdown />);
      const trigger = screen.getByTestId('library-dropdown-trigger');

      expect(trigger).toHaveAttribute('aria-label', 'Library navigation menu');
      expect(trigger).toHaveAttribute('aria-expanded', 'false');
      expect(trigger).toHaveAttribute('aria-haspopup', 'menu');
    });

    it('updates aria-expanded when opened', async () => {
      const user = userEvent.setup();
      render(<LibraryDropdown />);

      const trigger = screen.getByTestId('library-dropdown-trigger');
      await user.click(trigger);

      expect(trigger).toHaveAttribute('aria-expanded', 'true');
    });

    it('has aria-label on each sub-item', async () => {
      const user = userEvent.setup();
      render(<LibraryDropdown />);

      await user.click(screen.getByTestId('library-dropdown-trigger'));

      expect(screen.getByLabelText('Navigate to Collezione')).toBeInTheDocument();
      expect(screen.getByLabelText('Navigate to Giochi Privati')).toBeInTheDocument();
    });

    it('chevron icon is aria-hidden', () => {
      const { container } = render(<LibraryDropdown />);
      const chevrons = container.querySelectorAll('svg[aria-hidden="true"]');
      expect(chevrons.length).toBeGreaterThan(0);
    });
  });

  describe('Keyboard Navigation', () => {
    it('opens on Enter key', async () => {
      const user = userEvent.setup();
      render(<LibraryDropdown />);

      const trigger = screen.getByTestId('library-dropdown-trigger');
      trigger.focus();
      await user.keyboard('{Enter}');

      expect(screen.getByText('Collezione')).toBeInTheDocument();
    });

    it('navigates between items with Arrow keys', async () => {
      const user = userEvent.setup();
      render(<LibraryDropdown />);

      await user.click(screen.getByTestId('library-dropdown-trigger'));

      // ArrowDown should move focus
      await user.keyboard('{ArrowDown}');
      expect(screen.getByText('Collezione').closest('a')).toHaveFocus();
    });
  });

  describe('Styling', () => {
    it('shows orange hover state on inactive trigger', () => {
      mockUsePathname.mockReturnValue('/dashboard');
      render(<LibraryDropdown />);

      const trigger = screen.getByTestId('library-dropdown-trigger');
      expect(trigger).toHaveClass('hover:text-primary');
    });

    it('rotates chevron icon when open', async () => {
      const user = userEvent.setup();
      const { container } = render(<LibraryDropdown />);

      await user.click(screen.getByTestId('library-dropdown-trigger'));

      const chevron = container.querySelector('svg[class*="rotate-180"]');
      expect(chevron).toBeInTheDocument();
    });
  });
});
