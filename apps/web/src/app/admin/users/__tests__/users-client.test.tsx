import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { AdminPageClient } from '../client';
import { AuthProvider } from '@/components/auth/AuthProvider';
import type { AuthUser } from '@/types/auth';
import { api } from '@/lib/api';

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getUsers: vi.fn(),
      createUser: vi.fn(),
      updateUser: vi.fn(),
      deleteUser: vi.fn(),
    },
  },
}));

vi.mock('@/components/auth/AuthProvider', async () => {
  const actual = await vi.importActual('@/components/auth/AuthProvider');
  return {
    ...actual,
    useAuthUser: () => ({
      user: mockUser,
      loading: false,
    }),
  };
});

const mockUser: AuthUser = {
  id: 'admin-id',
  email: 'admin@meepleai.com',
  displayName: 'Admin User',
  role: 'Admin',
  createdAt: new Date().toISOString(),
  isTwoFactorEnabled: false,
};

const mockUsersData = {
  items: [
    {
      id: 'user-1',
      email: 'user1@example.com',
      displayName: 'User One',
      role: 'User',
      createdAt: '2025-01-01T00:00:00Z',
      lastSeenAt: '2025-12-10T15:30:00Z',
    },
    {
      id: 'user-2',
      email: 'user2@example.com',
      displayName: 'User Two',
      role: 'Admin',
      createdAt: '2025-01-15T00:00:00Z',
      lastSeenAt: '2025-12-11T10:20:00Z',
    },
  ],
  total: 2,
};

describe('Users AdminPageClient', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(api.admin.getUsers).mockResolvedValue(mockUsersData);
  });

  it('renders loading state initially', () => {
    render(
      <AuthProvider>
        <AdminPageClient />
      </AuthProvider>
    );

    expect(screen.getByText('Loading...')).toBeInTheDocument();
  });

  it('renders users table after loading', async () => {
    render(
      <AuthProvider>
        <AdminPageClient />
      </AuthProvider>
    );

    await waitFor(() => {
      expect(screen.getByText('user1@example.com')).toBeInTheDocument();
    });

    expect(screen.getByText('User One')).toBeInTheDocument();
    expect(screen.getByText('user2@example.com')).toBeInTheDocument();
    expect(screen.getByText('User Two')).toBeInTheDocument();
  });

  it('displays user roles correctly', async () => {
    render(
      <AuthProvider>
        <AdminPageClient />
      </AuthProvider>
    );

    await waitFor(() => {
      const roles = screen.getAllByText('User');
      expect(roles.length).toBeGreaterThan(0);
    });

    expect(screen.getByText('Admin')).toBeInTheDocument();
  });

  it('filters users by search term', async () => {
    const user = userEvent.setup();
    render(
      <AuthProvider>
        <AdminPageClient />
      </AuthProvider>
    );

    await waitFor(() => {
      expect(screen.getByText('user1@example.com')).toBeInTheDocument();
    });

    const searchInput = screen.getByPlaceholderText(/search/i);
    await user.type(searchInput, 'User One');

    await waitFor(() => {
      expect(api.admin.getUsers).toHaveBeenCalledWith(
        expect.objectContaining({
          search: 'User One',
        })
      );
    });
  });

  it('filters users by role', async () => {
    const user = userEvent.setup();
    render(
      <AuthProvider>
        <AdminPageClient />
      </AuthProvider>
    );

    await waitFor(() => {
      expect(screen.getByText('user1@example.com')).toBeInTheDocument();
    });

    const roleFilter = screen.getByRole('combobox', { name: /role/i });
    await user.click(roleFilter);

    const adminOption = screen.getByRole('option', { name: 'Admin' });
    await user.click(adminOption);

    await waitFor(() => {
      expect(api.admin.getUsers).toHaveBeenCalledWith(
        expect.objectContaining({
          role: 'Admin',
        })
      );
    });
  });

  it('opens create user dialog when create button is clicked', async () => {
    const user = userEvent.setup();
    render(
      <AuthProvider>
        <AdminPageClient />
      </AuthProvider>
    );

    await waitFor(() => {
      expect(screen.getByText('user1@example.com')).toBeInTheDocument();
    });

    const createButton = screen.getByRole('button', { name: /create user/i });
    await user.click(createButton);

    await waitFor(() => {
      expect(screen.getByRole('dialog')).toBeInTheDocument();
      expect(screen.getByText('Create User')).toBeInTheDocument();
    });
  });

  it('creates new user with valid form data', async () => {
    const user = userEvent.setup();
    vi.mocked(api.admin.createUser).mockResolvedValue(undefined);

    render(
      <AuthProvider>
        <AdminPageClient />
      </AuthProvider>
    );

    await waitFor(() => {
      expect(screen.getByText('user1@example.com')).toBeInTheDocument();
    });

    const createButton = screen.getByRole('button', { name: /create user/i });
    await user.click(createButton);

    await waitFor(() => {
      expect(screen.getByRole('dialog')).toBeInTheDocument();
    });

    const emailInput = screen.getByLabelText(/email/i);
    const passwordInput = screen.getByLabelText(/password/i);
    const displayNameInput = screen.getByLabelText(/display name/i);

    await user.type(emailInput, 'newuser@example.com');
    await user.type(passwordInput, 'SecurePassword123!');
    await user.type(displayNameInput, 'New User');

    const submitButton = screen.getByRole('button', { name: /submit|create/i });
    await user.click(submitButton);

    await waitFor(() => {
      expect(api.admin.createUser).toHaveBeenCalledWith({
        email: 'newuser@example.com',
        password: 'SecurePassword123!',
        displayName: 'New User',
        role: expect.any(String),
      });
    });
  });

  it('shows validation error for invalid email', async () => {
    const user = userEvent.setup();
    render(
      <AuthProvider>
        <AdminPageClient />
      </AuthProvider>
    );

    await waitFor(() => {
      expect(screen.getByText('user1@example.com')).toBeInTheDocument();
    });

    const createButton = screen.getByRole('button', { name: /create user/i });
    await user.click(createButton);

    await waitFor(() => {
      expect(screen.getByRole('dialog')).toBeInTheDocument();
    });

    const emailInput = screen.getByLabelText(/email/i);
    await user.type(emailInput, 'invalid-email');

    const submitButton = screen.getByRole('button', { name: /submit|create/i });
    await user.click(submitButton);

    await waitFor(() => {
      expect(api.admin.createUser).not.toHaveBeenCalled();
      expect(screen.getByText(/invalid email/i)).toBeInTheDocument();
    });
  });

  it('opens edit user dialog when edit button is clicked', async () => {
    const user = userEvent.setup();
    render(
      <AuthProvider>
        <AdminPageClient />
      </AuthProvider>
    );

    await waitFor(() => {
      expect(screen.getByText('user1@example.com')).toBeInTheDocument();
    });

    const editButtons = screen.getAllByRole('button', { name: /edit/i });
    await user.click(editButtons[0]);

    await waitFor(() => {
      expect(screen.getByRole('dialog')).toBeInTheDocument();
      expect(screen.getByText('Edit User')).toBeInTheDocument();
      expect(screen.getByDisplayValue('user1@example.com')).toBeInTheDocument();
    });
  });

  it('updates user with modified data', async () => {
    const user = userEvent.setup();
    vi.mocked(api.admin.updateUser).mockResolvedValue(undefined);

    render(
      <AuthProvider>
        <AdminPageClient />
      </AuthProvider>
    );

    await waitFor(() => {
      expect(screen.getByText('user1@example.com')).toBeInTheDocument();
    });

    const editButtons = screen.getAllByRole('button', { name: /edit/i });
    await user.click(editButtons[0]);

    await waitFor(() => {
      expect(screen.getByRole('dialog')).toBeInTheDocument();
    });

    const displayNameInput = screen.getByDisplayValue('User One');
    await user.clear(displayNameInput);
    await user.type(displayNameInput, 'Updated User Name');

    const submitButton = screen.getByRole('button', { name: /submit|save/i });
    await user.click(submitButton);

    await waitFor(() => {
      expect(api.admin.updateUser).toHaveBeenCalledWith(
        'user-1',
        expect.objectContaining({
          displayName: 'Updated User Name',
        })
      );
    });
  });

  it('shows confirmation dialog when deleting a user', async () => {
    const user = userEvent.setup();
    render(
      <AuthProvider>
        <AdminPageClient />
      </AuthProvider>
    );

    await waitFor(() => {
      expect(screen.getByText('user1@example.com')).toBeInTheDocument();
    });

    const deleteButtons = screen.getAllByRole('button', { name: /delete/i });
    await user.click(deleteButtons[0]);

    await waitFor(() => {
      expect(screen.getByRole('dialog')).toBeInTheDocument();
      expect(screen.getByText('Delete User')).toBeInTheDocument();
      expect(screen.getByText(/Are you sure/i)).toBeInTheDocument();
    });
  });

  it('deletes user after confirmation', async () => {
    const user = userEvent.setup();
    vi.mocked(api.admin.deleteUser).mockResolvedValue(undefined);

    render(
      <AuthProvider>
        <AdminPageClient />
      </AuthProvider>
    );

    await waitFor(() => {
      expect(screen.getByText('user1@example.com')).toBeInTheDocument();
    });

    const deleteButtons = screen.getAllByRole('button', { name: /delete/i });
    await user.click(deleteButtons[0]);

    await waitFor(() => {
      expect(screen.getByRole('dialog')).toBeInTheDocument();
    });

    const confirmButton = screen.getByRole('button', { name: /confirm/i });
    await user.click(confirmButton);

    await waitFor(() => {
      expect(api.admin.deleteUser).toHaveBeenCalledWith('user-1');
    });
  });

  it('cancels user deletion when cancel button is clicked', async () => {
    const user = userEvent.setup();
    render(
      <AuthProvider>
        <AdminPageClient />
      </AuthProvider>
    );

    await waitFor(() => {
      expect(screen.getByText('user1@example.com')).toBeInTheDocument();
    });

    const deleteButtons = screen.getAllByRole('button', { name: /delete/i });
    await user.click(deleteButtons[0]);

    await waitFor(() => {
      expect(screen.getByRole('dialog')).toBeInTheDocument();
    });

    const cancelButton = screen.getByRole('button', { name: /cancel/i });
    await user.click(cancelButton);

    await waitFor(() => {
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
      expect(api.admin.deleteUser).not.toHaveBeenCalled();
    });
  });

  it('handles bulk selection of users', async () => {
    const user = userEvent.setup();
    render(
      <AuthProvider>
        <AdminPageClient />
      </AuthProvider>
    );

    await waitFor(() => {
      expect(screen.getByText('user1@example.com')).toBeInTheDocument();
    });

    const checkboxes = screen.getAllByRole('checkbox');
    await user.click(checkboxes[1]); // First user checkbox (skip select all)

    await waitFor(() => {
      expect(screen.getByTestId('bulk-action-bar')).toBeInTheDocument();
    });
  });

  it('performs bulk delete with confirmation', async () => {
    const user = userEvent.setup();
    vi.mocked(api.admin.deleteUser).mockResolvedValue(undefined);

    render(
      <AuthProvider>
        <AdminPageClient />
      </AuthProvider>
    );

    await waitFor(() => {
      expect(screen.getByText('user1@example.com')).toBeInTheDocument();
    });

    const checkboxes = screen.getAllByRole('checkbox');
    await user.click(checkboxes[1]);

    const bulkDeleteButton = await screen.findByTestId('bulk-action-bar-action-delete');
    await user.click(bulkDeleteButton);

    await waitFor(() => {
      expect(screen.getByText('Delete Multiple Users')).toBeInTheDocument();
    });

    const confirmButton = screen.getByRole('button', { name: /confirm/i });
    await user.click(confirmButton);

    await waitFor(
      () => {
        expect(api.admin.deleteUser).toHaveBeenCalled();
      },
      { timeout: 3000 }
    );
  });

  it('displays error message when API call fails', async () => {
    vi.mocked(api.admin.getUsers).mockRejectedValue(new Error('API Error'));

    render(
      <AuthProvider>
        <AdminPageClient />
      </AuthProvider>
    );

    await waitFor(() => {
      expect(screen.getByText('API Error')).toBeInTheDocument();
    });
  });

  it('shows empty state when no users exist', async () => {
    vi.mocked(api.admin.getUsers).mockResolvedValue({
      items: [],
      total: 0,
    });

    render(
      <AuthProvider>
        <AdminPageClient />
      </AuthProvider>
    );

    await waitFor(() => {
      expect(screen.getByText(/No users found/i)).toBeInTheDocument();
    });
  });

  it('paginates through user list', async () => {
    const user = userEvent.setup();
    render(
      <AuthProvider>
        <AdminPageClient />
      </AuthProvider>
    );

    await waitFor(() => {
      expect(screen.getByText('user1@example.com')).toBeInTheDocument();
    });

    const nextPageButton = screen.getByRole('button', { name: /next/i });
    await user.click(nextPageButton);

    await waitFor(() => {
      expect(api.admin.getUsers).toHaveBeenCalledWith(
        expect.objectContaining({
          page: 2,
        })
      );
    });
  });
});
