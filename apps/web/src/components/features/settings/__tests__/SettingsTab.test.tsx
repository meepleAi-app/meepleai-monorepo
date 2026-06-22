import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { SettingsTab } from '../SettingsTab';

vi.mock('@/lib/api', () => ({
  api: {
    auth: { getTwoFactorStatus: vi.fn().mockResolvedValue({ isEnabled: false, enabledAt: null }) },
  },
}));

// Replace section components with markers so we test the router, not the sections.
vi.mock('../sections/SecuritySection', () => ({
  SecuritySection: () => <div data-testid="sec-security" />,
}));
vi.mock('../sections/ProfileSection', () => ({
  ProfileSection: () => <div data-testid="sec-profile" />,
}));
vi.mock('../sections/PreferencesSection', () => ({
  PreferencesSection: () => <div data-testid="sec-preferences" />,
}));
vi.mock('../sections/ApiKeysSection', () => ({
  ApiKeysSection: () => <div data-testid="sec-api-keys" />,
}));
vi.mock('../sections/AiConsentSection', () => ({
  AiConsentSection: () => <div data-testid="sec-ai-consent" />,
}));

const wrap = (ui: React.ReactNode) =>
  render(
    <QueryClientProvider
      client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}
    >
      {ui}
    </QueryClientProvider>
  );

describe('SettingsTab router', () => {
  it('renders Security section when section=security', () => {
    wrap(<SettingsTab activeSection="security" onChangeSection={() => {}} />);
    expect(screen.getByTestId('sec-security')).toBeInTheDocument();
  });
  it('renders Profile section when section=profile', () => {
    wrap(<SettingsTab activeSection="profile" onChangeSection={() => {}} />);
    expect(screen.getByTestId('sec-profile')).toBeInTheDocument();
  });
  it('renders placeholder for notifications', () => {
    wrap(<SettingsTab activeSection="notifications" onChangeSection={() => {}} />);
    expect(screen.getByText(/in development|coming soon/i)).toBeInTheDocument();
  });
  it('renders placeholder for services', () => {
    wrap(<SettingsTab activeSection="services" onChangeSection={() => {}} />);
    expect(screen.getByText(/in development|coming soon/i)).toBeInTheDocument();
  });
});
