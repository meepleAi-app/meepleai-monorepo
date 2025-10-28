import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import UserManagement from '../../pages/admin/users';

type FetchMock = jest.MockedFunction<typeof fetch>;

const createJsonResponse = (data: unknown, ok = true) =>
  ({
    ok,
    status: ok ? 200 : 400,
    json: async () => data
  } as unknown as Response);

describe('UserManagement', () => {
  const originalFetch = global.fetch;
  let fetchMock: FetchMock;
  const apiBase = 'https://api.example.com';

  const sampleUsers = [
    {
      id: 'user-1',
      email: 'admin@example.com',
      displayName: 'Admin User',
      role: 'Admin',
      createdAt: '2024-01-01T10:00:00Z',
      lastSeenAt: '2024-01-15T14:30:00Z'
    },
    {
      id: 'user-2',
      email: 'editor@example.com',
      displayName: 'Editor User',
      role: 'Editor',
      createdAt: '2024-01-02T10:00:00Z',
      lastSeenAt: null
    },
    {
      id: 'user-3',
      email: 'user@example.com',
      displayName: 'Regular User',
      role: 'User',
      createdAt: '2024-01-03T10:00:00Z',
      lastSeenAt: '2024-01-10T08:00:00Z'
    }
  ];

  const pagedResponse = {
    items: sampleUsers,
    total: 3,
    page: 1,
    pageSize: 20
  };

  beforeAll(() => {
    fetchMock = jest.fn() as FetchMock;
    global.fetch = fetchMock;

    Object.defineProperty(global, 'NEXT_PUBLIC_API_BASE', {
      configurable: true,
      writable: true,
      value: apiBase
    });
  });

  beforeEach(() => {
    fetchMock.mockClear();
    fetchMock.mockResolvedValue(createJsonResponse(pagedResponse));
  });

  afterAll(() => {
    global.fetch = originalFetch;
  });

  describe('User List Display', () => {
    test('renders user management page with table', async () => {
      render(<UserManagement />);

      expect(screen.getByText('User Management')).toBeInTheDocument();

      await waitFor(() => {
        expect(screen.getByText('admin@example.com')).toBeInTheDocument();
        expect(screen.getByText('editor@example.com')).toBeInTheDocument();
        expect(screen.getByText('user@example.com')).toBeInTheDocument();
      });
    });

    test('displays user roles with color-coded badges', async () => {
      render(<UserManagement />);

      await waitFor(() => {
        const adminBadge = screen.getByText('Admin');
        const editorBadge = screen.getByText('Editor');
        const userBadge = screen.getByText('User');

        expect(adminBadge).toBeInTheDocument();
        expect(editorBadge).toBeInTheDocument();
        expect(userBadge).toBeInTheDocument();
      });
    });

    test('displays last seen times correctly', async () => {
      render(<UserManagement />);

      await waitFor(() => {
        // Should show formatted dates for users who have been seen
        expect(screen.getAllByText(/\/\d{4}/).length).toBeGreaterThan(0);
        // Should show "Never" for users who haven't logged in
        expect(screen.getByText('Never')).toBeInTheDocument();
      });
    });

    test('shows empty state when no users found', async () => {
      fetchMock.mockResolvedValueOnce(
        createJsonResponse({
          items: [],
          total: 0,
          page: 1,
          pageSize: 20
        })
      );

      render(<UserManagement />);

      await waitFor(() => {
        expect(screen.getByText('No users found')).toBeInTheDocument();
      });
    });
  });

  describe('Search and Filters', () => {
    test('sends search query when typing in search box', async () => {
      const user = userEvent.setup();
      render(<UserManagement />);

      await waitFor(() => {
        expect(screen.getByPlaceholderText('Search by email or name...')).toBeInTheDocument();
      });

      const searchInput = screen.getByPlaceholderText('Search by email or name...');
      await user.type(searchInput, 'admin');

      await waitFor(() => {
        const calls = fetchMock.mock.calls;
        const lastCall = calls[calls.length - 1];
        const url = lastCall[0] as string;
        expect(url).toContain('search=admin');
      });
    });

    test('filters users by role', async () => {
      const user = userEvent.setup();
      render(<UserManagement />);

      await waitFor(() => {
        expect(screen.getByRole('combobox')).toBeInTheDocument();
      });

      const roleSelect = screen.getByRole('combobox');
      await user.selectOptions(roleSelect, 'Admin');

      await waitFor(() => {
        const calls = fetchMock.mock.calls;
        const lastCall = calls[calls.length - 1];
        const url = lastCall[0] as string;
        expect(url).toContain('role=Admin');
      });
    });

    test('resets to page 1 when search changes', async () => {
      const user = userEvent.setup();
      render(<UserManagement />);

      await waitFor(() => {
        expect(screen.getByText('Next')).toBeInTheDocument();
      });

      // Go to page 2
      const nextButton = screen.getByText('Next');
      await user.click(nextButton);

      // Search should reset to page 1
      const searchInput = screen.getByPlaceholderText('Search by email or name...');
      await user.type(searchInput, 'test');

      await waitFor(() => {
        const calls = fetchMock.mock.calls;
        const lastCall = calls[calls.length - 1];
        const url = lastCall[0] as string;
        expect(url).toContain('page=1');
      });
    });
  });

  describe('Sorting', () => {
    test('toggles sort order when clicking column header', async () => {
      const user = userEvent.setup();
      render(<UserManagement />);

      await waitFor(() => {
        expect(screen.getByText(/Email/)).toBeInTheDocument();
      });

      const emailHeader = screen.getByText(/Email/);
      await user.click(emailHeader);

      await waitFor(() => {
        const calls = fetchMock.mock.calls;
        const lastCall = calls[calls.length - 1];
        const url = lastCall[0] as string;
        expect(url).toContain('sortBy=email');
        expect(url).toContain('sortOrder=asc');
      });

      // Click again to reverse sort
      await user.click(emailHeader);

      await waitFor(() => {
        const calls = fetchMock.mock.calls;
        const lastCall = calls[calls.length - 1];
        const url = lastCall[0] as string;
        expect(url).toContain('sortOrder=desc');
      });
    });

    test('displays sort indicators on active column', async () => {
      render(<UserManagement />);

      await waitFor(() => {
        // Default sort is by createdAt desc
        const createdHeader = screen.getByText(/Created/);
        expect(createdHeader.textContent).toContain('↓');
      });
    });
  });

  describe('Pagination', () => {
    test('displays pagination controls', async () => {
      render(<UserManagement />);

      await waitFor(() => {
        expect(screen.getByText('Previous')).toBeInTheDocument();
        expect(screen.getByText('Next')).toBeInTheDocument();
        expect(screen.getByText(/Page 1 of/)).toBeInTheDocument();
      });
    });

    test('shows correct item range', async () => {
      render(<UserManagement />);

      await waitFor(() => {
        expect(screen.getByText('Showing 1 to 3 of 3 users')).toBeInTheDocument();
      });
    });

    test('disables Previous button on first page', async () => {
      render(<UserManagement />);

      await waitFor(() => {
        const previousButton = screen.getByText('Previous');
        expect(previousButton).toBeDisabled();
      });
    });

    test('navigates to next page when clicking Next', async () => {
      const user = userEvent.setup();
      fetchMock.mockResolvedValueOnce(
        createJsonResponse({
          items: sampleUsers,
          total: 50,
          page: 1,
          pageSize: 20
        })
      );

      render(<UserManagement />);

      await waitFor(() => {
        expect(screen.getByText('Next')).toBeInTheDocument();
      });

      const nextButton = screen.getByText('Next');
      await user.click(nextButton);

      await waitFor(() => {
        const calls = fetchMock.mock.calls;
        const lastCall = calls[calls.length - 1];
        const url = lastCall[0] as string;
        expect(url).toContain('page=2');
      });
    });
  });

  describe('User Selection', () => {
    test('allows selecting individual users', async () => {
      const user = userEvent.setup();
      render(<UserManagement />);

      await waitFor(() => {
        expect(screen.getAllByRole('checkbox').length).toBeGreaterThan(0);
      });

      const checkboxes = screen.getAllByRole('checkbox');
      const firstUserCheckbox = checkboxes[1]; // Skip "select all" checkbox

      await user.click(firstUserCheckbox);

      expect(firstUserCheckbox).toBeChecked();
    });

    test('selects all users when clicking select-all checkbox', async () => {
      const user = userEvent.setup();
      render(<UserManagement />);

      await waitFor(() => {
        expect(screen.getAllByRole('checkbox').length).toBeGreaterThan(0);
      });

      const selectAllCheckbox = screen.getAllByRole('checkbox')[0];
      await user.click(selectAllCheckbox);

      const checkboxes = screen.getAllByRole('checkbox');
      checkboxes.forEach((cb) => {
        expect(cb).toBeChecked();
      });
    });

    test('shows bulk delete button when users selected', async () => {
      const user = userEvent.setup();
      render(<UserManagement />);

      await waitFor(() => {
        expect(screen.getAllByRole('checkbox').length).toBeGreaterThan(0);
      });

      const firstUserCheckbox = screen.getAllByRole('checkbox')[1];
      await user.click(firstUserCheckbox);

      await waitFor(() => {
        expect(screen.getByText(/Delete Selected/)).toBeInTheDocument();
      });
    });
  });

  describe('Create User Modal', () => {
    test('opens create modal when clicking Create User button', async () => {
      const user = userEvent.setup();
      render(<UserManagement />);

      await waitFor(() => {
        expect(screen.getByText('Create User')).toBeInTheDocument();
      });

      const createButton = screen.getByText('Create User');
      await user.click(createButton);

      await waitFor(() => {
        expect(screen.getByRole('heading', { name: 'Create User' })).toBeInTheDocument();
        expect(screen.getByLabelText('Email *')).toBeInTheDocument();
        expect(screen.getByLabelText('Password *')).toBeInTheDocument();
        expect(screen.getByLabelText('Display Name *')).toBeInTheDocument();
        expect(screen.getByLabelText('Role *')).toBeInTheDocument();
      });
    });

    test('validates email format in create modal', async () => {
      const user = userEvent.setup();
      render(<UserManagement />);

      await waitFor(() => {
        const createButton = screen.getByText('Create User');
        user.click(createButton);
      });

      await waitFor(() => {
        expect(screen.getByLabelText('Email *')).toBeInTheDocument();
      });

      const emailInput = screen.getByLabelText('Email *');
      const submitButton = screen.getByRole('button', { name: /Create User/i });

      await user.type(emailInput, 'invalid-email');
      await user.click(submitButton);

      await waitFor(() => {
        expect(screen.getByText('Valid email is required')).toBeInTheDocument();
      });
    });

    test('validates password length in create modal', async () => {
      const user = userEvent.setup();
      render(<UserManagement />);

      await waitFor(() => {
        const createButton = screen.getByText('Create User');
        user.click(createButton);
      });

      await waitFor(() => {
        expect(screen.getByLabelText('Password *')).toBeInTheDocument();
      });

      const passwordInput = screen.getByLabelText('Password *');
      const submitButton = screen.getByRole('button', { name: /Create User/i });

      await user.type(passwordInput, 'short');
      await user.click(submitButton);

      await waitFor(() => {
        expect(screen.getByText(/at least 8 characters/)).toBeInTheDocument();
      });
    });

    test('creates user with valid data', async () => {
      const user = userEvent.setup();
      const newUser = { ...pagedResponse.items[0], id: 'new-user' };
      fetchMock
        .mockResolvedValueOnce(createJsonResponse(pagedResponse)) // Initial load
        .mockResolvedValueOnce(createJsonResponse(newUser)) // Create response
        .mockResolvedValueOnce(createJsonResponse(pagedResponse)); // Reload after create

      render(<UserManagement />);

      await waitFor(() => {
        const createButton = screen.getByText('Create User');
        user.click(createButton);
      });

      await waitFor(() => {
        expect(screen.getByLabelText('Email *')).toBeInTheDocument();
      });

      await user.type(screen.getByLabelText('Email *'), 'newuser@example.com');
      await user.type(screen.getByLabelText('Password *'), 'SecurePass123!');
      await user.type(screen.getByLabelText('Display Name *'), 'New User');

      const submitButton = screen.getByRole('button', { name: /Create User/i });
      await user.click(submitButton);

      await waitFor(() => {
        const postCall = fetchMock.mock.calls.find(
          (call) => call[0].toString().includes('/api/v1/admin/users') && call[1]?.method === 'POST'
        );
        expect(postCall).toBeDefined();
      });
    });
  });

  describe('Edit User Modal', () => {
    test('opens edit modal with pre-filled data', async () => {
      const user = userEvent.setup();
      render(<UserManagement />);

      await waitFor(() => {
        expect(screen.getAllByText('Edit').length).toBeGreaterThan(0);
      });

      const editButtons = screen.getAllByText('Edit');
      await user.click(editButtons[0]);

      await waitFor(() => {
        expect(screen.getByRole('heading', { name: 'Edit User' })).toBeInTheDocument();
        const emailInput = screen.getByLabelText('Email *') as HTMLInputElement;
        expect(emailInput.value).toBe(sampleUsers[0].email);
      });
    });

    test('does not show password field in edit mode', async () => {
      const user = userEvent.setup();
      render(<UserManagement />);

      await waitFor(() => {
        expect(screen.getAllByText('Edit').length).toBeGreaterThan(0);
      });

      const editButtons = screen.getAllByText('Edit');
      await user.click(editButtons[0]);

      await waitFor(() => {
        expect(screen.queryByLabelText('Password *')).not.toBeInTheDocument();
      });
    });

    test('updates user with modified data', async () => {
      const user = userEvent.setup();
      fetchMock
        .mockResolvedValueOnce(createJsonResponse(pagedResponse)) // Initial load
        .mockResolvedValueOnce(createJsonResponse({ ...sampleUsers[0], displayName: 'Updated Name' })) // Update response
        .mockResolvedValueOnce(createJsonResponse(pagedResponse)); // Reload

      render(<UserManagement />);

      await waitFor(() => {
        expect(screen.getAllByText('Edit').length).toBeGreaterThan(0);
      });

      const editButtons = screen.getAllByText('Edit');
      await user.click(editButtons[0]);

      await waitFor(() => {
        expect(screen.getByLabelText('Display Name *')).toBeInTheDocument();
      });

      const displayNameInput = screen.getByLabelText('Display Name *');
      await user.clear(displayNameInput);
      await user.type(displayNameInput, 'Updated Name');

      const saveButton = screen.getByRole('button', { name: /Save Changes/i });
      await user.click(saveButton);

      await waitFor(() => {
        const putCall = fetchMock.mock.calls.find(
          (call) => call[0].toString().includes('/api/v1/admin/users/user-1') && call[1]?.method === 'PUT'
        );
        expect(putCall).toBeDefined();
      });
    });
  });

  describe('Delete User', () => {
    test('shows confirmation dialog when deleting user', async () => {
      const user = userEvent.setup();
      render(<UserManagement />);

      await waitFor(() => {
        expect(screen.getAllByText('Delete').length).toBeGreaterThan(0);
      });

      const deleteButtons = screen.getAllByText('Delete');
      await user.click(deleteButtons[0]);

      await waitFor(() => {
        expect(screen.getByText('Delete User')).toBeInTheDocument();
        expect(screen.getByText(/Are you sure you want to delete admin@example.com/)).toBeInTheDocument();
      });
    });

    test('deletes user when confirming', async () => {
      const user = userEvent.setup();
      fetchMock
        .mockResolvedValueOnce(createJsonResponse(pagedResponse)) // Initial load
        .mockResolvedValueOnce(createJsonResponse({})) // Delete response
        .mockResolvedValueOnce(createJsonResponse({ ...pagedResponse, items: pagedResponse.items.slice(1) })); // Reload

      render(<UserManagement />);

      await waitFor(() => {
        expect(screen.getAllByText('Delete').length).toBeGreaterThan(0);
      });

      const deleteButtons = screen.getAllByText('Delete');
      await user.click(deleteButtons[0]);

      await waitFor(() => {
        expect(screen.getByText('Confirm')).toBeInTheDocument();
      });

      const confirmButton = screen.getByText('Confirm');
      await user.click(confirmButton);

      await waitFor(() => {
        const deleteCall = fetchMock.mock.calls.find(
          (call) => call[0].toString().includes('/api/v1/admin/users/user-1') && call[1]?.method === 'DELETE'
        );
        expect(deleteCall).toBeDefined();
      });
    });

    test('cancels deletion when clicking Cancel', async () => {
      const user = userEvent.setup();
      render(<UserManagement />);

      await waitFor(() => {
        expect(screen.getAllByText('Delete').length).toBeGreaterThan(0);
      });

      const deleteButtons = screen.getAllByText('Delete');
      await user.click(deleteButtons[0]);

      await waitFor(() => {
        expect(screen.getByText('Cancel')).toBeInTheDocument();
      });

      const initialCallCount = fetchMock.mock.calls.length;
      const cancelButton = screen.getByText('Cancel');
      await user.click(cancelButton);

      // Should not make additional API calls
      await waitFor(() => {
        expect(fetchMock.mock.calls.length).toBe(initialCallCount);
      });
    });
  });

  describe('Bulk Operations', () => {
    test('shows bulk delete button when users are selected', async () => {
      const user = userEvent.setup();
      render(<UserManagement />);

      await waitFor(() => {
        expect(screen.getAllByRole('checkbox').length).toBeGreaterThan(0);
      });

      expect(screen.queryByText(/Delete Selected/)).not.toBeInTheDocument();

      const checkboxes = screen.getAllByRole('checkbox');
      await user.click(checkboxes[1]);

      await waitFor(() => {
        expect(screen.getByText(/Delete Selected \(1\)/)).toBeInTheDocument();
      });
    });

    test('bulk deletes selected users', async () => {
      const user = userEvent.setup();
      fetchMock
        .mockResolvedValueOnce(createJsonResponse(pagedResponse)) // Initial load
        .mockResolvedValueOnce(createJsonResponse({})) // Delete user 1
        .mockResolvedValueOnce(createJsonResponse({})) // Delete user 2
        .mockResolvedValueOnce(createJsonResponse({ ...pagedResponse, items: [pagedResponse.items[2]] })); // Reload

      render(<UserManagement />);

      await waitFor(() => {
        expect(screen.getAllByRole('checkbox').length).toBeGreaterThan(0);
      });

      const checkboxes = screen.getAllByRole('checkbox');
      await user.click(checkboxes[1]);
      await user.click(checkboxes[2]);

      const bulkDeleteButton = screen.getByText(/Delete Selected \(2\)/);
      await user.click(bulkDeleteButton);

      await waitFor(() => {
        expect(screen.getByText('Delete Multiple Users')).toBeInTheDocument();
      });

      const confirmButton = screen.getByText('Confirm');
      await user.click(confirmButton);

      await waitFor(() => {
        const deleteCalls = fetchMock.mock.calls.filter(
          (call) => call[1]?.method === 'DELETE'
        );
        expect(deleteCalls.length).toBe(2);
      });
    });
  });

  describe('Error Handling', () => {
    test('displays error message when API fails', async () => {
      fetchMock.mockRejectedValueOnce(new Error('Network error'));

      render(<UserManagement />);

      await waitFor(() => {
        expect(screen.getByText(/An error occurred/)).toBeInTheDocument();
      });
    });

    test('shows toast notification on create error', async () => {
      const user = userEvent.setup();
      fetchMock
        .mockResolvedValueOnce(createJsonResponse(pagedResponse)) // Initial load
        .mockResolvedValueOnce(createJsonResponse({ error: 'Email already exists' }, false)); // Create error

      render(<UserManagement />);

      await waitFor(() => {
        const createButton = screen.getByText('Create User');
        user.click(createButton);
      });

      await waitFor(() => {
        expect(screen.getByLabelText('Email *')).toBeInTheDocument();
      });

      await user.type(screen.getByLabelText('Email *'), 'test@example.com');
      await user.type(screen.getByLabelText('Password *'), 'SecurePass123!');
      await user.type(screen.getByLabelText('Display Name *'), 'Test User');

      const submitButton = screen.getByRole('button', { name: /Create User/i });
      await user.click(submitButton);

      await waitFor(() => {
        expect(screen.getByText(/Failed to create user/)).toBeInTheDocument();
      });
    });

    test('shows unauthorized error when user lacks permissions', async () => {
      fetchMock.mockResolvedValueOnce(createJsonResponse({ error: 'Unauthorized' }, false));

      render(<UserManagement />);

      await waitFor(() => {
        expect(screen.getByText(/Unauthorized - Admin access required/)).toBeInTheDocument();
      });
    });
  });

  describe('Toast Notifications', () => {
    test('displays success toast after creating user', async () => {
      const user = userEvent.setup();
      fetchMock
        .mockResolvedValueOnce(createJsonResponse(pagedResponse))
        .mockResolvedValueOnce(createJsonResponse({ id: 'new', email: 'new@example.com' }))
        .mockResolvedValueOnce(createJsonResponse(pagedResponse));

      render(<UserManagement />);

      await waitFor(() => {
        const createButton = screen.getByText('Create User');
        user.click(createButton);
      });

      await waitFor(() => {
        expect(screen.getByLabelText('Email *')).toBeInTheDocument();
      });

      await user.type(screen.getByLabelText('Email *'), 'new@example.com');
      await user.type(screen.getByLabelText('Password *'), 'SecurePass123!');
      await user.type(screen.getByLabelText('Display Name *'), 'New User');

      const submitButton = screen.getByRole('button', { name: /Create User/i });
      await user.click(submitButton);

      await waitFor(() => {
        expect(screen.getByText(/created successfully/)).toBeInTheDocument();
      });
    });

    test('allows dismissing toast notifications', async () => {
      const user = userEvent.setup();
      fetchMock
        .mockResolvedValueOnce(createJsonResponse(pagedResponse))
        .mockResolvedValueOnce(createJsonResponse({}))
        .mockResolvedValueOnce(createJsonResponse(pagedResponse));

      render(<UserManagement />);

      await waitFor(() => {
        expect(screen.getAllByText('Delete').length).toBeGreaterThan(0);
      });

      const deleteButtons = screen.getAllByText('Delete');
      await user.click(deleteButtons[0]);

      await waitFor(() => {
        expect(screen.getByText('Confirm')).toBeInTheDocument();
      });

      await user.click(screen.getByText('Confirm'));

      await waitFor(() => {
        expect(screen.getByText(/deleted successfully/)).toBeInTheDocument();
      });

      const closeButton = screen.getByLabelText('Close notification');
      await user.click(closeButton);

      await waitFor(() => {
        expect(screen.queryByText(/deleted successfully/)).not.toBeInTheDocument();
      });
    });
  });
});
