/**
 * ServiceHealthMatrix Component Tests - Issue #897
 *
 * Comprehensive test coverage for infrastructure services grid.
 * Tests grid layout, loading, empty states, and responsive behavior.
 */

import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { ServiceHealthMatrix } from '../ServiceHealthMatrix';
import { createMockService } from '@/app/admin/infrastructure/__tests__/helpers/test-utils';
import type { ServiceHealthStatus } from '@/lib/api/schemas/admin.schemas';

describe('ServiceHealthMatrix', () => {
  // ==================== Basic Rendering ====================

  it('renders services grid', () => {
    const services = [
      createMockService('postgres', 'Healthy', 15300),
      createMockService('redis', 'Healthy', 900),
    ];

    render(<ServiceHealthMatrix services={services} locale="it" />);

    expect(screen.getByTestId('service-health-matrix')).toBeInTheDocument();
  });

  it('renders all provided services', () => {
    const services = [
      createMockService('postgres', 'Healthy', 15300),
      createMockService('redis', 'Healthy', 900),
      createMockService('qdrant', 'Degraded', 1200000),
    ];

    render(<ServiceHealthMatrix services={services} locale="it" />);

    expect(screen.getByText('PostgreSQL')).toBeInTheDocument();
    expect(screen.getByText('Redis')).toBeInTheDocument();
    expect(screen.getByText('Qdrant')).toBeInTheDocument();
  });

  it('renders correct number of service cards', () => {
    const services = [
      createMockService('postgres', 'Healthy', 15300),
      createMockService('redis', 'Healthy', 900),
      createMockService('qdrant', 'Healthy', 25000),
      createMockService('n8n', 'Healthy', 350000),
    ];

    render(<ServiceHealthMatrix services={services} locale="it" />);

    const cards = screen.getAllByRole('listitem');
    expect(cards).toHaveLength(4);
  });

  // ==================== Loading State ====================

  it('renders loading skeleton when loading is true', () => {
    render(<ServiceHealthMatrix loading locale="it" />);

    expect(screen.getByTestId('service-health-matrix-loading')).toBeInTheDocument();
  });

  it('renders default number of skeletons when loading', () => {
    render(<ServiceHealthMatrix loading locale="it" />);

    const skeletons = screen.getAllByTestId('service-card-loading');
    expect(skeletons).toHaveLength(6); // Default skeleton count
  });

  it('has accessible loading status', () => {
    render(<ServiceHealthMatrix loading locale="it" />);

    const loadingElement = screen.getByRole('status');
    expect(loadingElement).toBeInTheDocument();
  });

  it('does not render services when loading', () => {
    const services = [createMockService('postgres', 'Healthy', 15300)];

    render(<ServiceHealthMatrix services={services} loading locale="it" />);

    expect(screen.queryByText('PostgreSQL')).not.toBeInTheDocument();
  });

  // ==================== Empty State ====================

  it('renders empty state when no services provided', () => {
    render(<ServiceHealthMatrix services={[]} locale="it" />);

    expect(screen.getByTestId('service-health-matrix-empty')).toBeInTheDocument();
  });

  it('displays empty state message in Italian', () => {
    render(<ServiceHealthMatrix services={[]} locale="it" />);

    expect(screen.getByText(/Nessun dato disponibile/)).toBeInTheDocument();
  });

  it('displays empty state message in English', () => {
    render(<ServiceHealthMatrix services={[]} locale="en" />);

    expect(screen.getByText(/No data available/)).toBeInTheDocument();
  });

  it('shows empty state icon', () => {
    const { container } = render(<ServiceHealthMatrix services={[]} locale="it" />);

    const icon = container.querySelector('svg');
    expect(icon).toBeInTheDocument();
  });

  // ==================== Grid Layouts ====================

  it('applies auto layout by default', () => {
    const services = [createMockService('postgres', 'Healthy', 15300)];
    const { container } = render(<ServiceHealthMatrix services={services} locale="it" />);

    const grid = container.querySelector('[class*="grid-cols-1"]');
    expect(grid).toBeInTheDocument();
  });

  it('applies grid-2 layout when specified', () => {
    const services = [createMockService('postgres', 'Healthy', 15300)];
    const { container } = render(
      <ServiceHealthMatrix services={services} layout="grid-2" locale="it" />
    );

    const grid = container.querySelector('[class*="md:grid-cols-2"]');
    expect(grid).toBeInTheDocument();
  });

  it('applies grid-3 layout when specified', () => {
    const services = [createMockService('postgres', 'Healthy', 15300)];
    const { container } = render(
      <ServiceHealthMatrix services={services} layout="grid-3" locale="it" />
    );

    const grid = container.querySelector('[class*="lg:grid-cols-3"]');
    expect(grid).toBeInTheDocument();
  });

  it('applies grid-4 layout when specified', () => {
    const services = [createMockService('postgres', 'Healthy', 15300)];
    const { container } = render(
      <ServiceHealthMatrix services={services} layout="grid-4" locale="it" />
    );

    const grid = container.querySelector('[class*="xl:grid-cols-4"]');
    expect(grid).toBeInTheDocument();
  });

  // ==================== Data Parsing ====================

  it('parses TimeSpan response time correctly', () => {
    const services: ServiceHealthStatus[] = [
      {
        serviceName: 'postgres',
        state: 'Healthy',
        errorMessage: null,
        checkedAt: new Date().toISOString(),
        responseTime: '00:00:00.0153000', // 15.3ms
      },
    ];

    render(<ServiceHealthMatrix services={services} locale="it" />);

    expect(screen.getByText(/15ms/)).toBeInTheDocument();
  });

  it('parses TimeSpan with seconds correctly', () => {
    const services: ServiceHealthStatus[] = [
      {
        serviceName: 'n8n',
        state: 'Healthy',
        errorMessage: null,
        checkedAt: new Date().toISOString(),
        responseTime: '00:00:01.5000000', // 1.5s = 1500ms
      },
    ];

    render(<ServiceHealthMatrix services={services} locale="it" />);

    expect(screen.getByText(/1.50s/)).toBeInTheDocument();
  });

  it('parses checkedAt timestamp correctly', () => {
    const now = new Date();
    const services: ServiceHealthStatus[] = [
      {
        serviceName: 'postgres',
        state: 'Healthy',
        errorMessage: null,
        checkedAt: now.toISOString(),
        responseTime: '00:00:00.0150000',
      },
    ];

    render(<ServiceHealthMatrix services={services} locale="it" />);

    expect(screen.getByText(/Ora/)).toBeInTheDocument();
  });

  // ==================== Mixed Health States ====================

  it('renders services with different health states', () => {
    const services = [
      createMockService('postgres', 'Healthy', 15300),
      createMockService('redis', 'Degraded', 1200000, 'High latency'),
      createMockService('qdrant', 'Unhealthy', 0, 'Connection refused'),
    ];

    render(<ServiceHealthMatrix services={services} locale="it" />);

    expect(screen.getByText('Sano')).toBeInTheDocument();
    expect(screen.getByText('Degradato')).toBeInTheDocument();
    expect(screen.getByText('Non funzionante')).toBeInTheDocument();
  });

  it('displays error messages for unhealthy services', () => {
    const services = [createMockService('postgres', 'Unhealthy', 0, 'Database connection lost')];

    render(<ServiceHealthMatrix services={services} locale="it" />);

    expect(screen.getByText(/Database connection lost/)).toBeInTheDocument();
  });

  // ==================== Internationalization (i18n) ====================

  it('displays Italian labels by default', () => {
    const services = [createMockService('postgres', 'Healthy', 15300)];

    render(<ServiceHealthMatrix services={services} locale="it" />);

    expect(screen.getByText('Sano')).toBeInTheDocument();
  });

  it('displays English labels when locale is "en"', () => {
    const services = [createMockService('postgres', 'Healthy', 15300)];

    render(<ServiceHealthMatrix services={services} locale="en" />);

    expect(screen.getByText('Healthy')).toBeInTheDocument();
  });

  it('passes locale to ServiceCard components', () => {
    const services = [createMockService('postgres', 'Healthy', 15300)];

    render(<ServiceHealthMatrix services={services} locale="en" />);

    expect(screen.getByText(/Response Time:/)).toBeInTheDocument();
  });

  // ==================== Accessibility ====================

  it('has accessible list role', () => {
    const services = [createMockService('postgres', 'Healthy', 15300)];

    render(<ServiceHealthMatrix services={services} locale="it" />);

    expect(screen.getByRole('list')).toBeInTheDocument();
  });

  it('has accessible aria-label for grid', () => {
    const services = [createMockService('postgres', 'Healthy', 15300)];

    render(<ServiceHealthMatrix services={services} locale="it" />);

    const list = screen.getByRole('list');
    expect(list).toHaveAttribute('aria-label');
  });

  it('renders each service as list item', () => {
    const services = [
      createMockService('postgres', 'Healthy', 15300),
      createMockService('redis', 'Healthy', 900),
    ];

    render(<ServiceHealthMatrix services={services} locale="it" />);

    const items = screen.getAllByRole('listitem');
    expect(items).toHaveLength(2);
  });

  // ==================== CSS Classes & Styling ====================

  it('applies custom className', () => {
    const services = [createMockService('postgres', 'Healthy', 15300)];
    const { container } = render(
      <ServiceHealthMatrix services={services} className="custom-grid-class" locale="it" />
    );

    const grid = container.querySelector('.custom-grid-class');
    expect(grid).toBeInTheDocument();
  });

  it('has grid gap classes', () => {
    const services = [createMockService('postgres', 'Healthy', 15300)];
    const { container } = render(<ServiceHealthMatrix services={services} locale="it" />);

    const grid = container.querySelector('[class*="gap-4"]');
    expect(grid).toBeInTheDocument();
  });

  it('applies responsive grid classes', () => {
    const services = [createMockService('postgres', 'Healthy', 15300)];
    const { container } = render(<ServiceHealthMatrix services={services} locale="it" />);

    const grid = container.querySelector('[class*="sm:grid-cols-2"]');
    expect(grid).toBeInTheDocument();
  });

  // ==================== Edge Cases ====================

  it('handles services with undefined response time', () => {
    const services: ServiceHealthStatus[] = [
      {
        serviceName: 'postgres',
        state: 'Healthy',
        errorMessage: null,
        checkedAt: new Date().toISOString(),
        responseTime: '00:00:00.0000000',
      },
    ];

    render(<ServiceHealthMatrix services={services} locale="it" />);

    expect(screen.getByText(/0ms/)).toBeInTheDocument();
  });

  it('handles services with null error message', () => {
    const services: ServiceHealthStatus[] = [
      {
        serviceName: 'postgres',
        state: 'Degraded',
        errorMessage: null,
        checkedAt: new Date().toISOString(),
        responseTime: '00:00:00.0150000',
      },
    ];

    render(<ServiceHealthMatrix services={services} locale="it" />);

    expect(screen.getByText('PostgreSQL')).toBeInTheDocument();
  });

  it('handles large number of services', () => {
    const services = Array.from({ length: 12 }, (_, i) =>
      createMockService(`service${i}`, 'Healthy', 15000)
    );

    render(<ServiceHealthMatrix services={services} locale="it" />);

    const cards = screen.getAllByRole('listitem');
    expect(cards).toHaveLength(12);
  });

  it('handles single service', () => {
    const services = [createMockService('postgres', 'Healthy', 15300)];

    render(<ServiceHealthMatrix services={services} locale="it" />);

    expect(screen.getByRole('list')).toBeInTheDocument();
    expect(screen.getAllByRole('listitem')).toHaveLength(1);
  });

  // ==================== TimeSpan Parsing Edge Cases ====================

  it('parses TimeSpan with hours correctly', () => {
    const services: ServiceHealthStatus[] = [
      {
        serviceName: 'postgres',
        state: 'Healthy',
        errorMessage: null,
        checkedAt: new Date().toISOString(),
        responseTime: '01:30:15.0000000', // 1h 30m 15s = 5415000ms
      },
    ];

    render(<ServiceHealthMatrix services={services} locale="it" />);

    // Should display as seconds since >1000ms
    expect(screen.getByText(/5415.00s/)).toBeInTheDocument();
  });

  it('handles malformed TimeSpan gracefully', () => {
    const services: ServiceHealthStatus[] = [
      {
        serviceName: 'postgres',
        state: 'Healthy',
        errorMessage: null,
        checkedAt: new Date().toISOString(),
        responseTime: 'invalid-timespan',
      },
    ];

    render(<ServiceHealthMatrix services={services} locale="it" />);

    // Should render without crashing
    expect(screen.getByText('PostgreSQL')).toBeInTheDocument();
  });

  it('parses TimeSpan with only fractional seconds', () => {
    const services: ServiceHealthStatus[] = [
      {
        serviceName: 'postgres',
        state: 'Healthy',
        errorMessage: null,
        checkedAt: new Date().toISOString(),
        responseTime: '00:00:00.5000000', // 0.5s = 500ms
      },
    ];

    render(<ServiceHealthMatrix services={services} locale="it" />);

    expect(screen.getByText(/500ms/)).toBeInTheDocument();
  });

  // ==================== Default Props ====================

  it('uses empty array as default for services', () => {
    render(<ServiceHealthMatrix />);

    expect(screen.getByTestId('service-health-matrix-empty')).toBeInTheDocument();
  });

  it('uses Italian locale by default', () => {
    const services = [createMockService('postgres', 'Healthy', 15300)];

    render(<ServiceHealthMatrix services={services} />);

    expect(screen.getByText('Sano')).toBeInTheDocument();
  });

  it('uses auto layout by default', () => {
    const services = [createMockService('postgres', 'Healthy', 15300)];
    const { container } = render(<ServiceHealthMatrix services={services} />);

    const grid = container.querySelector('[class*="grid-cols-1"]');
    expect(grid).toBeInTheDocument();
  });
});
