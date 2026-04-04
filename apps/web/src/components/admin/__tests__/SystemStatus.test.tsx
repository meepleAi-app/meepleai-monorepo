/**
 * SystemStatus Component Tests - Issue #885
 */

import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { SystemStatus, defaultServices, type ServiceStatus } from '../SystemStatus';

describe('SystemStatus', () => {
  describe('Default rendering', () => {
    it('renders with default services', () => {
      render(<SystemStatus />);
      expect(screen.getByTestId('system-status-title')).toBeInTheDocument();
    });

    it('renders system status banner', () => {
      render(<SystemStatus />);
      expect(screen.getByTestId('system-status-banner')).toBeInTheDocument();
    });

    it('renders all default services', () => {
      render(<SystemStatus />);
      defaultServices.forEach(service => {
        expect(
          screen.getByTestId(`service-status-${service.name.toLowerCase().replace(/\s+/g, '-')}`)
        ).toBeInTheDocument();
      });
    });
  });

  describe('Overall status', () => {
    it('displays healthy status correctly', () => {
      render(<SystemStatus overallStatus="healthy" />);
      expect(screen.getByTestId('system-status-label')).toHaveTextContent(
        'All Systems Operational'
      );
    });

    it('displays degraded status correctly', () => {
      render(<SystemStatus overallStatus="degraded" />);
      expect(screen.getByTestId('system-status-label')).toHaveTextContent('Degraded Performance');
    });

    it('displays unhealthy status correctly', () => {
      render(<SystemStatus overallStatus="unhealthy" />);
      expect(screen.getByTestId('system-status-label')).toHaveTextContent('System Issues Detected');
    });

    it('displays unknown status correctly', () => {
      render(<SystemStatus overallStatus="unknown" />);
      expect(screen.getByTestId('system-status-label')).toHaveTextContent('Status Unknown');
    });

    it('applies healthy status styling', () => {
      render(<SystemStatus overallStatus="healthy" />);
      const banner = screen.getByTestId('system-status-banner');
      expect(banner).toHaveClass('bg-green-50');
      expect(banner).toHaveClass('border-green-200');
    });

    it('applies degraded status styling', () => {
      render(<SystemStatus overallStatus="degraded" />);
      const banner = screen.getByTestId('system-status-banner');
      expect(banner).toHaveClass('bg-yellow-50');
      expect(banner).toHaveClass('border-yellow-200');
    });

    it('applies unhealthy status styling', () => {
      render(<SystemStatus overallStatus="unhealthy" />);
      const banner = screen.getByTestId('system-status-banner');
      expect(banner).toHaveClass('bg-red-50');
      expect(banner).toHaveClass('border-red-200');
    });
  });

  describe('Services list', () => {
    const customServices: ServiceStatus[] = [
      { name: 'PostgreSQL', status: 'healthy', latency: 12 },
      { name: 'Redis', status: 'degraded', latency: 85, message: 'High latency' },
      { name: 'pgvector', status: 'unhealthy', message: 'Connection failed' },
      { name: 'OpenRouter', status: 'unknown' },
    ];

    it('renders custom services', () => {
      render(<SystemStatus services={customServices} />);
      expect(screen.getByText('PostgreSQL')).toBeInTheDocument();
      expect(screen.getByText('Redis')).toBeInTheDocument();
      expect(screen.getByText('pgvector')).toBeInTheDocument();
      expect(screen.getByText('OpenRouter')).toBeInTheDocument();
    });

    it('displays latency when provided', () => {
      render(<SystemStatus services={customServices} />);
      // Component shows "12ms response time" format
      expect(screen.getByText('12ms response time')).toBeInTheDocument();
      expect(screen.getByText('85ms response time')).toBeInTheDocument();
    });

    it('displays message when provided', () => {
      render(<SystemStatus services={customServices} />);
      expect(screen.getByText('High latency')).toBeInTheDocument();
      expect(screen.getByText('Connection failed')).toBeInTheDocument();
    });

    it('applies correct status indicator colors', () => {
      const { container } = render(<SystemStatus services={customServices} />);
      // Check for status dot colors - component uses bg-muted-foreground for unknown status
      expect(container.querySelector('.bg-green-500')).toBeInTheDocument();
      expect(container.querySelector('.bg-yellow-500')).toBeInTheDocument();
      expect(container.querySelector('.bg-red-500')).toBeInTheDocument();
      expect(container.querySelector('.bg-muted-foreground')).toBeInTheDocument();
    });
  });

  describe('Last update timestamp', () => {
    it('displays last update time', () => {
      const lastUpdate = new Date('2025-12-09T14:30:00');
      render(<SystemStatus lastUpdate={lastUpdate} />);
      expect(screen.getByText(/Last checked:/)).toBeInTheDocument();
    });

    it('does not display timestamp when not provided', () => {
      render(<SystemStatus />);
      expect(screen.queryByText(/Last checked:/)).not.toBeInTheDocument();
    });
  });

  describe('Refresh functionality', () => {
    it('renders refresh button when onRefresh provided', () => {
      const handleRefresh = vi.fn();
      render(<SystemStatus onRefresh={handleRefresh} />);
      expect(screen.getByTestId('system-status-refresh')).toBeInTheDocument();
    });

    it('does not render refresh button when onRefresh not provided', () => {
      render(<SystemStatus />);
      expect(screen.queryByTestId('system-status-refresh')).not.toBeInTheDocument();
    });

    it('calls onRefresh when button clicked', () => {
      const handleRefresh = vi.fn();
      render(<SystemStatus onRefresh={handleRefresh} />);
      fireEvent.click(screen.getByTestId('system-status-refresh'));
      expect(handleRefresh).toHaveBeenCalledTimes(1);
    });

    it('disables button when refreshing', () => {
      const handleRefresh = vi.fn();
      render(<SystemStatus onRefresh={handleRefresh} refreshing />);
      expect(screen.getByTestId('system-status-refresh')).toBeDisabled();
    });

    it('shows spinning animation when refreshing', () => {
      const handleRefresh = vi.fn();
      const { container } = render(<SystemStatus onRefresh={handleRefresh} refreshing />);
      const spinningIcon = container.querySelector('.animate-spin');
      expect(spinningIcon).toBeInTheDocument();
    });

    it('has accessible label on refresh button', () => {
      const handleRefresh = vi.fn();
      render(<SystemStatus onRefresh={handleRefresh} />);
      expect(screen.getByLabelText('Refresh system status')).toBeInTheDocument();
    });
  });

  describe('Loading state', () => {
    it('renders skeleton when loading', () => {
      render(<SystemStatus loading />);
      expect(screen.getByTestId('system-status-skeleton')).toBeInTheDocument();
    });

    it('does not render services when loading', () => {
      render(<SystemStatus loading />);
      expect(screen.queryByText('Database')).not.toBeInTheDocument();
    });

    it('renders skeleton placeholders', () => {
      const { container } = render(<SystemStatus loading />);
      const skeletons = container.querySelectorAll('.animate-pulse');
      expect(skeletons.length).toBeGreaterThan(0);
    });
  });

  describe('Accessibility', () => {
    it('has accessible service list', () => {
      render(<SystemStatus />);
      const list = screen.getByRole('list', { name: 'Service status list' });
      expect(list).toBeInTheDocument();
    });

    it('has listitem role for each service', () => {
      render(<SystemStatus />);
      const items = screen.getAllByRole('listitem');
      expect(items.length).toBe(defaultServices.length);
    });

    it('status dots have descriptive titles', () => {
      const services: ServiceStatus[] = [{ name: 'TestService', status: 'healthy' }];
      const { container } = render(<SystemStatus services={services} />);
      const dot = container.querySelector('[title="TestService: healthy"]');
      expect(dot).toBeInTheDocument();
    });

    it('status dots have aria-label', () => {
      const services: ServiceStatus[] = [{ name: 'TestService', status: 'degraded' }];
      const { container } = render(<SystemStatus services={services} />);
      const dot = container.querySelector('[aria-label="TestService is degraded"]');
      expect(dot).toBeInTheDocument();
    });
  });

  describe('Custom className', () => {
    it('applies custom className to card', () => {
      const { container } = render(<SystemStatus className="custom-class" />);
      const card = container.querySelector('.custom-class');
      expect(card).toBeInTheDocument();
    });
  });

  describe('Icons', () => {
    it('renders title icon', () => {
      const { container } = render(<SystemStatus />);
      const icons = container.querySelectorAll('svg');
      expect(icons.length).toBeGreaterThan(0);
    });

    it('renders status icon in banner', () => {
      const { container } = render(<SystemStatus overallStatus="healthy" />);
      const banner = screen.getByTestId('system-status-banner');
      const icon = banner.querySelector('svg');
      expect(icon).toBeInTheDocument();
    });
  });
});
