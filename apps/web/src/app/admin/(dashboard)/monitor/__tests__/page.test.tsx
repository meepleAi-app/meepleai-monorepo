import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

vi.mock('../AlertsTab', () => ({ AlertsTab: () => <div data-testid="alerts-tab" /> }));
vi.mock('../CacheTab', () => ({ CacheTab: () => <div data-testid="cache-tab" /> }));
vi.mock('../InfrastructureTab', () => ({
  InfrastructureTab: () => <div data-testid="infra-tab" />,
}));
vi.mock('../CommandCenterTab', () => ({
  CommandCenterTab: () => <div data-testid="command-tab" />,
}));
vi.mock('../TestingTab', () => ({ TestingTab: () => <div data-testid="testing-tab" /> }));
vi.mock('../NavConfig', () => ({ AdminMonitorNavConfig: () => null }));

import AdminMonitorPage from '../page';

describe('AdminMonitorPage', () => {
  it('renders alerts tab by default', async () => {
    const page = await AdminMonitorPage({ searchParams: Promise.resolve({}) });
    render(page);
    expect(screen.getByTestId('alerts-tab')).toBeInTheDocument();
  });

  it('renders cache tab when tab=cache', async () => {
    const page = await AdminMonitorPage({
      searchParams: Promise.resolve({ tab: 'cache' }),
    });
    render(page);
    expect(screen.getByTestId('cache-tab')).toBeInTheDocument();
  });

  it('renders infra tab when tab=infra', async () => {
    const page = await AdminMonitorPage({
      searchParams: Promise.resolve({ tab: 'infra' }),
    });
    render(page);
    expect(screen.getByTestId('infra-tab')).toBeInTheDocument();
  });

  it('renders command center tab', async () => {
    const page = await AdminMonitorPage({
      searchParams: Promise.resolve({ tab: 'command' }),
    });
    render(page);
    expect(screen.getByTestId('command-tab')).toBeInTheDocument();
  });

  it('renders testing tab', async () => {
    const page = await AdminMonitorPage({
      searchParams: Promise.resolve({ tab: 'testing' }),
    });
    render(page);
    expect(screen.getByTestId('testing-tab')).toBeInTheDocument();
  });

  it('renders heading', async () => {
    const page = await AdminMonitorPage({ searchParams: Promise.resolve({}) });
    render(page);
    expect(screen.getByText('Monitor')).toBeInTheDocument();
  });

  it('renders coming soon for export tab', async () => {
    const page = await AdminMonitorPage({
      searchParams: Promise.resolve({ tab: 'export' }),
    });
    render(page);
    expect(screen.getByText('Export users, audit logs, and API keys in bulk.')).toBeInTheDocument();
  });
});
