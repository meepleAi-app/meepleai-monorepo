'use client';

/**
 * Management Page Client - Issue #903 Integration
 *
 * Three-tab interface combining all FASE 3 features:
 * 1. API Keys Management (with filters, modal, stats)
 * 2. User Management (bulk operations, CSV import/export)
 * 3. Activity Timeline (system-wide activity monitoring)
 *
 * Components Used:
 * - ApiKeyFilterPanel (#910)
 * - ApiKeyCreationModal (#909)
 * - BulkActionBar (#912)
 * - UserActivityTimeline (#911)
 *
 * Backend Integration:
 * - GetAllApiKeysWithStatsQuery
 * - BulkExportApiKeysQuery
 * - BulkImportApiKeysCommand
 * - GetUserActivityQuery
 */

import { useState, useCallback, useEffect } from 'react';

import {
  Key,
  Users,
  Activity,
  AlertCircle,
  Download,
  Upload,
  Trash2,
  Plus,
  RefreshCw,
} from 'lucide-react';
import Link from 'next/link';

import { AdminAuthGuard } from '@/components/admin';
// Import FASE 3 components
import { ApiKeyFilterPanel } from '@/components/admin/ApiKeyFilterPanel';
import { BulkActionBar } from '@/components/admin/BulkActionBar';
// eslint-disable-next-line import/order -- Type import grouping (edge case)
import type { ApiKeyFilters } from '@/types';
import type { UserActivityEvent } from '@/components/admin/UserActivityItem';
import { UserActivityTimeline } from '@/components/admin/UserActivityTimeline';
import { useAuthUser } from '@/components/auth/AuthProvider';
import { ApiKeyCreationModal } from '@/components/modals/ApiKeyCreationModal';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/navigation/tabs';
import { ConfirmationDialog } from '@/components/ui/overlays/confirmation-dialog';
import { Button } from '@/components/ui/primitives/button';
import { api } from '@/lib/api';
import type { ApiKeyWithStatsDto, AdminUser } from '@/lib/api/schemas/admin.schemas';

type TabValue = 'api-keys' | 'users' | 'activity';

type ToastMessage = {
  id: string;
  type: 'success' | 'error' | 'info';
  message: string;
};

type ConfirmDialogState = {
  isOpen: boolean;
  title: string;
  message: string;
  variant: 'default' | 'destructive' | 'warning';
  onConfirm: () => void;
};

export function ManagementPageClient() {
  const { user, loading: authLoading } = useAuthUser();

  // Tab state
  const [activeTab, setActiveTab] = useState<TabValue>('api-keys');

  // Toast management
  const [toasts, setToasts] = useState<ToastMessage[]>([]);

  // Confirmation dialog state
  const [confirmDialog, setConfirmDialog] = useState<ConfirmDialogState>({
    isOpen: false,
    title: '',
    message: '',
    variant: 'default',
    onConfirm: () => {},
  });

  const addToast = useCallback((type: 'success' | 'error' | 'info', message: string) => {
    const id = `toast-${Date.now()}-${Math.random()}`;
    setToasts(prev => [...prev, { id, type, message }]);
    setTimeout(() => {
      setToasts(prev => prev.filter(t => t.id !== id));
    }, 5000);
  }, []);

  // =========================
  // TAB 1: API KEYS
  // =========================
  const [apiKeys, setApiKeys] = useState<ApiKeyWithStatsDto[]>([]);
  const [apiKeysLoading, setApiKeysLoading] = useState(false);
  const [apiKeyFilters, setApiKeyFilters] = useState<ApiKeyFilters>({});
  const [selectedKeys, setSelectedKeys] = useState<Set<string>>(new Set());
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);

  const fetchApiKeys = useCallback(async () => {
    try {
      setApiKeysLoading(true);
      const result = await api.admin.getApiKeysWithStats({
        includeRevoked: true,
      });
      setApiKeys(result.keys);
    } catch (err) {
      console.error('Failed to load API keys:', err);
      addToast('error', 'Failed to load API keys');
    } finally {
      setApiKeysLoading(false);
    }
  }, [addToast]);

  useEffect(() => {
    if (activeTab === 'api-keys') {
      fetchApiKeys();
    }
  }, [activeTab, fetchApiKeys]);

  const handleBulkDeleteKeys = useCallback(() => {
    if (selectedKeys.size === 0) return;

    setConfirmDialog({
      isOpen: true,
      title: 'Delete API Keys',
      message: `Are you sure you want to delete ${selectedKeys.size} API key(s)? This action cannot be undone.`,
      variant: 'destructive',
      onConfirm: async () => {
        try {
          await Promise.all(Array.from(selectedKeys).map(id => api.admin.deleteApiKey(id)));
          addToast('success', `${selectedKeys.size} API key(s) deleted successfully`);
          setSelectedKeys(new Set());
          fetchApiKeys();
        } catch (_err) {
          addToast('error', 'Failed to delete some API keys');
        }
      },
    });
  }, [selectedKeys, addToast, fetchApiKeys]);

  const handleExportKeys = useCallback(async () => {
    try {
      const blob = await api.admin.exportApiKeysToCSV({
        searchTerm: apiKeyFilters.search,
      });

      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `apikeys-export-${new Date().toISOString().split('T')[0]}.csv`;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      window.URL.revokeObjectURL(url);

      addToast('success', 'API keys exported successfully');
    } catch (_err) {
      addToast('error', 'Failed to export API keys');
    }
  }, [apiKeyFilters, addToast]);

  // =========================
  // TAB 2: USER MANAGEMENT
  // =========================
  const [users, setUsers] = useState<AdminUser[]>([]);
  const [usersLoading, setUsersLoading] = useState(false);
  const [selectedUsers, setSelectedUsers] = useState<Set<string>>(new Set());

  const fetchUsers = useCallback(async () => {
    try {
      setUsersLoading(true);
      const result = await api.admin.getAllUsers();
      setUsers(result.users);
    } catch (err) {
      console.error('Failed to load users:', err);
      addToast('error', 'Failed to load users');
    } finally {
      setUsersLoading(false);
    }
  }, [addToast]);

  useEffect(() => {
    if (activeTab === 'users') {
      fetchUsers();
    }
  }, [activeTab, fetchUsers]);

  const handleBulkExportUsers = useCallback(async () => {
    try {
      const blob = await api.admin.exportUsersToCSV();
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `users-export-${new Date().toISOString().split('T')[0]}.csv`;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      window.URL.revokeObjectURL(url);

      addToast('success', 'Users exported successfully');
    } catch (_err) {
      addToast('error', 'Failed to export users');
    }
  }, [addToast]);

  const handleBulkImportUsers = useCallback(
    async (file: File) => {
      try {
        const text = await file.text();
        const result = await api.admin.importUsersFromCSV(text);
        addToast(
          'success',
          `Import complete: ${result.successCount} created, ${result.failureCount} failed`
        );
        fetchUsers();
      } catch (_err) {
        addToast('error', 'Failed to import users');
      }
    },
    [addToast, fetchUsers]
  );

  const handleBulkDeleteUsers = useCallback(() => {
    if (selectedUsers.size === 0) return;

    setConfirmDialog({
      isOpen: true,
      title: 'Delete Users',
      message: `Are you sure you want to delete ${selectedUsers.size} user(s)? This action cannot be undone.`,
      variant: 'destructive',
      onConfirm: async () => {
        try {
          await Promise.all(Array.from(selectedUsers).map(id => api.admin.deleteUser(id)));
          addToast('success', `${selectedUsers.size} user(s) deleted successfully`);
          setSelectedUsers(new Set());
          fetchUsers();
        } catch (_err) {
          addToast('error', 'Failed to delete some users');
        }
      },
    });
  }, [selectedUsers, addToast, fetchUsers]);

  // =========================
  // TAB 3: ACTIVITY TIMELINE
  // =========================
  const [activities, setActivities] = useState<UserActivityEvent[]>([]);
  const [activitiesLoading, setActivitiesLoading] = useState(false);
  const [autoRefresh, setAutoRefresh] = useState(false);
  const [refreshInterval, setRefreshInterval] = useState(30); // seconds

  const fetchActivities = useCallback(async () => {
    try {
      setActivitiesLoading(true);
      // Get system-wide activity (all users)
      const result = await api.admin.getSystemActivity({
        limit: 100,
      });
      // Convert UserActivityDto to UserActivityEvent format
      const events: UserActivityEvent[] = result.activities.map(activity => ({
        id: activity.id,
        eventType: activity.action,
        description: `${activity.action} on ${activity.resource}`,
        userId: null,
        userEmail: null,
        entityId: activity.resourceId || null,
        entityType: activity.resource,
        timestamp: activity.createdAt,
        severity: activity.result === 'Success' ? 'Info' : 'Error',
        metadata: activity.details
          ? (() => {
              try {
                return JSON.parse(activity.details);
              } catch {
                return { raw: activity.details };
              }
            })()
          : undefined,
      }));
      setActivities(events);
    } catch (err) {
      console.error('Failed to load activity timeline:', err);
      addToast('error', 'Failed to load activity timeline');
    } finally {
      setActivitiesLoading(false);
    }
  }, [addToast]);

  useEffect(() => {
    if (activeTab === 'activity') {
      fetchActivities();
    }
  }, [activeTab, fetchActivities]);

  // Auto-refresh timer for activity timeline
  useEffect(() => {
    if (activeTab === 'activity' && autoRefresh) {
      const intervalId = setInterval(() => {
        fetchActivities();
      }, refreshInterval * 1000);

      return () => clearInterval(intervalId);
    }
  }, [activeTab, autoRefresh, refreshInterval, fetchActivities]);

  if (!user) return null;

  return (
    <AdminAuthGuard loading={authLoading} user={user}>
      <div className="p-8 max-w-7xl mx-auto">
        {/* Header */}
        <div className="flex justify-between items-center mb-8">
          <div>
            <h1 className="text-3xl font-bold mb-2">System Management</h1>
            <p className="text-gray-600">
              Comprehensive management for API keys, users, and system activity
            </p>
          </div>
          <Link href="/admin" className="text-blue-600 hover:underline flex items-center gap-2">
            ← Back to Dashboard
          </Link>
        </div>

        {/* Toast Notifications */}
        {toasts.length > 0 && (
          <div className="fixed top-4 right-4 z-50 space-y-2">
            {toasts.map(toast => (
              <Alert
                key={toast.id}
                variant={toast.type === 'error' ? 'destructive' : 'default'}
                className="min-w-[300px]"
              >
                <AlertCircle className="h-4 w-4" />
                <AlertDescription>{toast.message}</AlertDescription>
              </Alert>
            ))}
          </div>
        )}

        {/* Tabs */}
        <Tabs value={activeTab} onValueChange={value => setActiveTab(value as TabValue)}>
          <TabsList className="grid w-full grid-cols-3 mb-6">
            <TabsTrigger value="api-keys" className="flex items-center gap-2">
              <Key className="h-4 w-4" />
              API Keys
            </TabsTrigger>
            <TabsTrigger value="users" className="flex items-center gap-2">
              <Users className="h-4 w-4" />
              Users
            </TabsTrigger>
            <TabsTrigger value="activity" className="flex items-center gap-2">
              <Activity className="h-4 w-4" />
              Activity
            </TabsTrigger>
          </TabsList>

          {/* TAB 1: API Keys Management */}
          <TabsContent value="api-keys" className="space-y-4">
            <Card>
              <CardHeader>
                <div className="flex justify-between items-center">
                  <div>
                    <CardTitle>API Keys Management</CardTitle>
                    <CardDescription>
                      Manage API keys with advanced filtering and bulk operations
                    </CardDescription>
                  </div>
                  <div className="flex gap-2">
                    <Button variant="outline" size="sm" onClick={fetchApiKeys}>
                      <RefreshCw className="h-4 w-4 mr-2" />
                      Refresh
                    </Button>
                    <Button variant="outline" size="sm" onClick={handleExportKeys}>
                      <Download className="h-4 w-4 mr-2" />
                      Export CSV
                    </Button>
                    <Button size="sm" onClick={() => setIsCreateModalOpen(true)}>
                      <Plus className="h-4 w-4 mr-2" />
                      Create Key
                    </Button>
                  </div>
                </div>
              </CardHeader>
              <CardContent>
                <div className="grid grid-cols-1 lg:grid-cols-4 gap-4">
                  {/* Filter Panel */}
                  <div className="lg:col-span-1">
                    <ApiKeyFilterPanel
                      filters={apiKeyFilters}
                      onFiltersChange={setApiKeyFilters}
                      onReset={() => setApiKeyFilters({})}
                    />
                  </div>

                  {/* Keys List */}
                  <div className="lg:col-span-3">
                    {apiKeysLoading ? (
                      <div className="text-center py-8 text-gray-500">Loading API keys...</div>
                    ) : (
                      <>
                        <BulkActionBar
                          selectedCount={selectedKeys.size}
                          totalCount={apiKeys.length}
                          itemLabel="keys"
                          itemLabelSingular="key"
                          actions={[
                            {
                              id: 'delete',
                              label: 'Delete',
                              icon: Trash2,
                              variant: 'destructive',
                              onClick: handleBulkDeleteKeys,
                              tooltip: 'Delete selected API keys',
                            },
                          ]}
                          onClearSelection={() => setSelectedKeys(new Set())}
                        />
                        <div className="text-sm text-gray-600 mt-4">
                          {apiKeys.length} API key(s) found
                        </div>
                      </>
                    )}
                  </div>
                </div>
              </CardContent>
            </Card>
          </TabsContent>

          {/* TAB 2: User Management */}
          <TabsContent value="users" className="space-y-4">
            <Card>
              <CardHeader>
                <div className="flex justify-between items-center">
                  <div>
                    <CardTitle>User Management</CardTitle>
                    <CardDescription>
                      Bulk operations for user import, export, and deletion
                    </CardDescription>
                  </div>
                  <div className="flex gap-2">
                    <Button variant="outline" size="sm" onClick={fetchUsers}>
                      <RefreshCw className="h-4 w-4 mr-2" />
                      Refresh
                    </Button>
                    <Button variant="outline" size="sm" onClick={handleBulkExportUsers}>
                      <Download className="h-4 w-4 mr-2" />
                      Export CSV
                    </Button>
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={() => {
                        const input = document.createElement('input');
                        input.type = 'file';
                        input.accept = '.csv';
                        input.onchange = e => {
                          const file = (e.target as HTMLInputElement).files?.[0];
                          if (file) handleBulkImportUsers(file);
                        };
                        input.click();
                      }}
                    >
                      <Upload className="h-4 w-4 mr-2" />
                      Import CSV
                    </Button>
                  </div>
                </div>
              </CardHeader>
              <CardContent>
                {usersLoading ? (
                  <div className="text-center py-8 text-gray-500">Loading users...</div>
                ) : (
                  <>
                    <BulkActionBar
                      selectedCount={selectedUsers.size}
                      totalCount={users.length}
                      itemLabel="users"
                      itemLabelSingular="user"
                      actions={[
                        {
                          id: 'delete',
                          label: 'Delete',
                          icon: Trash2,
                          variant: 'destructive',
                          onClick: handleBulkDeleteUsers,
                          tooltip: 'Delete selected users',
                        },
                      ]}
                      onClearSelection={() => setSelectedUsers(new Set())}
                    />
                    <div className="text-sm text-gray-600 mt-4">{users.length} user(s) found</div>
                  </>
                )}
              </CardContent>
            </Card>
          </TabsContent>

          {/* TAB 3: Activity Timeline */}
          <TabsContent value="activity" className="space-y-4">
            <Card>
              <CardHeader>
                <div className="flex justify-between items-center">
                  <div>
                    <CardTitle>System Activity Timeline</CardTitle>
                    <CardDescription>
                      Real-time monitoring of system-wide user activity
                    </CardDescription>
                  </div>
                  <div className="flex items-center gap-2">
                    <div className="flex items-center gap-2 mr-2">
                      <label className="flex items-center gap-2 text-sm">
                        <input
                          type="checkbox"
                          checked={autoRefresh}
                          onChange={e => setAutoRefresh(e.target.checked)}
                          className="rounded border-gray-300"
                        />
                        Auto-refresh
                      </label>
                      {autoRefresh && (
                        <select
                          value={refreshInterval}
                          onChange={e => setRefreshInterval(Number(e.target.value))}
                          className="text-sm border rounded px-2 py-1"
                          aria-label="Refresh interval"
                        >
                          <option value="10">10s</option>
                          <option value="30">30s</option>
                          <option value="60">1m</option>
                          <option value="300">5m</option>
                        </select>
                      )}
                    </div>
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={fetchActivities}
                      disabled={activitiesLoading}
                    >
                      <RefreshCw
                        className={`h-4 w-4 mr-2 ${activitiesLoading ? 'animate-spin' : ''}`}
                      />
                      Refresh
                    </Button>
                  </div>
                </div>
              </CardHeader>
              <CardContent>
                {activitiesLoading ? (
                  <div className="text-center py-8 text-gray-500">Loading activities...</div>
                ) : (
                  <UserActivityTimeline events={activities} pageSize={20} />
                )}
              </CardContent>
            </Card>
          </TabsContent>
        </Tabs>

        {/* API Key Creation Modal */}
        <ApiKeyCreationModal
          isOpen={isCreateModalOpen}
          onClose={() => setIsCreateModalOpen(false)}
          onApiKeyCreated={apiKey => {
            addToast('success', `API key "${apiKey.keyName}" created successfully`);
            setIsCreateModalOpen(false);
            fetchApiKeys();
          }}
        />

        {/* Confirmation Dialog */}
        <ConfirmationDialog
          isOpen={confirmDialog.isOpen}
          onClose={() => setConfirmDialog({ ...confirmDialog, isOpen: false })}
          onConfirm={confirmDialog.onConfirm}
          title={confirmDialog.title}
          message={confirmDialog.message}
          variant={confirmDialog.variant}
        />
      </div>
    </AdminAuthGuard>
  );
}
