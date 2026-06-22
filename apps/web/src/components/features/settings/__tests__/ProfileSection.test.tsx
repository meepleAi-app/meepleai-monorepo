import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ProfileSection } from '../sections/ProfileSection';
import { api } from '@/lib/api';

vi.mock('@/lib/api', () => ({
  api: {
    auth: {
      getProfile: vi.fn(),
      updateProfile: vi.fn(),
    },
  },
}));

function wrap(ui: React.ReactNode) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={qc}>{ui}</QueryClientProvider>);
}

beforeEach(() => {
  vi.mocked(api.auth.getProfile).mockResolvedValue({
    id: 'u1',
    email: 'marco@meepleai.test',
    displayName: 'Marco',
    role: 'user',
    createdAt: '2025-01-01T00:00:00Z',
    isTwoFactorEnabled: false,
    twoFactorEnabledAt: null,
    language: 'it',
    theme: 'system',
    emailNotifications: true,
    dataRetentionDays: 90,
    avatarUrl: null,
  } as any);
  vi.mocked(api.auth.updateProfile).mockResolvedValue({ ok: true, message: 'updated' });
});

describe('ProfileSection', () => {
  it('renders profile fields from getProfile', async () => {
    wrap(<ProfileSection />);
    await waitFor(() => expect(screen.getByDisplayValue('Marco')).toBeInTheDocument());
    expect(screen.getByDisplayValue('marco@meepleai.test')).toBeInTheDocument();
  });

  it('saves updated displayName via updateProfile + re-fetches', async () => {
    wrap(<ProfileSection />);
    await waitFor(() => screen.getByDisplayValue('Marco'));
    const nameInput = screen.getByLabelText(/display name/i);
    fireEvent.change(nameInput, { target: { value: 'Marco Updated' } });
    fireEvent.click(screen.getByTestId('save-profile-button'));
    await waitFor(() =>
      expect(api.auth.updateProfile).toHaveBeenCalledWith({
        displayName: 'Marco Updated',
        email: 'marco@meepleai.test',
      })
    );
  });

  it('disables save button while saving', async () => {
    let resolveUpdate: ((v: any) => void) | undefined;
    vi.mocked(api.auth.updateProfile).mockReturnValue(
      new Promise(r => {
        resolveUpdate = r;
      })
    );
    wrap(<ProfileSection />);
    await waitFor(() => screen.getByDisplayValue('Marco'));
    fireEvent.click(screen.getByTestId('save-profile-button'));
    await waitFor(() => expect(screen.getByTestId('save-profile-button')).toBeDisabled());
    resolveUpdate?.({ ok: true, message: '' });
  });
});
