import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { DashboardSidebar } from '../DashboardSidebar';
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

// Mock ProgressIndicator
vi.mock('../ProgressIndicator', () => ({
  ProgressIndicator: () => <div data-testid="progress-indicator">Progress</div>,
}));

const mockGroups: NavGroup[] = [
  {
    id: 'understand',
    label: 'Understand',
    icon: '🎓',
    description: 'Learn how it works',
    sections: [
      { id: 'overview', label: 'Overview' },
      { id: 'architecture', label: 'Architecture' },
    ],
  },
  {
    id: 'explore',
    label: 'Explore',
    icon: '🔍',
    description: 'Test the system',
    sections: [
      { id: 'query-sim', label: 'Query Simulator' },
      { id: 'token-flow', label: 'Token Flow' },
    ],
  },
];

describe('DashboardSidebar', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Rendering', () => {
    it('should render all navigation groups', () => {
      render(<DashboardSidebar groups={mockGroups} activeSection="overview" />);

      expect(screen.getByText('Understand')).toBeInTheDocument();
      expect(screen.getByText('Explore')).toBeInTheDocument();
    });

    it('should render group icons and labels', () => {
      render(<DashboardSidebar groups={mockGroups} activeSection="overview" />);

      expect(screen.getByText('🎓')).toBeInTheDocument();
      expect(screen.getByText('🔍')).toBeInTheDocument();
      expect(screen.getByText('Understand')).toBeInTheDocument();
      expect(screen.getByText('Explore')).toBeInTheDocument();
    });

    it('should render all sections within groups', () => {
      render(<DashboardSidebar groups={mockGroups} activeSection="overview" />);

      expect(screen.getByText('Overview')).toBeInTheDocument();
      expect(screen.getByText('Architecture')).toBeInTheDocument();
      expect(screen.getByText('Query Simulator')).toBeInTheDocument();
      expect(screen.getByText('Token Flow')).toBeInTheDocument();
    });

    it('should apply custom className', () => {
      const { container } = render(
        <DashboardSidebar
          groups={mockGroups}
          activeSection="overview"
          className="custom-class"
        />
      );

      expect(container.querySelector('.custom-class')).toBeInTheDocument();
    });

    it('should render progress indicator by default', () => {
      render(<DashboardSidebar groups={mockGroups} activeSection="overview" />);

      expect(screen.getByTestId('progress-indicator')).toBeInTheDocument();
    });

    it('should hide progress indicator when showProgress is false', () => {
      render(
        <DashboardSidebar
          groups={mockGroups}
          activeSection="overview"
          showProgress={false}
        />
      );

      expect(screen.queryByTestId('progress-indicator')).not.toBeInTheDocument();
    });
  });

  describe('Group expansion', () => {
    it('should have all groups expanded by default', () => {
      render(<DashboardSidebar groups={mockGroups} activeSection="overview" />);

      // All sections should be visible
      expect(screen.getByText('Overview')).toBeInTheDocument();
      expect(screen.getByText('Query Simulator')).toBeInTheDocument();

      // All groups should have aria-expanded="true"
      const understandButton = screen.getByRole('button', { name: /understand/i });
      expect(understandButton).toHaveAttribute('aria-expanded', 'true');
    });

    it('should toggle group expansion on header click', () => {
      render(<DashboardSidebar groups={mockGroups} activeSection="overview" />);

      // Click the Explore group header (not Understand since it has active section)
      const groupButton = screen.getByRole('button', { name: /explore/i });

      // Initially expanded
      expect(groupButton).toHaveAttribute('aria-expanded', 'true');

      // Click to collapse
      fireEvent.click(groupButton);
      expect(groupButton).toHaveAttribute('aria-expanded', 'false');

      // Click again to expand
      fireEvent.click(groupButton);
      expect(groupButton).toHaveAttribute('aria-expanded', 'true');
    });
  });

  describe('Active section', () => {
    it('should highlight active section', () => {
      render(<DashboardSidebar groups={mockGroups} activeSection="architecture" />);

      const activeButton = screen.getByRole('button', { name: 'Architecture' });
      expect(activeButton).toHaveAttribute('aria-current', 'true');
    });

    it('should not highlight inactive sections', () => {
      render(<DashboardSidebar groups={mockGroups} activeSection="overview" />);

      const inactiveButton = screen.getByRole('button', { name: 'Architecture' });
      expect(inactiveButton).not.toHaveAttribute('aria-current');
    });
  });

  describe('Navigation', () => {
    it('should call scrollToSection on section click', async () => {
      const { scrollToSection } = await import('../hooks/useScrollSpy');

      render(<DashboardSidebar groups={mockGroups} activeSection="overview" />);

      const sectionButton = screen.getByRole('button', { name: 'Architecture' });
      fireEvent.click(sectionButton);

      expect(scrollToSection).toHaveBeenCalledWith('architecture');
    });
  });

  describe('Accessibility', () => {
    it('should have navigation role with aria-label', () => {
      render(<DashboardSidebar groups={mockGroups} activeSection="overview" />);

      // Use getByLabelText to find specific navigation by aria-label
      const nav = screen.getByRole('navigation', { name: 'Dashboard navigation' });
      expect(nav).toBeInTheDocument();
    });

    it('should have aria-controls on group buttons', () => {
      render(<DashboardSidebar groups={mockGroups} activeSection="overview" />);

      const groupButton = screen.getByRole('button', { name: /understand/i });
      expect(groupButton).toHaveAttribute('aria-controls', 'group-understand-sections');
    });
  });
});
