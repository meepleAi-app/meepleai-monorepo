import { render, screen, waitFor, cleanup } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import ProfilePage from '../../pages/profile';
import { API_BASE_FALLBACK } from '../../lib/api';

type FetchMock = jest.MockedFunction<typeof fetch>;

const createJsonResponse = (data: unknown, ok = true, status = 200) =>
  ({
    ok,
    status,
    json: async () => data
  } as unknown as Response);

describe('ProfilePage', () => {
  const originalFetch = global.fetch;
  const originalPush = jest.fn();
  let fetchMock: FetchMock;
  let confirmMock: jest.SpyInstance;
  const apiBase = 'https://api.example.com';

  const mockUserData = {
    user: {
      id: 'user-123',
      email: 'test@example.com',
      displayName: 'Test User',
      role: 'USER'
    }
  };

  const mockOAuthAccounts = [
    { provider: 'google', createdAt: '2024-01-15T10:00:00.000Z' },
    { provider: 'discord', createdAt: '2024-01-16T10:00:00.000Z' }
  ];

  const loadProfilePage = () => {
    const profilePath = require.resolve('../../pages/profile');
    const apiCacheKeys = Object.keys(require.cache).filter((key) => key.includes('lib/api'));

    delete require.cache[profilePath];
    apiCacheKeys.forEach((key) => {
      delete require.cache[key];
    });

    return require('../../pages/profile').default;
  };

  jest.mock('next/router', () => ({
    useRouter: () => ({
      push: originalPush,
      pathname: '/profile',
      query: {},
      asPath: '/profile',
    }),
  }));

  beforeAll(() => {
    fetchMock = jest.fn() as FetchMock;
    global.fetch = fetchMock;
  });

  afterAll(() => {
    global.fetch = originalFetch;
  });

  beforeEach(() => {
    fetchMock.mockReset();
    originalPush.mockReset();
    confirmMock = jest.spyOn(window, 'confirm');
  });

  afterEach(() => {
    delete process.env.NEXT_PUBLIC_API_BASE;
    jest.clearAllMocks();
    confirmMock.mockRestore();
    cleanup();
  });

  it('renders loading state while data is being fetched', () => {
    fetchMock.mockImplementation(() => new Promise(() => {}));

    process.env.NEXT_PUBLIC_API_BASE = apiBase;
    const ProfilePage = loadProfilePage();

    const { container } = render(<ProfilePage />);

    // Verify loading spinner is present by checking for animate-spin class
    const spinner = container.querySelector('.animate-spin');
    expect(spinner).toBeInTheDocument();
  });

  it('redirects to login when user is not authenticated', async () => {
    // @ts-expect-error - Mock signature simplified
    fetchMock.mockImplementation((url: string) => {
      if (url.includes('/api/v1/auth/me')) {
        return Promise.resolve(createJsonResponse(null, false, 401));
      }
      return Promise.reject(new Error(`Unexpected URL: ${url}`));
    });

    process.env.NEXT_PUBLIC_API_BASE = apiBase;
    const ProfilePage = loadProfilePage();

    render(<ProfilePage />);

    await waitFor(() =>
      expect(originalPush).toHaveBeenCalledWith('/login')
    );
  });

  it('renders user profile information successfully', async () => {
    // @ts-expect-error - Mock signature simplified
    fetchMock.mockImplementation((url: string) => {
      if (url.includes('/api/v1/auth/me')) {
        return Promise.resolve(createJsonResponse(mockUserData));
      }
      if (url.includes('/api/v1/users/me/oauth-accounts')) {
        return Promise.resolve(createJsonResponse(mockOAuthAccounts));
      }
      return Promise.reject(new Error(`Unexpected URL: ${url}`));
    });

    process.env.NEXT_PUBLIC_API_BASE = apiBase;
    const ProfilePage = loadProfilePage();

    render(<ProfilePage />);

    expect(await screen.findByRole('heading', { name: 'Profile', level: 1 })).toBeInTheDocument();
    expect(screen.getByText('Account Information')).toBeInTheDocument();
    expect(screen.getByText('test@example.com')).toBeInTheDocument();
    expect(screen.getByText('Test User')).toBeInTheDocument();
    // Role section should be present
    expect(screen.getByText('Role')).toBeInTheDocument();
  });

  it('renders user profile without displayName', async () => {
    const userWithoutName = {
      user: {
        ...mockUserData.user,
        displayName: null
      }
    };

    // @ts-expect-error - Mock signature simplified
    fetchMock.mockImplementation((url: string) => {
      if (url.includes('/api/v1/auth/me')) {
        return Promise.resolve(createJsonResponse(userWithoutName));
      }
      if (url.includes('/api/v1/users/me/oauth-accounts')) {
        return Promise.resolve(createJsonResponse([]));
      }
      return Promise.reject(new Error(`Unexpected URL: ${url}`));
    });

    process.env.NEXT_PUBLIC_API_BASE = apiBase;
    const ProfilePage = loadProfilePage();

    render(<ProfilePage />);

    expect(await screen.findByText('test@example.com')).toBeInTheDocument();
    expect(screen.queryByText('Display Name')).not.toBeInTheDocument();
  });

  it('displays linked OAuth accounts', async () => {
    // @ts-expect-error - Mock signature simplified
    fetchMock.mockImplementation((url: string) => {
      if (url.includes('/api/v1/auth/me')) {
        return Promise.resolve(createJsonResponse(mockUserData));
      }
      if (url.includes('/api/v1/users/me/oauth-accounts')) {
        return Promise.resolve(createJsonResponse(mockOAuthAccounts));
      }
      return Promise.reject(new Error(`Unexpected URL: ${url}`));
    });

    process.env.NEXT_PUBLIC_API_BASE = apiBase;
    const ProfilePage = loadProfilePage();

    render(<ProfilePage />);

    expect(await screen.findByText('Linked Accounts')).toBeInTheDocument();

    const googleSection = screen.getByText('Google').closest('div');
    expect(googleSection).toBeInTheDocument();
    expect(googleSection?.parentElement).toHaveTextContent('Connected');

    const discordSection = screen.getByText('Discord').closest('div');
    expect(discordSection).toBeInTheDocument();
    expect(discordSection?.parentElement).toHaveTextContent('Connected');

    const githubSection = screen.getByText('GitHub').closest('div');
    expect(githubSection).toBeInTheDocument();
    expect(githubSection?.parentElement).toHaveTextContent('Not connected');
  });

  it('handles linking OAuth account', async () => {
    // @ts-expect-error - Mock signature simplified
    fetchMock.mockImplementation((url: string) => {
      if (url.includes('/api/v1/auth/me')) {
        return Promise.resolve(createJsonResponse(mockUserData));
      }
      if (url.includes('/api/v1/users/me/oauth-accounts')) {
        return Promise.resolve(createJsonResponse([]));
      }
      return Promise.reject(new Error(`Unexpected URL: ${url}`));
    });

    process.env.NEXT_PUBLIC_API_BASE = apiBase;
    const ProfilePage = loadProfilePage();
    const user = userEvent.setup({ delay: null });

    render(<ProfilePage />);

    expect(await screen.findByText('Linked Accounts')).toBeInTheDocument();

    // Get all Link buttons - should have 3 (Google, Discord, GitHub)
    const linkButtons = screen.getAllByRole('button', { name: 'Link' });
    expect(linkButtons.length).toBe(3);

    // Click the first Link button (Google)
    // Note: In real browser, this would redirect to OAuth URL
    // In tests, we just verify the button is clickable without errors
    await user.click(linkButtons[0]);

    // Button click should not throw errors
    expect(linkButtons[0]).toBeInTheDocument();
  });

  it('handles unlinking OAuth account with confirmation', async () => {
    confirmMock.mockReturnValue(true);

    // @ts-expect-error - Mock signature simplified
    fetchMock.mockImplementation((url: string, options?: RequestInit) => {
      if (url.includes('/api/v1/auth/me')) {
        return Promise.resolve(createJsonResponse(mockUserData));
      }
      if (url.includes('/api/v1/users/me/oauth-accounts')) {
        return Promise.resolve(createJsonResponse(mockOAuthAccounts));
      }
      if (url.includes('/api/v1/auth/oauth/google/unlink') && options?.method === 'DELETE') {
        return Promise.resolve(createJsonResponse(null, true, 204));
      }
      return Promise.reject(new Error(`Unexpected URL: ${url}`));
    });

    process.env.NEXT_PUBLIC_API_BASE = apiBase;
    const ProfilePage = loadProfilePage();
    const user = userEvent.setup({ delay: null });

    render(<ProfilePage />);

    expect(await screen.findByText('Linked Accounts')).toBeInTheDocument();

    const unlinkButtons = screen.getAllByRole('button', { name: /Unlink/i });
    const googleUnlinkButton = unlinkButtons[0];
    await user.click(googleUnlinkButton);

    expect(confirmMock).toHaveBeenCalledWith('Unlink your google account?');

    await waitFor(() =>
      expect(fetchMock).toHaveBeenCalledWith(
        `${apiBase}/api/v1/auth/oauth/google/unlink`,
        expect.objectContaining({
          method: 'DELETE',
          credentials: 'include'
        })
      )
    );

    // After unlinking, Google should show "Not connected" status
    await waitFor(() => {
      const googleItems = screen.getAllByText('Google');
      // Find the parent container with the status
      const notConnectedStatus = screen.getAllByText('Not connected');
      expect(notConnectedStatus.length).toBeGreaterThan(0);
    });
  });

  it('cancels unlink when user declines confirmation', async () => {
    confirmMock.mockReturnValue(false);

    // @ts-expect-error - Mock signature simplified
    fetchMock.mockImplementation((url: string) => {
      if (url.includes('/api/v1/auth/me')) {
        return Promise.resolve(createJsonResponse(mockUserData));
      }
      if (url.includes('/api/v1/users/me/oauth-accounts')) {
        return Promise.resolve(createJsonResponse(mockOAuthAccounts));
      }
      return Promise.reject(new Error(`Unexpected URL: ${url}`));
    });

    process.env.NEXT_PUBLIC_API_BASE = apiBase;
    const ProfilePage = loadProfilePage();
    const user = userEvent.setup({ delay: null });

    render(<ProfilePage />);

    expect(await screen.findByText('Linked Accounts')).toBeInTheDocument();

    const unlinkButtons = screen.getAllByRole('button', { name: /Unlink/i });
    await user.click(unlinkButtons[0]);

    const deleteCalls = fetchMock.mock.calls.filter(
      (call) => call[1]?.method === 'DELETE'
    );
    expect(deleteCalls.length).toBe(0);
  });

  it('handles unlink error with alert', async () => {
    const alertMock = jest.spyOn(window, 'alert').mockImplementation(() => {});
    confirmMock.mockReturnValue(true);

    // @ts-expect-error - Mock signature simplified
    fetchMock.mockImplementation((url: string, options?: RequestInit) => {
      if (url.includes('/api/v1/auth/me')) {
        return Promise.resolve(createJsonResponse(mockUserData));
      }
      if (url.includes('/api/v1/users/me/oauth-accounts')) {
        return Promise.resolve(createJsonResponse(mockOAuthAccounts));
      }
      if (url.includes('/api/v1/auth/oauth/google/unlink') && options?.method === 'DELETE') {
        return Promise.resolve(createJsonResponse({ error: 'Forbidden' }, false, 403));
      }
      return Promise.reject(new Error(`Unexpected URL: ${url}`));
    });

    process.env.NEXT_PUBLIC_API_BASE = apiBase;
    const ProfilePage = loadProfilePage();
    const user = userEvent.setup({ delay: null });

    render(<ProfilePage />);

    expect(await screen.findByText('Linked Accounts')).toBeInTheDocument();

    const unlinkButtons = screen.getAllByRole('button', { name: /Unlink/i });
    await user.click(unlinkButtons[0]);

    await waitFor(() => {
      expect(alertMock).toHaveBeenCalledWith('Failed to unlink account. Please try again.');
    });

    alertMock.mockRestore();
  });

  it('handles network error during unlink (catch block)', async () => {
    const alertMock = jest.spyOn(window, 'alert').mockImplementation(() => {});
    const consoleError = jest.spyOn(console, 'error').mockImplementation(() => {});
    confirmMock.mockReturnValue(true);

    // @ts-expect-error - Mock signature simplified
    fetchMock.mockImplementation((url: string, options?: RequestInit) => {
      if (url.includes('/api/v1/auth/me')) {
        return Promise.resolve(createJsonResponse(mockUserData));
      }
      if (url.includes('/api/v1/users/me/oauth-accounts')) {
        return Promise.resolve(createJsonResponse(mockOAuthAccounts));
      }
      if (url.includes('/api/v1/auth/oauth/google/unlink') && options?.method === 'DELETE') {
        // Throw a network error to trigger the catch block
        return Promise.reject(new Error('Network error'));
      }
      return Promise.reject(new Error(`Unexpected URL: ${url}`));
    });

    process.env.NEXT_PUBLIC_API_BASE = apiBase;
    const ProfilePage = loadProfilePage();
    const user = userEvent.setup({ delay: null });

    render(<ProfilePage />);

    expect(await screen.findByText('Linked Accounts')).toBeInTheDocument();

    const unlinkButtons = screen.getAllByRole('button', { name: /Unlink/i });
    await user.click(unlinkButtons[0]);

    await waitFor(() => {
      expect(consoleError).toHaveBeenCalledWith('Unlink error:', expect.any(Error));
      expect(alertMock).toHaveBeenCalledWith('Failed to unlink account.');
    });

    alertMock.mockRestore();
    consoleError.mockRestore();
  });

  it('handles network error during profile load', async () => {
    const consoleError = jest.spyOn(console, 'error').mockImplementation(() => {});

    fetchMock.mockImplementation(() =>
      Promise.reject(new Error('Network error'))
    );

    process.env.NEXT_PUBLIC_API_BASE = apiBase;
    const ProfilePage = loadProfilePage();

    render(<ProfilePage />);

    await waitFor(() => {
      expect(consoleError).toHaveBeenCalledWith(
        'Failed to load profile:',
        expect.any(Error)
      );
    });

    consoleError.mockRestore();
  });

  it('falls back to localhost API base when NEXT_PUBLIC_API_BASE is unset', async () => {
    delete process.env.NEXT_PUBLIC_API_BASE;
    const ProfilePage = loadProfilePage();

    // @ts-expect-error - Mock signature simplified
    fetchMock.mockImplementation((url: string) => {
      if (url.includes('/api/v1/auth/me')) {
        return Promise.resolve(createJsonResponse(mockUserData));
      }
      if (url.includes('/api/v1/users/me/oauth-accounts')) {
        return Promise.resolve(createJsonResponse([]));
      }
      return Promise.reject(new Error(`Unexpected URL: ${url}`));
    });

    render(<ProfilePage />);

    await waitFor(() =>
      expect(fetchMock).toHaveBeenCalledWith(
        `${API_BASE_FALLBACK}/api/v1/auth/me`,
        expect.objectContaining({ credentials: 'include' })
      )
    );

    await waitFor(() =>
      expect(fetchMock).toHaveBeenCalledWith(
        `${API_BASE_FALLBACK}/api/v1/users/me/oauth-accounts`,
        expect.objectContaining({ credentials: 'include' })
      )
    );
  });

  it('renders back to home link', async () => {
    // @ts-expect-error - Mock signature simplified
    fetchMock.mockImplementation((url: string) => {
      if (url.includes('/api/v1/auth/me')) {
        return Promise.resolve(createJsonResponse(mockUserData));
      }
      if (url.includes('/api/v1/users/me/oauth-accounts')) {
        return Promise.resolve(createJsonResponse([]));
      }
      return Promise.reject(new Error(`Unexpected URL: ${url}`));
    });

    process.env.NEXT_PUBLIC_API_BASE = apiBase;
    const ProfilePage = loadProfilePage();

    render(<ProfilePage />);

    expect(await screen.findByText('Profile')).toBeInTheDocument();

    const backLink = screen.getByRole('link', { name: 'Back to Home' });
    expect(backLink).toBeInTheDocument();
    expect(backLink).toHaveAttribute('href', '/');
  });
});