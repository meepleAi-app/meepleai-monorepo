/**
 * Tests for Breadcrumbs component
 * Issue #3451: Breadcrumbs and Scroll Progress
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

import { Breadcrumbs } from '../Breadcrumbs';

import type { NavGroup } from '../DashboardSidebar';

// Mock navigation groups for testing
const mockGroups: NavGroup[] = [
  {
    id: 'understand',
    label: 'Understand',
    icon: '🎓',
    description: 'Learn how TOMAC-RAG works',
    sections: [
      { id: 'overview', label: 'System Overview' },
      { id: 'architecture', label: 'Architecture' },
    ],
  },
  {
    id: 'explore',
    label: 'Explore',
    icon: '🔍',
    description: 'Test and visualize the system',
    sections: [
      { id: 'query-sim', label: 'Query Simulator' },
      { id: 'token-flow', label: 'Token Flow' },
    ],
  },
  {
    id: 'optimize',
    label: 'Optimize',
    icon: '💰',
    description: 'Cost and model optimization',
    sections: [
      { id: 'cost', label: 'Cost Calculator' },
    ],
  },
];

describe('Breadcrumbs', () => {
  beforeEach(() => {
    // Mock scrollIntoView
    Element.prototype.scrollIntoView = vi.fn();

    // Mock window.scrollTo
    vi.spyOn(window, 'scrollTo').mockImplementation(() => {});
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  // =========================================================================
  // Rendering Tests
  // =========================================================================

  describe('Rendering', () => {
    it('should not render when no active section', () => {
      const { container } = render(<Breadcrumbs groups={mockGroups} />);
      expect(container.querySelector('nav')).toBeNull();
    });

    it('should not render when active section is not found', () => {
      const { container } = render(
        <Breadcrumbs activeSection="nonexistent" groups={mockGroups} />
      );
      expect(container.querySelector('nav')).toBeNull();
    });

    it('should render when active section exists', () => {
      render(<Breadcrumbs activeSection="query-sim" groups={mockGroups} />);
      expect(screen.getByRole('navigation')).toBeInTheDocument();
    });

    it('should render Home button', () => {
      render(<Breadcrumbs activeSection="query-sim" groups={mockGroups} />);
      expect(screen.getByLabelText('Scroll to top')).toBeInTheDocument();
    });

    it('should render group label', () => {
      render(<Breadcrumbs activeSection="query-sim" groups={mockGroups} />);
      expect(screen.getByText('Explore')).toBeInTheDocument();
    });

    it('should render section label', () => {
      render(<Breadcrumbs activeSection="query-sim" groups={mockGroups} />);
      expect(screen.getByText('Query Simulator')).toBeInTheDocument();
    });

    it('should render group icon', () => {
      render(<Breadcrumbs activeSection="query-sim" groups={mockGroups} />);
      expect(screen.getByText('🔍')).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Navigation Structure Tests
  // =========================================================================

  describe('Navigation structure', () => {
    it('should show Home > Group > Section format', () => {
      render(<Breadcrumbs activeSection="overview" groups={mockGroups} />);

      // Home icon exists
      expect(screen.getByLabelText('Scroll to top')).toBeInTheDocument();
      // Group label
      expect(screen.getByText('Understand')).toBeInTheDocument();
      // Section label
      expect(screen.getByText('System Overview')).toBeInTheDocument();
    });

    it('should show separators between breadcrumb items', () => {
      const { container } = render(<Breadcrumbs activeSection="architecture" groups={mockGroups} />);

      // Should have ChevronRight separators (2 total: Home > Group > Section)
      // These are SVG icons with aria-hidden="true"
      const separators = container.querySelectorAll('[aria-hidden="true"]');
      // At least 2 separators (chevrons) plus the group icon emoji
      expect(separators.length).toBeGreaterThanOrEqual(2);
    });

    it('should show only Home > Group when activeSection is a group ID', () => {
      render(<Breadcrumbs activeSection="understand" groups={mockGroups} />);

      expect(screen.getByText('Understand')).toBeInTheDocument();
      // Section should not be present
      expect(screen.queryByText('System Overview')).not.toBeInTheDocument();
    });
  });

  // =========================================================================
  // Accessibility Tests
  // =========================================================================

  describe('Accessibility', () => {
    it('should have navigation aria-label', () => {
      render(<Breadcrumbs activeSection="query-sim" groups={mockGroups} />);
      expect(screen.getByRole('navigation')).toHaveAttribute(
        'aria-label',
        'Breadcrumb navigation'
      );
    });

    it('should mark current section with aria-current', () => {
      render(<Breadcrumbs activeSection="query-sim" groups={mockGroups} />);
      expect(screen.getByText('Query Simulator').closest('button')).toHaveAttribute(
        'aria-current',
        'page'
      );
    });

    it('should mark group as current when no section selected', () => {
      render(<Breadcrumbs activeSection="understand" groups={mockGroups} />);
      expect(screen.getByText('Understand').closest('button')).toHaveAttribute(
        'aria-current',
        'page'
      );
    });

    it('should have hidden text for icons', () => {
      render(<Breadcrumbs activeSection="query-sim" groups={mockGroups} />);
      expect(screen.getByText('Dashboard')).toHaveClass('sr-only');
    });

    it('should hide chevron icons from screen readers', () => {
      const { container } = render(<Breadcrumbs activeSection="architecture" groups={mockGroups} />);
      // Chevrons are SVG elements with aria-hidden="true"
      const hiddenElements = container.querySelectorAll('[aria-hidden="true"]');
      expect(hiddenElements.length).toBeGreaterThan(0);
      hiddenElements.forEach(element => {
        expect(element).toHaveAttribute('aria-hidden', 'true');
      });
    });
  });

  // =========================================================================
  // Click Behavior Tests
  // =========================================================================

  describe('Click behavior', () => {
    it('should scroll to top when clicking Home', () => {
      render(<Breadcrumbs activeSection="query-sim" groups={mockGroups} />);

      fireEvent.click(screen.getByLabelText('Scroll to top'));

      expect(window.scrollTo).toHaveBeenCalledWith({
        top: 0,
        behavior: 'smooth',
      });
    });

    it('should scroll to group when clicking group button', () => {
      const mockElement = document.createElement('div');
      mockElement.id = 'explore';
      document.body.appendChild(mockElement);

      render(<Breadcrumbs activeSection="query-sim" groups={mockGroups} />);

      fireEvent.click(screen.getByText('Explore'));

      expect(mockElement.scrollIntoView).toHaveBeenCalledWith({
        behavior: 'smooth',
        block: 'start',
      });

      document.body.removeChild(mockElement);
    });

    it('should scroll to section when clicking section button', () => {
      const mockElement = document.createElement('div');
      mockElement.id = 'query-sim';
      document.body.appendChild(mockElement);

      render(<Breadcrumbs activeSection="query-sim" groups={mockGroups} />);

      fireEvent.click(screen.getByText('Query Simulator'));

      expect(mockElement.scrollIntoView).toHaveBeenCalledWith({
        behavior: 'smooth',
        block: 'start',
      });

      document.body.removeChild(mockElement);
    });

    it('should call onNavigate callback when clicking group', () => {
      const mockElement = document.createElement('div');
      mockElement.id = 'explore';
      document.body.appendChild(mockElement);

      const onNavigate = vi.fn();
      render(
        <Breadcrumbs
          activeSection="query-sim"
          groups={mockGroups}
          onNavigate={onNavigate}
        />
      );

      fireEvent.click(screen.getByText('Explore'));

      expect(onNavigate).toHaveBeenCalledWith('explore');

      document.body.removeChild(mockElement);
    });

    it('should call onNavigate callback when clicking section', () => {
      const mockElement = document.createElement('div');
      mockElement.id = 'query-sim';
      document.body.appendChild(mockElement);

      const onNavigate = vi.fn();
      render(
        <Breadcrumbs
          activeSection="query-sim"
          groups={mockGroups}
          onNavigate={onNavigate}
        />
      );

      fireEvent.click(screen.getByText('Query Simulator'));

      expect(onNavigate).toHaveBeenCalledWith('query-sim');

      document.body.removeChild(mockElement);
    });

    it('should not throw when element does not exist', () => {
      render(<Breadcrumbs activeSection="query-sim" groups={mockGroups} />);

      // Should not throw
      expect(() => {
        fireEvent.click(screen.getByText('Explore'));
      }).not.toThrow();
    });
  });

  // =========================================================================
  // Styling Tests
  // =========================================================================

  describe('Styling', () => {
    it('should apply custom className', () => {
      render(
        <Breadcrumbs
          activeSection="query-sim"
          groups={mockGroups}
          className="custom-breadcrumbs"
        />
      );
      expect(screen.getByRole('navigation')).toHaveClass('custom-breadcrumbs');
    });

    it('should have base styling classes', () => {
      render(<Breadcrumbs activeSection="query-sim" groups={mockGroups} />);
      expect(screen.getByRole('navigation')).toHaveClass(
        'flex',
        'items-center',
        'gap-1',
        'text-sm',
        'text-muted-foreground'
      );
    });

    it('should highlight current section with font-medium', () => {
      render(<Breadcrumbs activeSection="query-sim" groups={mockGroups} />);
      expect(screen.getByText('Query Simulator').closest('button')).toHaveClass(
        'font-medium',
        'text-foreground'
      );
    });
  });

  // =========================================================================
  // Different Groups Tests
  // =========================================================================

  describe('Different groups', () => {
    it('should correctly identify section in Understand group', () => {
      render(<Breadcrumbs activeSection="overview" groups={mockGroups} />);
      expect(screen.getByText('Understand')).toBeInTheDocument();
      expect(screen.getByText('System Overview')).toBeInTheDocument();
    });

    it('should correctly identify section in Explore group', () => {
      render(<Breadcrumbs activeSection="token-flow" groups={mockGroups} />);
      expect(screen.getByText('Explore')).toBeInTheDocument();
      expect(screen.getByText('Token Flow')).toBeInTheDocument();
    });

    it('should correctly identify section in Optimize group', () => {
      render(<Breadcrumbs activeSection="cost" groups={mockGroups} />);
      expect(screen.getByText('Optimize')).toBeInTheDocument();
      expect(screen.getByText('Cost Calculator')).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Memoization Tests
  // =========================================================================

  describe('Memoization', () => {
    it('should memoize breadcrumb info based on activeSection', () => {
      const { rerender } = render(
        <Breadcrumbs activeSection="query-sim" groups={mockGroups} />
      );

      expect(screen.getByText('Query Simulator')).toBeInTheDocument();

      rerender(<Breadcrumbs activeSection="overview" groups={mockGroups} />);

      expect(screen.getByText('System Overview')).toBeInTheDocument();
      expect(screen.queryByText('Query Simulator')).not.toBeInTheDocument();
    });
  });

  // =========================================================================
  // Default Props Tests
  // =========================================================================

  describe('Default props', () => {
    it('should use NAVIGATION_GROUPS when groups not provided', () => {
      // This test verifies the component can be used without explicit groups
      // The default is NAVIGATION_GROUPS from RagDashboard
      // Note: After the tabbed redesign, NAVIGATION_GROUPS contains tab IDs
      // like 'overview', 'architecture', 'agents', 'performance', 'walkthrough'
      render(<Breadcrumbs activeSection="overview" />);
      // If it renders without error and finds the section, defaults work
      expect(screen.getByRole('navigation')).toBeInTheDocument();
    });
  });
});
