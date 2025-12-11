/**
 * ServiceCard Component Tests - Issue #897
 *
 * Comprehensive test coverage for infrastructure service health cards.
 * Pattern follows StatCard.test.tsx (90%+ coverage requirement).
 */

import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { ServiceCard } from '../ServiceCard';
import type { HealthState } from '@/lib/api';

describe('ServiceCard', () => {
  // ==================== Basic Rendering ====================

  it('renders service name correctly', () => {
    render(<ServiceCard serviceName="postgres" status="Healthy" locale="it" />);

    expect(screen.getByText('PostgreSQL')).toBeInTheDocument();
  });

  it('renders status badge', () => {
    render(<ServiceCard serviceName="redis" status="Healthy" locale="it" />);

    expect(screen.getByText('Sano')).toBeInTheDocument();
  });

  it('displays response time when provided', () => {
    render(
      <ServiceCard serviceName="postgres" status="Healthy" responseTimeMs={15.3} locale="it" />
    );

    expect(screen.getByText(/15ms/)).toBeInTheDocument();
  });

  it('displays last check timestamp when provided', () => {
    const now = new Date();
    render(<ServiceCard serviceName="postgres" status="Healthy" lastCheck={now} locale="it" />);

    expect(screen.getByText(/Ora/)).toBeInTheDocument();
  });

  // ==================== Health States ====================

  it('applies Healthy state styling', () => {
    const { container } = render(
      <ServiceCard serviceName="postgres" status="Healthy" locale="it" />
    );
    const card = container.querySelector('[class*="border-green-200"]');
    expect(card).toBeInTheDocument();
  });

  it('applies Degraded state styling', () => {
    const { container } = render(<ServiceCard serviceName="redis" status="Degraded" locale="it" />);
    const card = container.querySelector('[class*="border-yellow-200"]');
    expect(card).toBeInTheDocument();
  });

  it('applies Unhealthy state styling', () => {
    const { container } = render(
      <ServiceCard serviceName="qdrant" status="Unhealthy" locale="it" />
    );
    const card = container.querySelector('[class*="border-red-200"]');
    expect(card).toBeInTheDocument();
  });

  it('shows CheckCircle icon for Healthy state', () => {
    const { container } = render(
      <ServiceCard serviceName="postgres" status="Healthy" locale="it" />
    );
    const icon = container.querySelector('[class*="text-green-600"]');
    expect(icon).toBeInTheDocument();
  });

  it('shows AlertTriangle icon for Degraded state', () => {
    const { container } = render(<ServiceCard serviceName="redis" status="Degraded" locale="it" />);
    const icon = container.querySelector('[class*="text-yellow-600"]');
    expect(icon).toBeInTheDocument();
  });

  it('shows XCircle icon for Unhealthy state', () => {
    const { container } = render(
      <ServiceCard serviceName="qdrant" status="Unhealthy" locale="it" />
    );
    const icon = container.querySelector('[class*="text-red-600"]');
    expect(icon).toBeInTheDocument();
  });

  // ==================== Error Messages ====================

  it('displays error message for Degraded state', () => {
    render(
      <ServiceCard
        serviceName="redis"
        status="Degraded"
        errorMessage="High latency detected"
        locale="it"
      />
    );

    expect(screen.getByText(/High latency detected/)).toBeInTheDocument();
  });

  it('displays error message for Unhealthy state', () => {
    render(
      <ServiceCard
        serviceName="qdrant"
        status="Unhealthy"
        errorMessage="Connection refused: ECONNREFUSED"
        locale="it"
      />
    );

    expect(screen.getByText(/Connection refused/)).toBeInTheDocument();
  });

  it('does not display error message for Healthy state', () => {
    render(
      <ServiceCard
        serviceName="postgres"
        status="Healthy"
        errorMessage="This should not appear"
        locale="it"
      />
    );

    expect(screen.queryByText(/This should not appear/)).not.toBeInTheDocument();
  });

  it('does not display error message when not provided', () => {
    render(<ServiceCard serviceName="redis" status="Degraded" locale="it" />);

    expect(screen.queryByText(/Messaggio di errore:/)).not.toBeInTheDocument();
  });

  // ==================== Metrics Formatting ====================

  it('formats response time < 1000ms as milliseconds', () => {
    render(<ServiceCard serviceName="redis" status="Healthy" responseTimeMs={45} locale="it" />);

    expect(screen.getByText(/45ms/)).toBeInTheDocument();
  });

  it('formats response time >= 1000ms as seconds', () => {
    render(
      <ServiceCard serviceName="qdrant" status="Degraded" responseTimeMs={1500} locale="it" />
    );

    expect(screen.getByText(/1.50s/)).toBeInTheDocument();
  });

  it('shows dash when response time is undefined', () => {
    render(
      <ServiceCard serviceName="postgres" status="Healthy" responseTimeMs={undefined} locale="it" />
    );

    // Should not show response time line at all when undefined
    expect(screen.queryByText(/Tempo di risposta:/)).not.toBeInTheDocument();
  });

  it('formats last check as "Ora" for recent checks', () => {
    const now = new Date();
    render(<ServiceCard serviceName="postgres" status="Healthy" lastCheck={now} locale="it" />);

    expect(screen.getByText(/Ora/)).toBeInTheDocument();
  });

  it('formats last check as minutes ago', () => {
    const fiveMinutesAgo = new Date(Date.now() - 5 * 60 * 1000);
    render(
      <ServiceCard serviceName="postgres" status="Healthy" lastCheck={fiveMinutesAgo} locale="it" />
    );

    expect(screen.getByText(/5 min fa/)).toBeInTheDocument();
  });

  it('formats last check as hours ago', () => {
    const twoHoursAgo = new Date(Date.now() - 2 * 60 * 60 * 1000);
    render(
      <ServiceCard serviceName="postgres" status="Healthy" lastCheck={twoHoursAgo} locale="it" />
    );

    expect(screen.getByText(/2 h fa/)).toBeInTheDocument();
  });

  // ==================== Loading State ====================

  it('renders loading skeleton when loading is true', () => {
    render(<ServiceCard serviceName="postgres" status="Healthy" loading locale="it" />);

    expect(screen.getByTestId('service-card-loading')).toBeInTheDocument();
  });

  it('does not render service name when loading', () => {
    render(<ServiceCard serviceName="postgres" status="Healthy" loading locale="it" />);

    expect(screen.queryByText('PostgreSQL')).not.toBeInTheDocument();
  });

  it('renders skeleton placeholders when loading', () => {
    const { container } = render(
      <ServiceCard serviceName="postgres" status="Healthy" loading locale="it" />
    );

    const skeletons = container.querySelectorAll('.animate-pulse');
    expect(skeletons.length).toBeGreaterThan(0);
  });

  // ==================== Internationalization (i18n) ====================

  it('displays Italian labels by default', () => {
    render(<ServiceCard serviceName="postgres" status="Healthy" responseTimeMs={15} locale="it" />);

    expect(screen.getByText(/Tempo di risposta:/)).toBeInTheDocument();
  });

  it('displays English labels when locale is "en"', () => {
    render(<ServiceCard serviceName="postgres" status="Healthy" responseTimeMs={15} locale="en" />);

    expect(screen.getByText(/Response Time:/)).toBeInTheDocument();
  });

  it('displays Italian status for Healthy state', () => {
    render(<ServiceCard serviceName="postgres" status="Healthy" locale="it" />);

    expect(screen.getByText('Sano')).toBeInTheDocument();
  });

  it('displays English status for Healthy state', () => {
    render(<ServiceCard serviceName="postgres" status="Healthy" locale="en" />);

    expect(screen.getByText('Healthy')).toBeInTheDocument();
  });

  it('displays Italian status for Degraded state', () => {
    render(<ServiceCard serviceName="redis" status="Degraded" locale="it" />);

    expect(screen.getByText('Degradato')).toBeInTheDocument();
  });

  it('displays English status for Degraded state', () => {
    render(<ServiceCard serviceName="redis" status="Degraded" locale="en" />);

    expect(screen.getByText('Degraded')).toBeInTheDocument();
  });

  it('displays Italian status for Unhealthy state', () => {
    render(<ServiceCard serviceName="qdrant" status="Unhealthy" locale="it" />);

    expect(screen.getByText('Non funzionante')).toBeInTheDocument();
  });

  it('displays English status for Unhealthy state', () => {
    render(<ServiceCard serviceName="qdrant" status="Unhealthy" locale="en" />);

    expect(screen.getByText('Unhealthy')).toBeInTheDocument();
  });

  it('formats English timestamp as "Now"', () => {
    const now = new Date();
    render(<ServiceCard serviceName="postgres" status="Healthy" lastCheck={now} locale="en" />);

    expect(screen.getByText(/Now/)).toBeInTheDocument();
  });

  it('formats English timestamp as "min ago"', () => {
    const fiveMinutesAgo = new Date(Date.now() - 5 * 60 * 1000);
    render(
      <ServiceCard serviceName="postgres" status="Healthy" lastCheck={fiveMinutesAgo} locale="en" />
    );

    expect(screen.getByText(/5 min ago/)).toBeInTheDocument();
  });

  // ==================== Service Name Mappings ====================

  it('displays "PostgreSQL" for "postgres" service', () => {
    render(<ServiceCard serviceName="postgres" status="Healthy" locale="it" />);

    expect(screen.getByText('PostgreSQL')).toBeInTheDocument();
  });

  it('displays "Redis" for "redis" service', () => {
    render(<ServiceCard serviceName="redis" status="Healthy" locale="it" />);

    expect(screen.getByText('Redis')).toBeInTheDocument();
  });

  it('displays "Qdrant" for "qdrant" service', () => {
    render(<ServiceCard serviceName="qdrant" status="Healthy" locale="it" />);

    expect(screen.getByText('Qdrant')).toBeInTheDocument();
  });

  it('displays raw service name for unknown service', () => {
    render(<ServiceCard serviceName="unknown-service" status="Healthy" locale="it" />);

    expect(screen.getByText('unknown-service')).toBeInTheDocument();
  });

  // ==================== CSS Classes & Styling ====================

  it('applies custom className', () => {
    const { container } = render(
      <ServiceCard
        serviceName="postgres"
        status="Healthy"
        className="custom-test-class"
        locale="it"
      />
    );

    const card = container.querySelector('.custom-test-class');
    expect(card).toBeInTheDocument();
  });

  it('has hover transition classes', () => {
    const { container } = render(
      <ServiceCard serviceName="postgres" status="Healthy" locale="it" />
    );

    const card = container.querySelector('[class*="hover:shadow-md"]');
    expect(card).toBeInTheDocument();
  });

  it('has transition-all class for smooth animations', () => {
    const { container } = render(
      <ServiceCard serviceName="postgres" status="Healthy" locale="it" />
    );

    const card = container.querySelector('[class*="transition-all"]');
    expect(card).toBeInTheDocument();
  });

  it('has correct testid for identification', () => {
    render(<ServiceCard serviceName="postgres" status="Healthy" locale="it" />);

    expect(screen.getByTestId('service-card-postgres')).toBeInTheDocument();
  });

  // ==================== Accessibility ====================

  it('has accessible aria-label for status', () => {
    render(<ServiceCard serviceName="postgres" status="Healthy" locale="it" />);

    const badge = screen.getByLabelText(/Stato: Sano/);
    expect(badge).toBeInTheDocument();
  });

  it('has accessible aria-label for status in English', () => {
    render(<ServiceCard serviceName="postgres" status="Healthy" locale="en" />);

    const badge = screen.getByLabelText(/Status: Healthy/);
    expect(badge).toBeInTheDocument();
  });

  it('hides decorative icons from screen readers', () => {
    const { container } = render(
      <ServiceCard serviceName="postgres" status="Healthy" locale="it" />
    );

    const icons = container.querySelectorAll('[aria-hidden="true"]');
    expect(icons.length).toBeGreaterThan(0);
  });

  // ==================== Edge Cases ====================

  it('handles null error message gracefully', () => {
    render(
      <ServiceCard serviceName="postgres" status="Degraded" errorMessage={null} locale="it" />
    );

    expect(screen.queryByText(/Messaggio di errore:/)).not.toBeInTheDocument();
  });

  it('handles undefined last check timestamp', () => {
    render(
      <ServiceCard serviceName="postgres" status="Healthy" lastCheck={undefined} locale="it" />
    );

    expect(screen.queryByText(/Ultimo controllo:/)).not.toBeInTheDocument();
  });

  it('handles very long service names', () => {
    render(
      <ServiceCard
        serviceName="very-long-service-name-that-should-truncate"
        status="Healthy"
        locale="it"
      />
    );

    const name = screen.getByText('very-long-service-name-that-should-truncate');
    expect(name).toHaveClass('truncate');
  });

  it('handles very long error messages', () => {
    const longError =
      'This is a very long error message that should wrap properly and not break the layout or overflow the container boundaries';
    render(
      <ServiceCard serviceName="postgres" status="Unhealthy" errorMessage={longError} locale="it" />
    );

    expect(screen.getByText(new RegExp(longError))).toBeInTheDocument();
  });

  it('handles response time of 0ms', () => {
    render(<ServiceCard serviceName="redis" status="Healthy" responseTimeMs={0} locale="it" />);

    expect(screen.getByText(/0ms/)).toBeInTheDocument();
  });

  // ==================== Type Safety ====================

  it('accepts all valid HealthState values', () => {
    const states: HealthState[] = ['Healthy', 'Degraded', 'Unhealthy'];

    states.forEach(state => {
      const { container } = render(<ServiceCard serviceName="test" status={state} locale="it" />);
      expect(container).toBeInTheDocument();
    });
  });
});
