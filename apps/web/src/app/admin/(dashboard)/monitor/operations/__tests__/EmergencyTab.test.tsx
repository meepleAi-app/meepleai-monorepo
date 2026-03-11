import { vi, describe, it, expect, beforeEach, afterEach } from 'vitest';

const mockGetActiveEmergencyOverrides = vi.hoisted(() => vi.fn());
const mockActivateEmergencyOverride = vi.hoisted(() => vi.fn());
const mockDeactivateEmergencyOverride = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getActiveEmergencyOverrides: mockGetActiveEmergencyOverrides,
      activateEmergencyOverride: mockActivateEmergencyOverride,
      deactivateEmergencyOverride: mockDeactivateEmergencyOverride,
    },
  },
}));

const mockToast = vi.hoisted(() => vi.fn());
vi.mock('@/hooks/use-toast', () => ({
  useToast: () => ({ toast: mockToast }),
}));

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { EmergencyTab } from '../EmergencyTab';

// ---------- Mock Data ----------

const MOCK_OVERRIDES = [
  {
    action: 'force-ollama-only',
    reason: 'High error rate on OpenRouter',
    adminUserId: 'admin-1',
    targetProvider: null,
    activatedAt: '2026-03-01T09:30:00Z',
    expiresAt: '2026-03-01T10:00:00Z',
    remainingMinutes: 25,
  },
];

describe('EmergencyTab', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.useFakeTimers({ shouldAdvanceTime: true });
    mockGetActiveEmergencyOverrides.mockResolvedValue([]);
    mockActivateEmergencyOverride.mockResolvedValue(undefined);
    mockDeactivateEmergencyOverride.mockResolvedValue(undefined);
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('renders heading "Emergency Controls"', async () => {
    render(<EmergencyTab />);

    await waitFor(() => {
      expect(screen.getByText('Emergency Controls')).toBeInTheDocument();
    });
  });

  it('shows empty state when no active overrides', async () => {
    render(<EmergencyTab />);

    await waitFor(() => {
      expect(screen.getByText('No active emergency overrides')).toBeInTheDocument();
    });

    expect(screen.getByTestId('no-overrides')).toBeInTheDocument();
  });

  it('renders active overrides with details', async () => {
    mockGetActiveEmergencyOverrides.mockResolvedValue(MOCK_OVERRIDES);
    render(<EmergencyTab />);

    await waitFor(() => {
      expect(screen.getByText('force-ollama-only')).toBeInTheDocument();
    });

    expect(screen.getByText('High error rate on OpenRouter')).toBeInTheDocument();
    expect(screen.getByText(/25 min remaining/)).toBeInTheDocument();
    expect(screen.getByTestId('active-overrides')).toBeInTheDocument();
  });

  it('Deactivate button opens Level 1 confirmation dialog', async () => {
    mockGetActiveEmergencyOverrides.mockResolvedValue(MOCK_OVERRIDES);
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    render(<EmergencyTab />);

    await waitFor(() => {
      expect(screen.getByText('force-ollama-only')).toBeInTheDocument();
    });

    await user.click(screen.getByRole('button', { name: /deactivate/i }));

    await waitFor(() => {
      expect(screen.getByText('Deactivate Override')).toBeInTheDocument();
      expect(
        screen.getByText(/remove the "force-ollama-only" emergency override/)
      ).toBeInTheDocument();
    });
  });

  it('deactivate confirmation calls API and shows toast', async () => {
    mockGetActiveEmergencyOverrides.mockResolvedValue(MOCK_OVERRIDES);
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    render(<EmergencyTab />);

    await waitFor(() => {
      expect(screen.getByText('force-ollama-only')).toBeInTheDocument();
    });

    await user.click(screen.getByRole('button', { name: /deactivate/i }));

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /conferma/i })).toBeInTheDocument();
    });

    await user.click(screen.getByRole('button', { name: /conferma/i }));

    await waitFor(() => {
      expect(mockDeactivateEmergencyOverride).toHaveBeenCalledWith('force-ollama-only');
    });

    expect(mockToast).toHaveBeenCalledWith(
      expect.objectContaining({ title: 'Override deactivated: force-ollama-only' })
    );
  });

  it('Activate Override button is disabled when reason is empty', async () => {
    render(<EmergencyTab />);

    await waitFor(() => {
      expect(screen.getByTestId('activate-override-button')).toBeInTheDocument();
    });

    expect(screen.getByTestId('activate-override-button')).toBeDisabled();
  });

  it('Activate Override button is enabled when reason is filled', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    render(<EmergencyTab />);

    await waitFor(() => {
      expect(screen.getByTestId('override-reason-input')).toBeInTheDocument();
    });

    await user.type(screen.getByTestId('override-reason-input'), 'Production incident');

    expect(screen.getByTestId('activate-override-button')).not.toBeDisabled();
  });

  it('Activate button opens Level 2 confirmation dialog', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    render(<EmergencyTab />);

    await waitFor(() => {
      expect(screen.getByTestId('override-reason-input')).toBeInTheDocument();
    });

    await user.type(screen.getByTestId('override-reason-input'), 'Production incident');
    await user.click(screen.getByTestId('activate-override-button'));

    await waitFor(() => {
      expect(screen.getByText('Activate Emergency Override')).toBeInTheDocument();
      // Level 2 requires typing CONFIRM
      expect(
        screen.getByLabelText(/Type CONFIRM to proceed/i)
      ).toBeInTheDocument();
    });
  });

  it('activate confirmation calls API and shows toast after typing CONFIRM', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    render(<EmergencyTab />);

    await waitFor(() => {
      expect(screen.getByTestId('override-reason-input')).toBeInTheDocument();
    });

    await user.type(screen.getByTestId('override-reason-input'), 'Production incident');
    await user.click(screen.getByTestId('activate-override-button'));

    // Level 2 dialog: type CONFIRM
    await waitFor(() => {
      expect(screen.getByLabelText(/Type CONFIRM to proceed/i)).toBeInTheDocument();
    });

    await user.type(screen.getByLabelText(/Type CONFIRM to proceed/i), 'CONFIRM');
    await user.click(screen.getByRole('button', { name: /conferma azione critica/i }));

    await waitFor(() => {
      expect(mockActivateEmergencyOverride).toHaveBeenCalledWith({
        action: 'force-ollama-only',
        reason: 'Production incident',
        durationMinutes: 30,
        targetProvider: undefined,
      });
    });

    expect(mockToast).toHaveBeenCalledWith(
      expect.objectContaining({
        title: 'Emergency override activated: force-ollama-only',
      })
    );
  });

  it('shows provider input only for reset-circuit-breaker action', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    render(<EmergencyTab />);

    await waitFor(() => {
      expect(screen.getByTestId('override-action-select')).toBeInTheDocument();
    });

    // Default action is force-ollama-only — no provider input
    expect(screen.queryByTestId('override-provider-input')).not.toBeInTheDocument();

    // Change to reset-circuit-breaker
    await user.selectOptions(screen.getByTestId('override-action-select'), 'reset-circuit-breaker');

    expect(screen.getByTestId('override-provider-input')).toBeInTheDocument();
  });

  it('shows error toast when loading overrides fails', async () => {
    mockGetActiveEmergencyOverrides.mockRejectedValue(new Error('Network error'));
    render(<EmergencyTab />);

    await waitFor(() => {
      expect(mockToast).toHaveBeenCalledWith(
        expect.objectContaining({
          title: 'Failed to load emergency overrides',
          variant: 'destructive',
        })
      );
    });
  });

  it('shows error toast when deactivation fails', async () => {
    mockGetActiveEmergencyOverrides.mockResolvedValue(MOCK_OVERRIDES);
    mockDeactivateEmergencyOverride.mockRejectedValue(new Error('Failed'));
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    render(<EmergencyTab />);

    await waitFor(() => {
      expect(screen.getByText('force-ollama-only')).toBeInTheDocument();
    });

    await user.click(screen.getByRole('button', { name: /deactivate/i }));

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /conferma/i })).toBeInTheDocument();
    });

    await user.click(screen.getByRole('button', { name: /conferma/i }));

    await waitFor(() => {
      expect(mockToast).toHaveBeenCalledWith(
        expect.objectContaining({
          title: 'Failed to deactivate override',
          variant: 'destructive',
        })
      );
    });
  });

  it('shows error toast when activation fails', async () => {
    mockActivateEmergencyOverride.mockRejectedValue(new Error('Failed'));
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    render(<EmergencyTab />);

    await waitFor(() => {
      expect(screen.getByTestId('override-reason-input')).toBeInTheDocument();
    });

    await user.type(screen.getByTestId('override-reason-input'), 'Test');
    await user.click(screen.getByTestId('activate-override-button'));

    await waitFor(() => {
      expect(screen.getByLabelText(/Type CONFIRM to proceed/i)).toBeInTheDocument();
    });

    await user.type(screen.getByLabelText(/Type CONFIRM to proceed/i), 'CONFIRM');
    await user.click(screen.getByRole('button', { name: /conferma azione critica/i }));

    await waitFor(() => {
      expect(mockToast).toHaveBeenCalledWith(
        expect.objectContaining({
          title: 'Failed to activate override',
          variant: 'destructive',
        })
      );
    });
  });

  it('renders duration input with default value of 30', async () => {
    render(<EmergencyTab />);

    await waitFor(() => {
      expect(screen.getByTestId('override-duration-input')).toBeInTheDocument();
    });

    expect(screen.getByTestId('override-duration-input')).toHaveValue(30);
  });

  it('renders override action descriptions', async () => {
    render(<EmergencyTab />);

    await waitFor(() => {
      expect(
        screen.getByText('Route all LLM requests through local Ollama')
      ).toBeInTheDocument();
    });
  });
});
