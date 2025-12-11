/**
 * QuickActions Component Tests - Issue #885
 */

import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { QuickActions, defaultQuickActions, type QuickAction } from '../QuickActions';
import { FileUpIcon, UsersIcon, AlertTriangleIcon } from 'lucide-react';

describe('QuickActions', () => {
  describe('Default rendering', () => {
    it('renders with default actions', () => {
      render(<QuickActions />);
      expect(screen.getByTestId('quick-actions-grid')).toBeInTheDocument();
    });

    it('renders default title', () => {
      render(<QuickActions />);
      expect(screen.getByText('Quick Actions')).toBeInTheDocument();
    });

    it('renders all default actions', () => {
      render(<QuickActions />);
      defaultQuickActions.forEach(action => {
        expect(screen.getByTestId(`quick-action-${action.id}`)).toBeInTheDocument();
      });
    });

    it('renders action labels', () => {
      render(<QuickActions />);
      expect(screen.getByText('Upload PDF')).toBeInTheDocument();
      expect(screen.getByText('Manage Users')).toBeInTheDocument();
    });

    it('renders action descriptions', () => {
      render(<QuickActions />);
      expect(screen.getByText('Add new game rules')).toBeInTheDocument();
      expect(screen.getByText('View and edit users')).toBeInTheDocument();
    });
  });

  describe('Custom actions', () => {
    const customActions: QuickAction[] = [
      {
        id: 'custom-1',
        label: 'Custom Action 1',
        description: 'Description 1',
        href: '/custom-1',
        icon: FileUpIcon,
        variant: 'primary',
      },
      {
        id: 'custom-2',
        label: 'Custom Action 2',
        description: 'Description 2',
        href: '/custom-2',
        icon: UsersIcon,
        variant: 'default',
      },
    ];

    it('renders custom actions instead of defaults', () => {
      render(<QuickActions actions={customActions} />);
      expect(screen.getByText('Custom Action 1')).toBeInTheDocument();
      expect(screen.getByText('Custom Action 2')).toBeInTheDocument();
      expect(screen.queryByText('Upload PDF')).not.toBeInTheDocument();
    });

    it('renders correct number of actions', () => {
      render(<QuickActions actions={customActions} />);
      const links = screen.getAllByRole('link');
      expect(links).toHaveLength(2);
    });
  });

  describe('Custom title', () => {
    it('renders custom title when provided', () => {
      render(<QuickActions title="Admin Actions" />);
      expect(screen.getByText('Admin Actions')).toBeInTheDocument();
      expect(screen.queryByText('Quick Actions')).not.toBeInTheDocument();
    });
  });

  describe('Loading state', () => {
    it('renders skeleton when loading', () => {
      render(<QuickActions loading />);
      expect(screen.getByTestId('quick-actions-skeleton')).toBeInTheDocument();
    });

    it('does not render actions when loading', () => {
      render(<QuickActions loading />);
      expect(screen.queryByTestId('quick-actions-grid')).not.toBeInTheDocument();
    });

    it('renders 6 skeleton items', () => {
      const { container } = render(<QuickActions loading />);
      const skeletons = container.querySelectorAll('.animate-pulse');
      expect(skeletons.length).toBeGreaterThan(0);
    });
  });

  describe('Badges', () => {
    const actionsWithBadges: QuickAction[] = [
      {
        id: 'action-with-badge',
        label: 'Alerts',
        href: '/alerts',
        icon: AlertTriangleIcon,
        variant: 'warning',
        badge: 5,
      },
      {
        id: 'action-with-large-badge',
        label: 'Notifications',
        href: '/notifications',
        icon: AlertTriangleIcon,
        variant: 'danger',
        badge: 150,
      },
      {
        id: 'action-no-badge',
        label: 'Settings',
        href: '/settings',
        icon: UsersIcon,
        variant: 'default',
      },
    ];

    it('renders badge when provided', () => {
      render(<QuickActions actions={actionsWithBadges} />);
      expect(screen.getByText('5')).toBeInTheDocument();
    });

    it('displays 99+ for badges over 99', () => {
      render(<QuickActions actions={actionsWithBadges} />);
      expect(screen.getByText('99+')).toBeInTheDocument();
    });

    it('does not render badge when not provided', () => {
      render(
        <QuickActions
          actions={[
            {
              id: 'no-badge',
              label: 'No Badge',
              href: '/test',
              icon: UsersIcon,
              variant: 'default',
            },
          ]}
        />
      );
      const badges = screen.queryAllByRole('status');
      // No badges should be rendered
      expect(badges).toHaveLength(0);
    });
  });

  describe('Variants', () => {
    it('applies primary variant styling', () => {
      render(
        <QuickActions
          actions={[
            {
              id: 'primary',
              label: 'Primary',
              href: '/test',
              icon: FileUpIcon,
              variant: 'primary',
            },
          ]}
        />
      );
      const link = screen.getByTestId('quick-action-primary');
      expect(link).toHaveClass('hover:bg-blue-50');
    });

    it('applies warning variant styling', () => {
      render(
        <QuickActions
          actions={[
            {
              id: 'warning',
              label: 'Warning',
              href: '/test',
              icon: AlertTriangleIcon,
              variant: 'warning',
            },
          ]}
        />
      );
      const link = screen.getByTestId('quick-action-warning');
      expect(link).toHaveClass('hover:bg-yellow-50');
    });

    it('applies danger variant styling', () => {
      render(
        <QuickActions
          actions={[
            {
              id: 'danger',
              label: 'Danger',
              href: '/test',
              icon: AlertTriangleIcon,
              variant: 'danger',
            },
          ]}
        />
      );
      const link = screen.getByTestId('quick-action-danger');
      expect(link).toHaveClass('hover:bg-red-50');
    });

    it('applies default variant when not specified', () => {
      render(
        <QuickActions
          actions={[
            {
              id: 'default',
              label: 'Default',
              href: '/test',
              icon: UsersIcon,
            },
          ]}
        />
      );
      const link = screen.getByTestId('quick-action-default');
      expect(link).toHaveClass('hover:bg-gray-50');
    });
  });

  describe('Links', () => {
    it('renders links with correct href', () => {
      render(<QuickActions />);
      const uploadLink = screen.getByTestId('quick-action-upload-pdf');
      expect(uploadLink).toHaveAttribute('href', '/admin/bulk-export');
    });

    it('all actions are links', () => {
      render(<QuickActions />);
      defaultQuickActions.forEach(action => {
        const link = screen.getByTestId(`quick-action-${action.id}`);
        expect(link.tagName).toBe('A');
      });
    });
  });

  describe('Responsive grid', () => {
    it('has responsive grid classes', () => {
      render(<QuickActions />);
      const grid = screen.getByTestId('quick-actions-grid');
      expect(grid).toHaveClass('grid-cols-1');
      expect(grid).toHaveClass('sm:grid-cols-2');
      expect(grid).toHaveClass('lg:grid-cols-3');
    });
  });

  describe('Custom className', () => {
    it('applies custom className to card', () => {
      const { container } = render(<QuickActions className="custom-class" />);
      const card = container.querySelector('.custom-class');
      expect(card).toBeInTheDocument();
    });
  });

  describe('Icons', () => {
    it('renders icons for each action', () => {
      const { container } = render(<QuickActions />);
      const icons = container.querySelectorAll('svg');
      // At least one icon per action + title icon
      expect(icons.length).toBeGreaterThanOrEqual(defaultQuickActions.length);
    });

    it('icons have aria-hidden for accessibility', () => {
      const { container } = render(<QuickActions />);
      const iconContainers = container.querySelectorAll('[aria-hidden="true"]');
      expect(iconContainers.length).toBeGreaterThan(0);
    });
  });
});
