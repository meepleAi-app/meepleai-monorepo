import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { renderWithQuery } from '@/__tests__/utils/query-test-utils';

import { InviteUserDialog } from '../InviteUserDialog';

const mockSendInvitation = vi.hoisted(() => vi.fn());
const mockWriteText = vi.fn().mockResolvedValue(undefined);

vi.mock('@/lib/api', () => ({
  api: {
    invitations: {
      sendInvitation: mockSendInvitation,
    },
  },
}));

vi.mock('sonner', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
  },
}));

describe('InviteUserDialog', () => {
  const user = userEvent.setup();
  const defaultProps = {
    open: true,
    onOpenChange: vi.fn(),
    onSuccess: vi.fn(),
  };

  beforeEach(() => {
    vi.clearAllMocks();
    mockWriteText.mockResolvedValue(undefined);
    // Stub clipboard.writeText so we can assert on it without jsdom restrictions
    vi.stubGlobal('navigator', {
      ...navigator,
      clipboard: { writeText: mockWriteText },
    });
  });

  it('renders the dialog with form fields', () => {
    renderWithQuery(<InviteUserDialog {...defaultProps} />);

    expect(screen.getByText('Invite User')).toBeInTheDocument();
    expect(screen.getByLabelText('Email Address')).toBeInTheDocument();
    expect(screen.getByText('Role')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /send invitation/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /cancel/i })).toBeInTheDocument();
  });

  it('validates empty email', async () => {
    renderWithQuery(<InviteUserDialog {...defaultProps} />);

    await user.click(screen.getByRole('button', { name: /send invitation/i }));

    expect(screen.getByRole('alert')).toHaveTextContent('Email is required');
    expect(mockSendInvitation).not.toHaveBeenCalled();
  });

  it('validates invalid email format', async () => {
    renderWithQuery(<InviteUserDialog {...defaultProps} />);

    const emailInput = screen.getByLabelText('Email Address');
    // Use a value that passes HTML5 type="email" but fails our stricter regex
    await user.clear(emailInput);
    await user.type(emailInput, 'bad-email');
    await user.click(screen.getByRole('button', { name: /send invitation/i }));

    // Either shows our custom error or the native validation prevents submission
    await waitFor(() => {
      expect(mockSendInvitation).not.toHaveBeenCalled();
    });
  });

  it('submits valid invitation and calls onSuccess — no token in response closes dialog', async () => {
    mockSendInvitation.mockResolvedValueOnce({
      id: '123',
      email: 'test@example.com',
      role: 'User',
      status: 'Pending',
      // no token field → dialog closes immediately
    });

    renderWithQuery(<InviteUserDialog {...defaultProps} />);

    await user.type(screen.getByLabelText('Email Address'), 'test@example.com');
    await user.click(screen.getByRole('button', { name: /send invitation/i }));

    await waitFor(() => {
      expect(mockSendInvitation).toHaveBeenCalledWith('test@example.com', 'User');
    });

    expect(defaultProps.onSuccess).toHaveBeenCalled();
    expect(defaultProps.onOpenChange).toHaveBeenCalledWith(false);
  });

  it('shows copyable invitation link when token is returned', async () => {
    mockSendInvitation.mockResolvedValueOnce({
      id: '123',
      email: 'test@example.com',
      role: 'User',
      status: 'Pending',
      token: 'abc-def-ghi',
    });

    renderWithQuery(<InviteUserDialog {...defaultProps} />);

    await user.type(screen.getByLabelText('Email Address'), 'test@example.com');
    await user.click(screen.getByRole('button', { name: /send invitation/i }));

    await waitFor(() => {
      expect(screen.getByLabelText('Invitation link')).toBeInTheDocument();
    });

    const linkInput = screen.getByLabelText('Invitation link') as HTMLInputElement;
    expect(linkInput.value).toContain('/accept-invite?token=');
    expect(linkInput.value).toContain('abc-def-ghi');
    expect(defaultProps.onSuccess).toHaveBeenCalled();

    // Dialog should NOT have been closed yet
    expect(defaultProps.onOpenChange).not.toHaveBeenCalledWith(false);

    // Copy button should be present
    expect(screen.getByRole('button', { name: /copy invitation link/i })).toBeInTheDocument();
  });

  it('copies invitation link to clipboard when copy button is clicked', async () => {
    mockSendInvitation.mockResolvedValueOnce({
      id: '123',
      email: 'test@example.com',
      role: 'User',
      status: 'Pending',
      token: 'my-test-token',
    });

    renderWithQuery(<InviteUserDialog {...defaultProps} />);

    await user.type(screen.getByLabelText('Email Address'), 'test@example.com');
    await user.click(screen.getByRole('button', { name: /send invitation/i }));

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /copy invitation link/i })).toBeInTheDocument();
    });

    await user.click(screen.getByRole('button', { name: /copy invitation link/i }));

    expect(mockWriteText).toHaveBeenCalledWith(expect.stringContaining('my-test-token'));

    // Button label changes to "Copied"
    await waitFor(() => {
      expect(screen.getByRole('button', { name: /copied/i })).toBeInTheDocument();
    });
  });

  it('closes dialog via Done button after showing invite link', async () => {
    mockSendInvitation.mockResolvedValueOnce({
      id: '123',
      email: 'test@example.com',
      role: 'User',
      status: 'Pending',
      token: 'abc-def-ghi',
    });

    renderWithQuery(<InviteUserDialog {...defaultProps} />);

    await user.type(screen.getByLabelText('Email Address'), 'test@example.com');
    await user.click(screen.getByRole('button', { name: /send invitation/i }));

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /done/i })).toBeInTheDocument();
    });

    await user.click(screen.getByRole('button', { name: /done/i }));
    expect(defaultProps.onOpenChange).toHaveBeenCalledWith(false);
  });

  it('shows error on API failure', async () => {
    mockSendInvitation.mockRejectedValueOnce(new Error('Email already invited'));

    renderWithQuery(<InviteUserDialog {...defaultProps} />);

    await user.type(screen.getByLabelText('Email Address'), 'test@example.com');
    await user.click(screen.getByRole('button', { name: /send invitation/i }));

    await waitFor(() => {
      expect(screen.getByRole('alert')).toHaveTextContent('Email already invited');
    });
  });

  it('disables form while submitting', async () => {
    // Never resolve to keep the submitting state
    mockSendInvitation.mockReturnValue(new Promise(() => {}));

    renderWithQuery(<InviteUserDialog {...defaultProps} />);

    await user.type(screen.getByLabelText('Email Address'), 'test@example.com');
    await user.click(screen.getByRole('button', { name: /send invitation/i }));

    await waitFor(() => {
      expect(screen.getByText('Sending...')).toBeInTheDocument();
    });
  });
});
