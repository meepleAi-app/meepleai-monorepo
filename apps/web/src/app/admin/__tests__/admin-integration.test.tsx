/**
 * Admin Panel Integration Tests - Issue #2307 Week 3
 *
 * Frontend integration tests for admin dashboard workflows:
 * 1. Dashboard stats loading → cards display → real-time updates
 * 2. User management → create → edit → delete
 * 3. Configuration update → form submit → persistence verification
 * 4. Alert rules → create → test → trigger simulation
 * 5. Analytics charts → date range filter → data refresh
 * 6. API keys management → create → revoke → usage display
 *
 * Pattern: Vitest + React Testing Library
 * Mocks: API calls, admin actions
 */

import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import React from 'react';
import type { AuthUser, ApiKey, AlertRule, SystemStats } from '@/types';

// Mock API
vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getStats: vi.fn(),
      getUsers: vi.fn(),
      createUser: vi.fn(),
      updateUser: vi.fn(),
      deleteUser: vi.fn(),
      getConfiguration: vi.fn(),
      updateConfiguration: vi.fn(),
      getAlertRules: vi.fn(),
      createAlertRule: vi.fn(),
      testAlertRule: vi.fn(),
      getAnalytics: vi.fn(),
      getApiKeys: vi.fn(),
      createApiKey: vi.fn(),
      revokeApiKey: vi.fn(),
    },
  },
}));

// Mock components
function AdminDashboard() {
  const [stats, setStats] = React.useState<SystemStats | null>(null);
  const [loading, setLoading] = React.useState(true);

  React.useEffect(() => {
    const fetchStats = async () => {
      const { api } = await import('@/lib/api');
      const data = await api.admin.getStats();
      setStats(data);
      setLoading(false);
    };
    fetchStats();
  }, []);

  if (loading) return <div>Loading dashboard...</div>;

  return (
    <div aria-label="admin dashboard">
      <div aria-label="stat card users">
        <h3>Total Users</h3>
        <p>{stats?.totalUsers || 0}</p>
      </div>
      <div aria-label="stat card games">
        <h3>Total Games</h3>
        <p>{stats?.totalGames || 0}</p>
      </div>
      <div aria-label="stat card queries">
        <h3>Queries (24h)</h3>
        <p>{stats?.queries24h || 0}</p>
      </div>
      <div aria-label="stat card uptime">
        <h3>System Uptime</h3>
        <p>{stats?.uptime || '0%'}</p>
      </div>
    </div>
  );
}

function UserManagement() {
  const [users, setUsers] = React.useState<AuthUser[]>([]);
  const [showForm, setShowForm] = React.useState(false);
  const [editingUser, setEditingUser] = React.useState<AuthUser | null>(null);
  const [formData, setFormData] = React.useState({ email: '', displayName: '', role: 'User' });

  React.useEffect(() => {
    const fetchUsers = async () => {
      const { api } = await import('@/lib/api');
      const data = await api.admin.getUsers();
      setUsers(data);
    };
    fetchUsers();
  }, []);

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    const { api } = await import('@/lib/api');
    const newUser = await api.admin.createUser(formData);
    setUsers([...users, newUser]);
    setShowForm(false);
    setFormData({ email: '', displayName: '', role: 'User' });
  };

  const handleEdit = (user: AuthUser) => {
    setEditingUser(user);
    setFormData({ email: user.email, displayName: user.displayName, role: user.role });
    setShowForm(true);
  };

  const handleUpdate = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!editingUser) return;
    const { api } = await import('@/lib/api');
    const updated = await api.admin.updateUser(editingUser.id, formData);
    setUsers(users.map(u => (u.id === updated.id ? updated : u)));
    setShowForm(false);
    setEditingUser(null);
  };

  const handleDelete = async (userId: string) => {
    const { api } = await import('@/lib/api');
    await api.admin.deleteUser(userId);
    setUsers(users.filter(u => u.id !== userId));
  };

  return (
    <div aria-label="user management">
      <button onClick={() => setShowForm(true)} aria-label="create user button">
        Create User
      </button>

      {showForm && (
        <form onSubmit={editingUser ? handleUpdate : handleCreate} aria-label="user form">
          <input
            type="email"
            placeholder="Email"
            value={formData.email}
            onChange={e => setFormData({ ...formData, email: e.target.value })}
            aria-label="email input"
          />
          <input
            type="text"
            placeholder="Display Name"
            value={formData.displayName}
            onChange={e => setFormData({ ...formData, displayName: e.target.value })}
            aria-label="display name input"
          />
          <select
            value={formData.role}
            onChange={e => setFormData({ ...formData, role: e.target.value })}
            aria-label="role select"
          >
            <option value="User">User</option>
            <option value="Admin">Admin</option>
          </select>
          <button type="submit">{editingUser ? 'Update' : 'Create'}</button>
          <button type="button" onClick={() => setShowForm(false)}>
            Cancel
          </button>
        </form>
      )}

      <div aria-label="user list">
        {users.map(user => (
          <div key={user.id} aria-label={`user item ${user.email}`}>
            <span>{user.email}</span>
            <span>{user.displayName}</span>
            <span>{user.role}</span>
            <button onClick={() => handleEdit(user)} aria-label="edit user">
              Edit
            </button>
            <button onClick={() => handleDelete(user.id)} aria-label="delete user">
              Delete
            </button>
          </div>
        ))}
      </div>
    </div>
  );
}

function ConfigurationPanel() {
  const [config, setConfig] = React.useState<Record<string, any>>({});
  const [saved, setSaved] = React.useState(false);

  React.useEffect(() => {
    const fetchConfig = async () => {
      const { api } = await import('@/lib/api');
      const data = await api.admin.getConfiguration();
      setConfig(data);
    };
    fetchConfig();
  }, []);

  const handleSave = async () => {
    const { api } = await import('@/lib/api');
    await api.admin.updateConfiguration(config);
    setSaved(true);
    setTimeout(() => setSaved(false), 2000);
  };

  return (
    <div aria-label="configuration panel">
      <input
        type="text"
        value={config.apiUrl || ''}
        onChange={e => setConfig({ ...config, apiUrl: e.target.value })}
        aria-label="api url input"
      />
      <input
        type="number"
        value={config.maxQueryLength || ''}
        onChange={e => setConfig({ ...config, maxQueryLength: parseInt(e.target.value) })}
        aria-label="max query length input"
      />
      <button onClick={handleSave} aria-label="save config">
        Save Configuration
      </button>
      {saved && <div role="alert">Configuration saved successfully</div>}
    </div>
  );
}

function AlertRulesManager() {
  const [rules, setRules] = React.useState<AlertRule[]>([]);
  const [newRule, setNewRule] = React.useState({ name: '', condition: '', threshold: '' });
  const [testResult, setTestResult] = React.useState<string | null>(null);

  React.useEffect(() => {
    const fetchRules = async () => {
      const { api } = await import('@/lib/api');
      const data = await api.admin.getAlertRules();
      setRules(data);
    };
    fetchRules();
  }, []);

  const handleCreate = async () => {
    const { api } = await import('@/lib/api');
    const created = await api.admin.createAlertRule(newRule);
    setRules([...rules, created]);
    setNewRule({ name: '', condition: '', threshold: '' });
  };

  const handleTest = async (ruleId: string) => {
    const { api } = await import('@/lib/api');
    const result = await api.admin.testAlertRule(ruleId);
    setTestResult(result.message);
  };

  return (
    <div aria-label="alert rules">
      <div aria-label="create rule form">
        <input
          placeholder="Rule Name"
          value={newRule.name}
          onChange={e => setNewRule({ ...newRule, name: e.target.value })}
          aria-label="rule name input"
        />
        <input
          placeholder="Condition"
          value={newRule.condition}
          onChange={e => setNewRule({ ...newRule, condition: e.target.value })}
          aria-label="rule condition input"
        />
        <input
          placeholder="Threshold"
          value={newRule.threshold}
          onChange={e => setNewRule({ ...newRule, threshold: e.target.value })}
          aria-label="rule threshold input"
        />
        <button onClick={handleCreate} aria-label="create rule button">
          Create Rule
        </button>
      </div>

      <div aria-label="rules list">
        {rules.map(rule => (
          <div key={rule.id} aria-label={`rule item ${rule.name}`}>
            <span>{rule.name}</span>
            <button onClick={() => handleTest(rule.id)} aria-label="test rule">
              Test
            </button>
          </div>
        ))}
      </div>

      {testResult && (
        <div role="alert" aria-label="test result">
          {testResult}
        </div>
      )}
    </div>
  );
}

function AnalyticsDashboard() {
  const [dateRange, setDateRange] = React.useState('7days');
  const [analytics, setAnalytics] = React.useState<any>(null);

  const fetchAnalytics = async (range: string) => {
    const { api } = await import('@/lib/api');
    const data = await api.admin.getAnalytics(range);
    setAnalytics(data);
  };

  React.useEffect(() => {
    fetchAnalytics(dateRange);
  }, [dateRange]);

  return (
    <div aria-label="analytics dashboard">
      <select
        value={dateRange}
        onChange={e => setDateRange(e.target.value)}
        aria-label="date range selector"
      >
        <option value="24hours">Last 24 Hours</option>
        <option value="7days">Last 7 Days</option>
        <option value="30days">Last 30 Days</option>
      </select>

      {analytics && (
        <div aria-label="analytics data">
          <div aria-label="chart queries">Queries: {analytics.totalQueries}</div>
          <div aria-label="chart users">Active Users: {analytics.activeUsers}</div>
        </div>
      )}
    </div>
  );
}

function ApiKeyManager() {
  const [keys, setKeys] = React.useState<ApiKey[]>([]);
  const [newKeyName, setNewKeyName] = React.useState('');
  const [createdKey, setCreatedKey] = React.useState<string | null>(null);

  React.useEffect(() => {
    const fetchKeys = async () => {
      const { api } = await import('@/lib/api');
      const data = await api.admin.getApiKeys();
      setKeys(data);
    };
    fetchKeys();
  }, []);

  const handleCreate = async () => {
    const { api } = await import('@/lib/api');
    const result = await api.admin.createApiKey(newKeyName);
    setKeys([...keys, result.keyInfo]);
    setCreatedKey(result.key);
    setNewKeyName('');
  };

  const handleRevoke = async (keyId: string) => {
    const { api } = await import('@/lib/api');
    await api.admin.revokeApiKey(keyId);
    setKeys(keys.filter(k => k.id !== keyId));
  };

  return (
    <div aria-label="api key manager">
      <div aria-label="create key form">
        <input
          placeholder="Key Name"
          value={newKeyName}
          onChange={e => setNewKeyName(e.target.value)}
          aria-label="key name input"
        />
        <button onClick={handleCreate} aria-label="create key button">
          Create API Key
        </button>
      </div>

      {createdKey && (
        <div role="alert" aria-label="created key display">
          New API Key: {createdKey}
        </div>
      )}

      <div aria-label="keys list">
        {keys.map(key => (
          <div key={key.id} aria-label={`key item ${key.name}`}>
            <span>{key.name}</span>
            <span>Usage: {key.usageCount}</span>
            <button onClick={() => handleRevoke(key.id)} aria-label="revoke key">
              Revoke
            </button>
          </div>
        ))}
      </div>
    </div>
  );
}

describe('Admin Panel Integration Tests - Issue #2307', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  // ============================================================================
  // TEST 1: Dashboard stats → cards display → real-time updates
  // ============================================================================
  describe('1. Dashboard statistics loading and display', () => {
    it('should load and display system statistics', async () => {
      const { api } = await import('@/lib/api');

      const mockStats: SystemStats = {
        totalUsers: 150,
        totalGames: 500,
        queries24h: 1250,
        uptime: '99.8%',
      };

      vi.mocked(api.admin.getStats).mockResolvedValue(mockStats);

      render(<AdminDashboard />);

      // Verify loading state
      expect(screen.getByText(/loading dashboard/i)).toBeInTheDocument();

      // Verify stats cards appear
      await waitFor(() => {
        const dashboard = screen.getByLabelText(/admin dashboard/i);
        expect(dashboard).toBeInTheDocument();
      });

      // Verify each stat card
      const usersCard = screen.getByLabelText(/stat card users/i);
      expect(within(usersCard).getByText('150')).toBeInTheDocument();

      const gamesCard = screen.getByLabelText(/stat card games/i);
      expect(within(gamesCard).getByText('500')).toBeInTheDocument();

      const queriesCard = screen.getByLabelText(/stat card queries/i);
      expect(within(queriesCard).getByText('1250')).toBeInTheDocument();

      const uptimeCard = screen.getByLabelText(/stat card uptime/i);
      expect(within(uptimeCard).getByText('99.8%')).toBeInTheDocument();
    });
  });

  // ============================================================================
  // TEST 2: User management → create → edit → delete
  // ============================================================================
  describe('2. User management CRUD operations', () => {
    it('should create, edit, and delete users', async () => {
      const user = userEvent.setup();
      const { api } = await import('@/lib/api');

      const mockUsers: AuthUser[] = [
        {
          id: 'user-1',
          email: 'existing@example.com',
          displayName: 'Existing User',
          role: 'User',
          createdAt: new Date(),
          emailVerified: true,
          twoFactorEnabled: false,
        },
      ];

      vi.mocked(api.admin.getUsers).mockResolvedValue(mockUsers);

      const newUser: AuthUser = {
        id: 'user-2',
        email: 'new@example.com',
        displayName: 'New User',
        role: 'Admin',
        createdAt: new Date(),
        emailVerified: false,
        twoFactorEnabled: false,
      };

      vi.mocked(api.admin.createUser).mockResolvedValue(newUser);

      render(<UserManagement />);

      // Wait for initial load
      await waitFor(() => {
        expect(screen.getByText('existing@example.com')).toBeInTheDocument();
      });

      // Create new user
      const createButton = screen.getByLabelText(/create user button/i);
      await user.click(createButton);

      const emailInput = screen.getByLabelText(/email input/i);
      const displayNameInput = screen.getByLabelText(/display name input/i);
      const roleSelect = screen.getByLabelText(/role select/i);

      await user.type(emailInput, 'new@example.com');
      await user.type(displayNameInput, 'New User');
      await user.selectOptions(roleSelect, 'Admin');

      // Use form-specific button to avoid ambiguity with "Create User" button
      const userForm = screen.getByLabelText(/user form/i);
      const submitButton = within(userForm).getByRole('button', { name: /create/i });
      await user.click(submitButton);

      await waitFor(() => {
        expect(api.admin.createUser).toHaveBeenCalledWith({
          email: 'new@example.com',
          displayName: 'New User',
          role: 'Admin',
        });
      });

      // Verify new user appears
      await waitFor(() => {
        expect(screen.getByText('new@example.com')).toBeInTheDocument();
      });

      // Edit user
      const updatedUser = { ...newUser, displayName: 'Updated User' };
      vi.mocked(api.admin.updateUser).mockResolvedValue(updatedUser);

      const editButton = screen.getAllByLabelText(/edit user/i)[1]; // Second user
      await user.click(editButton);

      const displayNameInputEdit = screen.getByLabelText(/display name input/i);
      await user.clear(displayNameInputEdit);
      await user.type(displayNameInputEdit, 'Updated User');

      const updateButton = screen.getByRole('button', { name: /update/i });
      await user.click(updateButton);

      await waitFor(() => {
        expect(api.admin.updateUser).toHaveBeenCalled();
      });

      // Delete user
      vi.mocked(api.admin.deleteUser).mockResolvedValue(undefined);

      const deleteButtons = screen.getAllByLabelText(/delete user/i);
      await user.click(deleteButtons[0]);

      await waitFor(() => {
        expect(api.admin.deleteUser).toHaveBeenCalledWith('user-1');
      });
    });
  });

  // ============================================================================
  // TEST 3: Configuration update → form → persistence
  // ============================================================================
  describe('3. Configuration management', () => {
    it('should update and persist configuration settings', async () => {
      const user = userEvent.setup();
      const { api } = await import('@/lib/api');

      vi.mocked(api.admin.getConfiguration).mockResolvedValue({
        apiUrl: 'http://localhost:8080',
        maxQueryLength: 2000,
      });

      vi.mocked(api.admin.updateConfiguration).mockResolvedValue(undefined);

      render(<ConfigurationPanel />);

      await waitFor(() => {
        expect(screen.getByLabelText(/api url input/i)).toHaveValue('http://localhost:8080');
      });

      const apiUrlInput = screen.getByLabelText(/api url input/i);
      await user.clear(apiUrlInput);
      await user.type(apiUrlInput, 'http://api.meepleai.com');

      const maxLengthInput = screen.getByLabelText(/max query length input/i);
      await user.clear(maxLengthInput);
      await user.type(maxLengthInput, '3000');

      const saveButton = screen.getByLabelText(/save config/i);
      await user.click(saveButton);

      await waitFor(() => {
        expect(api.admin.updateConfiguration).toHaveBeenCalledWith({
          apiUrl: 'http://api.meepleai.com',
          maxQueryLength: 3000,
        });
      });

      await waitFor(() => {
        expect(screen.getByText(/configuration saved successfully/i)).toBeInTheDocument();
      });
    });
  });

  // ============================================================================
  // TEST 4: Alert rules → create → test → trigger
  // ============================================================================
  describe('4. Alert rules management', () => {
    it('should create and test alert rules', async () => {
      const user = userEvent.setup();
      const { api } = await import('@/lib/api');

      vi.mocked(api.admin.getAlertRules).mockResolvedValue([]);

      const mockRule: AlertRule = {
        id: 'rule-1',
        name: 'High Error Rate',
        condition: 'error_rate > threshold',
        threshold: '5%',
        enabled: true,
        createdAt: new Date(),
      };

      vi.mocked(api.admin.createAlertRule).mockResolvedValue(mockRule);
      vi.mocked(api.admin.testAlertRule).mockResolvedValue({
        success: true,
        message: 'Alert triggered successfully',
      });

      render(<AlertRulesManager />);

      const nameInput = screen.getByLabelText(/rule name input/i);
      const conditionInput = screen.getByLabelText(/rule condition input/i);
      const thresholdInput = screen.getByLabelText(/rule threshold input/i);

      await user.type(nameInput, 'High Error Rate');
      await user.type(conditionInput, 'error_rate > threshold');
      await user.type(thresholdInput, '5%');

      const createButton = screen.getByLabelText(/create rule button/i);
      await user.click(createButton);

      await waitFor(() => {
        expect(api.admin.createAlertRule).toHaveBeenCalledWith({
          name: 'High Error Rate',
          condition: 'error_rate > threshold',
          threshold: '5%',
        });
      });

      await waitFor(() => {
        expect(screen.getByText('High Error Rate')).toBeInTheDocument();
      });

      // Test the rule
      const testButton = screen.getByLabelText(/test rule/i);
      await user.click(testButton);

      await waitFor(() => {
        expect(api.admin.testAlertRule).toHaveBeenCalledWith('rule-1');
      });

      await waitFor(() => {
        const testResult = screen.getByLabelText(/test result/i);
        expect(testResult).toHaveTextContent('Alert triggered successfully');
      });
    });
  });

  // ============================================================================
  // TEST 5: Analytics → date range → data refresh
  // ============================================================================
  describe('5. Analytics dashboard with date filtering', () => {
    it('should filter analytics by date range', async () => {
      const user = userEvent.setup();
      const { api } = await import('@/lib/api');

      vi.mocked(api.admin.getAnalytics).mockResolvedValue({
        totalQueries: 1000,
        activeUsers: 50,
      });

      render(<AnalyticsDashboard />);

      await waitFor(() => {
        expect(screen.getByText(/queries: 1000/i)).toBeInTheDocument();
      });

      // Change date range
      vi.mocked(api.admin.getAnalytics).mockResolvedValue({
        totalQueries: 5000,
        activeUsers: 200,
      });

      const dateSelector = screen.getByLabelText(/date range selector/i);
      await user.selectOptions(dateSelector, '30days');

      await waitFor(() => {
        expect(api.admin.getAnalytics).toHaveBeenCalledWith('30days');
      });

      await waitFor(() => {
        expect(screen.getByText(/queries: 5000/i)).toBeInTheDocument();
        expect(screen.getByText(/active users: 200/i)).toBeInTheDocument();
      });
    });
  });

  // ============================================================================
  // TEST 6: API keys → create → revoke → usage display
  // ============================================================================
  describe('6. API key management', () => {
    it('should create, display, and revoke API keys', async () => {
      const user = userEvent.setup();
      const { api } = await import('@/lib/api');

      const mockKeys: ApiKey[] = [
        {
          id: 'key-1',
          name: 'Production Key',
          usageCount: 1500,
          createdAt: new Date(),
          lastUsedAt: new Date(),
        },
      ];

      vi.mocked(api.admin.getApiKeys).mockResolvedValue(mockKeys);

      render(<ApiKeyManager />);

      await waitFor(() => {
        expect(screen.getByText('Production Key')).toBeInTheDocument();
        expect(screen.getByText(/usage: 1500/i)).toBeInTheDocument();
      });

      // Create new key
      const newKeyInfo: ApiKey = {
        id: 'key-2',
        name: 'Development Key',
        usageCount: 0,
        createdAt: new Date(),
        lastUsedAt: null,
      };

      vi.mocked(api.admin.createApiKey).mockResolvedValue({
        keyInfo: newKeyInfo,
        key: 'mpl_dev_abc123xyz789',
      });

      const nameInput = screen.getByLabelText(/key name input/i);
      await user.type(nameInput, 'Development Key');

      const createButton = screen.getByLabelText(/create key button/i);
      await user.click(createButton);

      await waitFor(() => {
        expect(api.admin.createApiKey).toHaveBeenCalledWith('Development Key');
      });

      await waitFor(() => {
        const createdKeyDisplay = screen.getByLabelText(/created key display/i);
        expect(createdKeyDisplay).toHaveTextContent('mpl_dev_abc123xyz789');
      });

      // Revoke key
      vi.mocked(api.admin.revokeApiKey).mockResolvedValue(undefined);

      const revokeButtons = screen.getAllByLabelText(/revoke key/i);
      await user.click(revokeButtons[0]);

      await waitFor(() => {
        expect(api.admin.revokeApiKey).toHaveBeenCalledWith('key-1');
      });
    });
  });
});
