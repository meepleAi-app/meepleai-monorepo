import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

vi.mock('../GeneralTab', () => ({ GeneralTab: () => <div data-testid="general-tab" /> }));
vi.mock('../LimitsTab', () => ({ LimitsTab: () => <div data-testid="limits-tab" /> }));
vi.mock('../FeatureFlagsWrapper', () => ({
  FeatureFlagsWrapper: () => <div data-testid="flags-tab" />,
}));
vi.mock('../RateLimitsTab', () => ({
  RateLimitsTab: () => <div data-testid="rate-limits-tab" />,
}));
vi.mock('../NavConfig', () => ({ AdminConfigNavConfig: () => null }));
vi.mock('@/components/admin/layout/AdminTabPersistence', () => ({
  AdminTabPersistence: () => null,
}));

import AdminConfigPage from '../page';

describe('AdminConfigPage', () => {
  it('renders general tab by default', async () => {
    const page = await AdminConfigPage({ searchParams: Promise.resolve({}) });
    render(page);
    expect(screen.getByTestId('general-tab')).toBeInTheDocument();
  });

  it('renders limits tab when tab=limits', async () => {
    const page = await AdminConfigPage({
      searchParams: Promise.resolve({ tab: 'limits' }),
    });
    render(page);
    expect(screen.getByTestId('limits-tab')).toBeInTheDocument();
  });

  it('renders flags tab when tab=flags', async () => {
    const page = await AdminConfigPage({
      searchParams: Promise.resolve({ tab: 'flags' }),
    });
    render(page);
    expect(screen.getByTestId('flags-tab')).toBeInTheDocument();
  });

  it('renders rate-limits tab when tab=rate-limits', async () => {
    const page = await AdminConfigPage({
      searchParams: Promise.resolve({ tab: 'rate-limits' }),
    });
    render(page);
    expect(screen.getByTestId('rate-limits-tab')).toBeInTheDocument();
  });

  it('renders heading and description', async () => {
    const page = await AdminConfigPage({ searchParams: Promise.resolve({}) });
    render(page);
    expect(screen.getByText('Configuration')).toBeInTheDocument();
    expect(
      screen.getByText('System settings, feature flags, and rate limits.')
    ).toBeInTheDocument();
  });

  it('renders nothing for unknown tab (n8n)', async () => {
    const page = await AdminConfigPage({
      searchParams: Promise.resolve({ tab: 'n8n' }),
    });
    render(page);
    // n8n tab no longer exists — renderTabContent returns null for unknown tabs
    expect(screen.getByText('Configuration')).toBeInTheDocument();
  });

  it('renders nothing for unknown tab (wizard)', async () => {
    const page = await AdminConfigPage({
      searchParams: Promise.resolve({ tab: 'wizard' }),
    });
    render(page);
    // wizard tab no longer exists — renderTabContent returns null for unknown tabs
    expect(screen.getByText('Configuration')).toBeInTheDocument();
  });
});
