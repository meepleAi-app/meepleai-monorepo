import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { PreferencesSection } from '../sections/PreferencesSection';
import { api } from '@/lib/api';

vi.mock('@/lib/api', () => ({
  api: {
    auth: {
      getPreferences: vi.fn(),
      updatePreferences: vi.fn(),
    },
  },
}));

function wrap(ui: React.ReactNode) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={qc}>{ui}</QueryClientProvider>);
}

beforeEach(() => {
  vi.mocked(api.auth.getPreferences).mockResolvedValue({
    language: 'it',
    emailNotifications: true,
    theme: 'system',
    dataRetentionDays: 90,
  } as any);
  vi.mocked(api.auth.updatePreferences).mockResolvedValue({} as any);
});

describe('PreferencesSection', () => {
  it('renders theme and language from getPreferences', async () => {
    wrap(<PreferencesSection />);
    await waitFor(() => expect(screen.getByLabelText(/theme/i)).toHaveValue('system'));
    expect(screen.getByLabelText(/language/i)).toHaveValue('it');
  });

  it('saves with updated theme via updatePreferences', async () => {
    wrap(<PreferencesSection />);
    await waitFor(() => screen.getByLabelText(/theme/i));
    fireEvent.change(screen.getByLabelText(/theme/i), { target: { value: 'dark' } });
    fireEvent.click(screen.getByTestId('save-preferences-button'));
    await waitFor(() =>
      expect(api.auth.updatePreferences).toHaveBeenCalledWith(
        expect.objectContaining({ theme: 'dark', language: 'it' })
      )
    );
  });

  it('disables save button while saving', async () => {
    let resolve: ((v: any) => void) | undefined;
    vi.mocked(api.auth.updatePreferences).mockReturnValue(
      new Promise(r => {
        resolve = r;
      })
    );
    wrap(<PreferencesSection />);
    await waitFor(() => screen.getByLabelText(/theme/i));
    fireEvent.click(screen.getByTestId('save-preferences-button'));
    await waitFor(() => expect(screen.getByTestId('save-preferences-button')).toBeDisabled());
    resolve?.({});
  });
});
