import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { usePathname, useSearchParams } from 'next/navigation';

import { MonitorCrumbs } from '../MonitorCrumbs';

vi.mock('next/navigation', () => ({
  usePathname: vi.fn(),
  useSearchParams: vi.fn(),
}));

const mockUsePathname = usePathname as ReturnType<typeof vi.fn>;
const mockUseSearchParams = useSearchParams as ReturnType<typeof vi.fn>;

function mockNav(pathname: string, tab?: string) {
  mockUsePathname.mockReturnValue(pathname);
  mockUseSearchParams.mockReturnValue({
    get: (key: string) => (key === 'tab' ? (tab ?? null) : null),
  });
}

describe('MonitorCrumbs', () => {
  it('renders Alerts label at monitor root with no tab param', () => {
    mockNav('/admin/monitor');
    render(<MonitorCrumbs />);
    expect(screen.getByText(/Alerts/)).toBeInTheDocument();
  });

  it('renders Alerts label when tab=alerts', () => {
    mockNav('/admin/monitor', 'alerts');
    render(<MonitorCrumbs />);
    expect(screen.getByText(/Alerts/)).toBeInTheDocument();
  });

  it('renders Metrics label when tab=cache', () => {
    mockNav('/admin/monitor', 'cache');
    render(<MonitorCrumbs />);
    expect(screen.getByText(/Metrics/)).toBeInTheDocument();
  });

  it('renders Infrastructure label when tab=infra', () => {
    mockNav('/admin/monitor', 'infra');
    render(<MonitorCrumbs />);
    expect(screen.getByText(/Infrastructure/)).toBeInTheDocument();
  });

  it('renders Command Center label when tab=command', () => {
    mockNav('/admin/monitor', 'command');
    render(<MonitorCrumbs />);
    expect(screen.getByText(/Command Center/)).toBeInTheDocument();
  });

  it('renders Testing label when tab=testing', () => {
    mockNav('/admin/monitor', 'testing');
    render(<MonitorCrumbs />);
    expect(screen.getByText(/Testing/)).toBeInTheDocument();
  });

  it('renders MAU label when tab=mau', () => {
    mockNav('/admin/monitor', 'mau');
    render(<MonitorCrumbs />);
    expect(screen.getByText(/MAU/)).toBeInTheDocument();
  });

  it('renders Containers label when tab=containers', () => {
    mockNav('/admin/monitor', 'containers');
    render(<MonitorCrumbs />);
    expect(screen.getByText(/Containers/)).toBeInTheDocument();
  });

  it('renders Logs label when tab=logs', () => {
    mockNav('/admin/monitor', 'logs');
    render(<MonitorCrumbs />);
    expect(screen.getByText(/Logs/)).toBeInTheDocument();
  });

  it('renders Grafana label when tab=grafana', () => {
    mockNav('/admin/monitor', 'grafana');
    render(<MonitorCrumbs />);
    expect(screen.getByText(/Grafana/)).toBeInTheDocument();
  });

  it('renders Bulk Export label when tab=export', () => {
    mockNav('/admin/monitor', 'export');
    render(<MonitorCrumbs />);
    expect(screen.getByText(/Bulk Export/)).toBeInTheDocument();
  });

  it('renders Email label when tab=email', () => {
    mockNav('/admin/monitor', 'email');
    render(<MonitorCrumbs />);
    expect(screen.getByText(/Email/)).toBeInTheDocument();
  });

  it('renders History label when tab=history', () => {
    mockNav('/admin/monitor', 'history');
    render(<MonitorCrumbs />);
    expect(screen.getByText(/History/)).toBeInTheDocument();
  });

  it('renders Events label when tab=events', () => {
    mockNav('/admin/monitor', 'events');
    render(<MonitorCrumbs />);
    expect(screen.getByText(/Events/)).toBeInTheDocument();
  });

  it('renders Containers label for /admin/monitor/containers sub-route', () => {
    mockNav('/admin/monitor/containers');
    render(<MonitorCrumbs />);
    expect(screen.getByText(/Containers/)).toBeInTheDocument();
  });

  it('renders Logs label for /admin/monitor/logs sub-route', () => {
    mockNav('/admin/monitor/logs');
    render(<MonitorCrumbs />);
    expect(screen.getByText(/Logs/)).toBeInTheDocument();
  });

  it('renders Grafana label for /admin/monitor/grafana sub-route', () => {
    mockNav('/admin/monitor/grafana');
    render(<MonitorCrumbs />);
    expect(screen.getByText(/Grafana/)).toBeInTheDocument();
  });

  it('renders breadcrumb nav with aria-label', () => {
    mockNav('/admin/monitor');
    render(<MonitorCrumbs />);
    expect(screen.getByRole('navigation', { name: /breadcrumb/i })).toBeInTheDocument();
  });
});
