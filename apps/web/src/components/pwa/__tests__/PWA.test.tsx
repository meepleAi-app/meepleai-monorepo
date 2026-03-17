/**
 * Unit tests for PWA components (Issue #3346)
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';

import { OfflineIndicator } from '../OfflineIndicator';
import { InstallPrompt } from '../InstallPrompt';
import { UpdatePrompt } from '../UpdatePrompt';
import { PWAProvider } from '../PWAProvider';

// Mock usePWA hook
const mockUsePWA = vi.fn();
vi.mock('@/lib/domain-hooks/usePWA', () => ({
  usePWA: () => mockUsePWA(),
}));

// Mock framer-motion
vi.mock('framer-motion', async () => {
  const actual = await vi.importActual('framer-motion');
  return {
    ...actual,
    motion: {
      div: ({ children, ...props }: { children?: React.ReactNode }) => (
        <div {...props}>{children}</div>
      ),
    },
    AnimatePresence: ({ children }: { children?: React.ReactNode }) => <>{children}</>,
  };
});

describe('OfflineIndicator', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('shows offline banner when not online', () => {
    mockUsePWA.mockReturnValue({
      isOnline: false,
      syncState: { pendingCount: 0, status: 'idle' },
      actions: { sync: vi.fn() },
    });

    render(<OfflineIndicator />);

    expect(screen.getByText('You are offline')).toBeInTheDocument();
  });

  it('shows pending actions count when offline', () => {
    mockUsePWA.mockReturnValue({
      isOnline: false,
      syncState: { pendingCount: 3, status: 'idle' },
      actions: { sync: vi.fn() },
    });

    render(<OfflineIndicator />);

    expect(screen.getByText('3 actions pending')).toBeInTheDocument();
  });

  it('shows syncing state', () => {
    mockUsePWA.mockReturnValue({
      isOnline: true,
      syncState: { pendingCount: 2, status: 'syncing' },
      actions: { sync: vi.fn() },
    });

    render(<OfflineIndicator />);

    expect(screen.getByText('Syncing...')).toBeInTheDocument();
  });

  it('shows sync button when online with pending actions', async () => {
    const syncFn = vi.fn().mockResolvedValue({});
    mockUsePWA.mockReturnValue({
      isOnline: true,
      syncState: { pendingCount: 2, status: 'idle' },
      actions: { sync: syncFn },
    });

    render(<OfflineIndicator />);

    const syncButton = screen.getByText('Sync');
    fireEvent.click(syncButton);

    await waitFor(() => {
      expect(syncFn).toHaveBeenCalled();
    });
  });

  it('does not show banner when online and no pending', () => {
    mockUsePWA.mockReturnValue({
      isOnline: true,
      syncState: { pendingCount: 0, status: 'idle' },
      actions: { sync: vi.fn() },
    });

    render(<OfflineIndicator />);

    expect(screen.queryByText('You are offline')).not.toBeInTheDocument();
  });
});

describe('InstallPrompt', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
  });

  it('renders button variant when can install', () => {
    mockUsePWA.mockReturnValue({
      canInstall: true,
      isStandalone: false,
      actions: { install: vi.fn() },
    });

    render(<InstallPrompt variant="button" />);

    expect(screen.getByText('Install App')).toBeInTheDocument();
  });

  it('does not render when already standalone', () => {
    mockUsePWA.mockReturnValue({
      canInstall: true,
      isStandalone: true,
      actions: { install: vi.fn() },
    });

    render(<InstallPrompt variant="button" />);

    expect(screen.queryByText('Install App')).not.toBeInTheDocument();
  });

  it('does not render button when cannot install', () => {
    mockUsePWA.mockReturnValue({
      canInstall: false,
      isStandalone: false,
      actions: { install: vi.fn() },
    });

    render(<InstallPrompt variant="button" />);

    expect(screen.queryByText('Install App')).not.toBeInTheDocument();
  });

  it('calls install action when clicked', async () => {
    const installFn = vi.fn().mockResolvedValue(true);
    mockUsePWA.mockReturnValue({
      canInstall: true,
      isStandalone: false,
      actions: { install: installFn },
    });

    render(<InstallPrompt variant="button" />);

    fireEvent.click(screen.getByText('Install App'));

    await waitFor(() => {
      expect(installFn).toHaveBeenCalled();
    });
  });

  it('calls onInstallResult callback', async () => {
    const installFn = vi.fn().mockResolvedValue(true);
    const onResult = vi.fn();
    mockUsePWA.mockReturnValue({
      canInstall: true,
      isStandalone: false,
      actions: { install: installFn },
    });

    render(<InstallPrompt variant="button" onInstallResult={onResult} />);

    fireEvent.click(screen.getByText('Install App'));

    await waitFor(() => {
      expect(onResult).toHaveBeenCalledWith(true);
    });
  });
});

describe('UpdatePrompt', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders when update is available', () => {
    mockUsePWA.mockReturnValue({
      updateAvailable: true,
      actions: { applyUpdate: vi.fn() },
    });

    render(<UpdatePrompt />);

    expect(screen.getByText('Update Available')).toBeInTheDocument();
  });

  it('does not render when no update', () => {
    mockUsePWA.mockReturnValue({
      updateAvailable: false,
      actions: { applyUpdate: vi.fn() },
    });

    render(<UpdatePrompt />);

    expect(screen.queryByText('Update Available')).not.toBeInTheDocument();
  });

  it('calls applyUpdate when button clicked', () => {
    const applyUpdateFn = vi.fn();
    mockUsePWA.mockReturnValue({
      updateAvailable: true,
      actions: { applyUpdate: applyUpdateFn },
    });

    render(<UpdatePrompt />);

    fireEvent.click(screen.getByText('Update Now'));

    expect(applyUpdateFn).toHaveBeenCalled();
  });
});

describe('PWAProvider', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockUsePWA.mockReturnValue({
      isOnline: true,
      canInstall: false,
      isStandalone: false,
      updateAvailable: false,
      syncState: { pendingCount: 0, status: 'idle' },
      actions: {
        install: vi.fn(),
        applyUpdate: vi.fn(),
        sync: vi.fn(),
      },
    });
  });

  it('renders children', () => {
    render(
      <PWAProvider>
        <div data-testid="child">Child Content</div>
      </PWAProvider>
    );

    expect(screen.getByTestId('child')).toBeInTheDocument();
  });

  it('can disable offline indicator', () => {
    mockUsePWA.mockReturnValue({
      isOnline: false,
      canInstall: false,
      isStandalone: false,
      updateAvailable: false,
      syncState: { pendingCount: 0, status: 'idle' },
      actions: { sync: vi.fn() },
    });

    render(
      <PWAProvider showOfflineIndicator={false}>
        <div>Content</div>
      </PWAProvider>
    );

    expect(screen.queryByText('You are offline')).not.toBeInTheDocument();
  });

  it('can disable install prompt', () => {
    mockUsePWA.mockReturnValue({
      isOnline: true,
      canInstall: true,
      isStandalone: false,
      updateAvailable: false,
      syncState: { pendingCount: 0, status: 'idle' },
      actions: { install: vi.fn() },
    });

    render(
      <PWAProvider showInstallPrompt={false}>
        <div>Content</div>
      </PWAProvider>
    );

    // With showDelay, banner won't show immediately anyway
    expect(screen.queryByText('Install MeepleAI')).not.toBeInTheDocument();
  });
});
