import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { DashboardNav } from '../DashboardNav';
import type { NavGroup } from '../DashboardSidebar';

// Mock framer-motion
vi.mock('framer-motion', () => ({
  motion: {
    div: ({ children, ...props }: React.PropsWithChildren<Record<string, unknown>>) => (
      <div {...props}>{children}</div>
    ),
  },
  AnimatePresence: ({ children }: React.PropsWithChildren) => <>{children}</>,
}));

// Mock scrollToSection
vi.mock('../hooks/useScrollSpy', () => ({
  scrollToSection: vi.fn(),
}));

const mockGroups: NavGroup[] = [
  {
    id: 'understand',
    label: 'Understand',
    icon: '🎓',
    sections: [
      { id: 'overview', label: 'Overview' },
      { id: 'architecture', label: 'Architecture' },
    ],
  },
  {
    id: 'explore',
    label: 'Explore',
    icon: '🔍',
    sections: [
      { id: 'query-sim', label: 'Query Simulator' },
      { id: 'token-flow', label: 'Token Flow' },
    ],
  },
];

describe('DashboardNav', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Rendering', () => {
    it('should render current section label', () => {
      render(<DashboardNav groups={mockGroups} activeSection="architecture" />);

      expect(screen.getByText('Architecture')).toBeInTheDocument();
    });

    it('should render current group icon', () => {
      render(<DashboardNav groups={mockGroups} activeSection="overview" />);

      // The icon should be visible in the toggle button
      expect(screen.getByText('🎓')).toBeInTheDocument();
    });

    it('should show default label when no active section', () => {
      render(<DashboardNav groups={mockGroups} activeSection="" />);

      expect(screen.getByText('Navigate')).toBeInTheDocument();
    });
  });

  describe('Dropdown', () => {
    it('should open dropdown on button click', () => {
      render(<DashboardNav groups={mockGroups} activeSection="overview" />);

      const toggleButton = screen.getByRole('button', { name: /overview/i });
      fireEvent.click(toggleButton);

      // Dropdown should show all groups
      expect(screen.getByRole('menu')).toBeInTheDocument();
    });

    it('should close dropdown on section select', async () => {
      const { scrollToSection } = await import('../hooks/useScrollSpy');

      render(<DashboardNav groups={mockGroups} activeSection="overview" />);

      // Open dropdown
      const toggleButton = screen.getByRole('button', { name: /overview/i });
      fireEvent.click(toggleButton);

      // Click a section
      const sectionButton = screen.getByRole('menuitem', { name: 'Architecture' });
      fireEvent.click(sectionButton);

      expect(scrollToSection).toHaveBeenCalledWith('architecture');
    });

    it('should close dropdown on backdrop click', () => {
      render(<DashboardNav groups={mockGroups} activeSection="overview" />);

      // Open dropdown
      const toggleButton = screen.getByRole('button', { name: /overview/i });
      fireEvent.click(toggleButton);

      expect(screen.getByRole('menu')).toBeInTheDocument();

      // Click backdrop (the div with aria-hidden)
      const backdrop = document.querySelector('[aria-hidden="true"]');
      if (backdrop) {
        fireEvent.click(backdrop);
      }

      // Toggle button should indicate closed state
      expect(toggleButton).toHaveAttribute('aria-expanded', 'false');
    });

    it('should show all groups and sections in dropdown', () => {
      render(<DashboardNav groups={mockGroups} activeSection="overview" />);

      // Open dropdown
      const toggleButton = screen.getByRole('button', { name: /overview/i });
      fireEvent.click(toggleButton);

      // Check all groups are visible
      expect(screen.getByText('Understand')).toBeInTheDocument();
      expect(screen.getByText('Explore')).toBeInTheDocument();

      // Check all sections are visible
      expect(screen.getByRole('menuitem', { name: 'Overview' })).toBeInTheDocument();
      expect(screen.getByRole('menuitem', { name: 'Architecture' })).toBeInTheDocument();
      expect(screen.getByRole('menuitem', { name: 'Query Simulator' })).toBeInTheDocument();
      expect(screen.getByRole('menuitem', { name: 'Token Flow' })).toBeInTheDocument();
    });
  });

  describe('Active section', () => {
    it('should highlight active section in dropdown', () => {
      render(<DashboardNav groups={mockGroups} activeSection="architecture" />);

      // Open dropdown
      const toggleButton = screen.getByRole('button', { name: /architecture/i });
      fireEvent.click(toggleButton);

      const activeSection = screen.getByRole('menuitem', { name: 'Architecture' });
      expect(activeSection).toHaveAttribute('aria-current', 'true');
    });

    it('should update displayed label when active changes', () => {
      const { rerender } = render(
        <DashboardNav groups={mockGroups} activeSection="overview" />
      );

      expect(screen.getByText('Overview')).toBeInTheDocument();

      rerender(<DashboardNav groups={mockGroups} activeSection="architecture" />);

      expect(screen.getByText('Architecture')).toBeInTheDocument();
    });
  });

  describe('Accessibility', () => {
    it('should have navigation role', () => {
      render(<DashboardNav groups={mockGroups} activeSection="overview" />);

      expect(screen.getByRole('navigation')).toBeInTheDocument();
    });

    it('should have aria-label for mobile navigation', () => {
      render(<DashboardNav groups={mockGroups} activeSection="overview" />);

      expect(
        screen.getByRole('navigation', { name: 'Mobile dashboard navigation' })
      ).toBeInTheDocument();
    });

    it('should have aria-expanded on toggle button', () => {
      render(<DashboardNav groups={mockGroups} activeSection="overview" />);

      const toggleButton = screen.getByRole('button', { name: /overview/i });
      expect(toggleButton).toHaveAttribute('aria-expanded', 'false');

      fireEvent.click(toggleButton);
      expect(toggleButton).toHaveAttribute('aria-expanded', 'true');
    });

    it('should have aria-haspopup on toggle button', () => {
      render(<DashboardNav groups={mockGroups} activeSection="overview" />);

      const toggleButton = screen.getByRole('button', { name: /overview/i });
      expect(toggleButton).toHaveAttribute('aria-haspopup', 'true');
    });

    it('should close on Escape key', () => {
      render(<DashboardNav groups={mockGroups} activeSection="overview" />);

      // Open dropdown
      const toggleButton = screen.getByRole('button', { name: /overview/i });
      fireEvent.click(toggleButton);
      expect(toggleButton).toHaveAttribute('aria-expanded', 'true');

      // Press Escape
      fireEvent.keyDown(document, { key: 'Escape' });
      expect(toggleButton).toHaveAttribute('aria-expanded', 'false');
    });
  });
});
