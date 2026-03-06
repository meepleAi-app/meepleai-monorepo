import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

vi.mock('../OverviewTab', () => ({ OverviewTab: () => <div data-testid="overview-tab" /> }));
vi.mock('../AiUsageTab', () => ({ AiUsageTab: () => <div data-testid="ai-usage-tab" /> }));
vi.mock('../AuditLogTab', () => ({ AuditLogTab: () => <div data-testid="audit-tab" /> }));
vi.mock('../ReportsTab', () => ({ ReportsTab: () => <div data-testid="reports-tab" /> }));
vi.mock('../ApiKeysTab', () => ({ ApiKeysTab: () => <div data-testid="api-keys-tab" /> }));
vi.mock('../NavConfig', () => ({ AdminAnalyticsNavConfig: () => null }));

import AdminAnalyticsPage from '../page';

describe('AdminAnalyticsPage', () => {
  it('renders overview tab by default', async () => {
    const page = await AdminAnalyticsPage({ searchParams: Promise.resolve({}) });
    render(page);
    expect(screen.getByTestId('overview-tab')).toBeInTheDocument();
  });

  it('renders ai-usage tab when tab=ai-usage', async () => {
    const page = await AdminAnalyticsPage({
      searchParams: Promise.resolve({ tab: 'ai-usage' }),
    });
    render(page);
    expect(screen.getByTestId('ai-usage-tab')).toBeInTheDocument();
  });

  it('renders audit tab when tab=audit', async () => {
    const page = await AdminAnalyticsPage({
      searchParams: Promise.resolve({ tab: 'audit' }),
    });
    render(page);
    expect(screen.getByTestId('audit-tab')).toBeInTheDocument();
  });

  it('renders reports tab when tab=reports', async () => {
    const page = await AdminAnalyticsPage({
      searchParams: Promise.resolve({ tab: 'reports' }),
    });
    render(page);
    expect(screen.getByTestId('reports-tab')).toBeInTheDocument();
  });

  it('renders api-keys tab when tab=api-keys', async () => {
    const page = await AdminAnalyticsPage({
      searchParams: Promise.resolve({ tab: 'api-keys' }),
    });
    render(page);
    expect(screen.getByTestId('api-keys-tab')).toBeInTheDocument();
  });

  it('renders heading and description', async () => {
    const page = await AdminAnalyticsPage({ searchParams: Promise.resolve({}) });
    render(page);
    expect(screen.getByText('Analytics')).toBeInTheDocument();
    expect(
      screen.getByText('Usage statistics, AI analytics, audit logs, and reports.')
    ).toBeInTheDocument();
  });
});
