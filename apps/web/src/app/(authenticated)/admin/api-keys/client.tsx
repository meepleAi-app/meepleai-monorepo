'use client';

import { useCallback, useEffect, useState } from 'react';

import { AlertCircle, Key, Download, Trash2, BarChart3, Eye, EyeOff } from 'lucide-react';
import Link from 'next/link';

import { AdminAuthGuard, BulkActionBar } from '@/components/admin';
import { useAuthUser } from '@/components/auth/AuthProvider';
import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/data-display/table';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { Button } from '@/components/ui/primitives/button';
import { Checkbox } from '@/components/ui/primitives/checkbox';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { api } from '@/lib/api';
import type {
  ApiKeyWithStatsDto,
  CreateApiKeyRequest,
  CreateApiKeyResponse,
} from '@/lib/api/schemas';
import { cn } from '@/lib/utils';

type ToastMessage = {
  id: string;
  type: 'success' | 'error' | 'info';
  message: string;
};

type ConfirmationDialog = {
  isOpen: boolean;
  title: string;
  message: string;
  onConfirm: () => void;
};

type CreateKeyModalState = {
  isOpen: boolean;
  createdKey?: CreateApiKeyResponse;
};

type StatsModalState = {
  isOpen: boolean;
  apiKey?: ApiKeyWithStatsDto;
};

export function ApiKeysPageClient() {
  const { user, loading: authLoading } = useAuthUser();

  if (!user) return null;

  // State
  const [apiKeys, setApiKeys] = useState<ApiKeyWithStatsDto[]>([]);
  const [filteredKeys, setFilteredKeys] = useState<ApiKeyWithStatsDto[]>([]);
  const [dataLoading, setDataLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [toasts, setToasts] = useState<ToastMessage[]>([]);
  const [confirmation, setConfirmation] = useState<ConfirmationDialog>({
    isOpen: false,
    title: '',
    message: '',
    onConfirm: () => {},
  });
  const [createModal, setCreateModal] = useState<CreateKeyModalState>({
    isOpen: false,
  });
  const [statsModal, setStatsModal] = useState<StatsModalState>({
    isOpen: false,
  });
  const [selectedKeys, setSelectedKeys] = useState<Set<string>>(new Set());

  // Filters
  const [statusFilter, setStatusFilter] = useState('all'); // all | active | revoked | expired
  const [userIdFilter, setUserIdFilter] = useState('');
  const [searchTerm, setSearchTerm] = useState('');
  const [includeRevoked, setIncludeRevoked] = useState(false);
  const [showPlaintextKey, setShowPlaintextKey] = useState(false);

  // Toast management
  const addToast = useCallback((type: 'success' | 'error' | 'info', message: string) => {
    const id = `toast-${Date.now()}-${Math.random()}`;
    setToasts(prev => [...prev, { id, type, message }]);
    setTimeout(() => {
      setToasts(prev => prev.filter(t => t.id !== id));
    }, 5000);
  }, []);

  const removeToast = useCallback((id: string) => {
    setToasts(prev => prev.filter(t => t.id !== id));
  }, []);

  // Fetch API keys
  const fetchApiKeys = useCallback(async () => {
    try {
      setDataLoading(true);
      setError(null);

      const result = await api.admin.getApiKeysWithStats({
        userId: userIdFilter || undefined,
        includeRevoked,
      });

      setApiKeys(result.keys);
      setDataLoading(false);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'An error occurred');
      setDataLoading(false);
      addToast('error', 'Failed to load API keys');
    }
  }, [userIdFilter, includeRevoked, addToast]);

  useEffect(() => {
    fetchApiKeys();
  }, [fetchApiKeys]);

  // Apply filters
  useEffect(() => {
    let filtered = [...apiKeys];

    // Status filter
    if (statusFilter !== 'all') {
      filtered = filtered.filter(k => {
        const now = new Date();
        const expiresAt = k.apiKey.expiresAt ? new Date(k.apiKey.expiresAt) : null;
        const isExpired = expiresAt && expiresAt < now;

        if (statusFilter === 'active') return k.apiKey.isActive && !isExpired;
        if (statusFilter === 'revoked') return !k.apiKey.isActive;
        if (statusFilter === 'expired') return isExpired;
        return true;
      });
    }

    // Search filter
    if (searchTerm) {
      filtered = filtered.filter(
        k =>
          k.apiKey.keyName.toLowerCase().includes(searchTerm.toLowerCase()) ||
          k.apiKey.keyPrefix.toLowerCase().includes(searchTerm.toLowerCase())
      );
    }

    setFilteredKeys(filtered);
  }, [apiKeys, statusFilter, searchTerm]);

  // Create API key form state
  const [newKeyForm, setNewKeyForm] = useState({
    keyName: '',
    scopes: 'read:games,read:rules',
    expiresAt: '',
  });

  // Create API key
  const handleCreateKey = useCallback(async () => {
    if (!newKeyForm.keyName.trim()) {
      addToast('error', 'Key name is required');
      return;
    }

    try {
      const request: CreateApiKeyRequest = {
        keyName: newKeyForm.keyName.trim(),
        scopes: newKeyForm.scopes,
        expiresAt: newKeyForm.expiresAt || null,
      };

      // Note: This endpoint is not in the admin client, it's a user endpoint
      // We need to call it directly
      const response = await fetch('/api/v1/api-keys', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        credentials: 'include',
        body: JSON.stringify(request),
      });

      if (!response.ok) {
        throw new Error('Failed to create API key');
      }

      const result = await response.json();

      addToast('success', 'API key created successfully');
      setCreateModal({ isOpen: true, createdKey: result.apiKey });
      setNewKeyForm({ keyName: '', scopes: 'read:games,read:rules', expiresAt: '' });
      fetchApiKeys();
    } catch (err) {
      addToast('error', err instanceof Error ? err.message : 'Failed to create API key');
    }
  }, [newKeyForm, addToast, fetchApiKeys]);

  // Delete API key
  const handleDelete = useCallback(
    async (keyId: string, keyName: string) => {
      setConfirmation({
        isOpen: true,
        title: 'Delete API Key',
        message: `Are you sure you want to permanently delete "${keyName}"? This action cannot be undone.`,
        onConfirm: async () => {
          try {
            await api.admin.deleteApiKey(keyId);
            addToast('success', `API key "${keyName}" deleted successfully`);
            setConfirmation({ isOpen: false, title: '', message: '', onConfirm: () => {} });
            fetchApiKeys();
          } catch (err) {
            addToast('error', err instanceof Error ? err.message : 'Failed to delete API key');
            setConfirmation({ isOpen: false, title: '', message: '', onConfirm: () => {} });
          }
        },
      });
    },
    [addToast, fetchApiKeys]
  );

  // Bulk delete
  const handleBulkDelete = useCallback(() => {
    if (selectedKeys.size === 0) return;

    setConfirmation({
      isOpen: true,
      title: 'Delete Multiple API Keys',
      message: `Are you sure you want to delete ${selectedKeys.size} API key(s)? This action cannot be undone.`,
      onConfirm: async () => {
        try {
          await Promise.all(Array.from(selectedKeys).map(id => api.admin.deleteApiKey(id)));
          addToast('success', `${selectedKeys.size} API key(s) deleted successfully`);
          setSelectedKeys(new Set());
          setConfirmation({ isOpen: false, title: '', message: '', onConfirm: () => {} });
          fetchApiKeys();
        } catch (_err) {
          addToast('error', 'Failed to delete some API keys');
          setConfirmation({ isOpen: false, title: '', message: '', onConfirm: () => {} });
        }
      },
    });
  }, [selectedKeys, addToast, fetchApiKeys]);

  // Export to CSV
  const handleExport = useCallback(async () => {
    try {
      const blob = await api.admin.exportApiKeysToCSV({
        userId: userIdFilter || undefined,
        isActive: statusFilter === 'active' ? true : statusFilter === 'revoked' ? false : undefined,
        searchTerm: searchTerm || undefined,
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
  }, [userIdFilter, statusFilter, searchTerm, addToast]);

  // Toggle selection
  const toggleKeySelection = useCallback((keyId: string) => {
    setSelectedKeys(prev => {
      const newSet = new Set(prev);
      if (newSet.has(keyId)) {
        newSet.delete(keyId);
      } else {
        newSet.add(keyId);
      }
      return newSet;
    });
  }, []);

  const toggleAllKeys = useCallback(() => {
    if (selectedKeys.size === filteredKeys.length) {
      setSelectedKeys(new Set());
    } else {
      setSelectedKeys(new Set(filteredKeys.map(k => k.apiKey.id)));
    }
  }, [filteredKeys, selectedKeys.size]);

  // Format date
  const formatDate = (dateString: string | null | undefined) => {
    if (!dateString) return 'Never';
    return new Date(dateString).toLocaleString();
  };

  // Check if expired
  const isExpired = (expiresAt: string | null | undefined) => {
    if (!expiresAt) return false;
    return new Date(expiresAt) < new Date();
  };

  // Show stats modal
  const showStats = (apiKey: ApiKeyWithStatsDto) => {
    setStatsModal({ isOpen: true, apiKey });
  };

  if (dataLoading) {
    return (
      <AdminAuthGuard loading={authLoading} user={user}>
        <div className="p-8">
          <h1 className="text-3xl font-bold mb-6">API Keys Management</h1>
          <p>Loading...</p>
        </div>
      </AdminAuthGuard>
    );
  }

  if (error) {
    return (
      <AdminAuthGuard loading={authLoading} user={user}>
        <div className="p-8">
          <h1 className="text-3xl font-bold mb-6">API Keys Management</h1>
          <div className="p-4 bg-red-50 border border-red-600 rounded flex items-center gap-2">
            <AlertCircle className="h-5 w-5 text-red-600" />
            <span>{error}</span>
          </div>
          <Link href="/admin" className="text-blue-600 hover:underline mt-4 inline-block">
            ← Back to Admin Dashboard
          </Link>
        </div>
      </AdminAuthGuard>
    );
  }

  return (
    <AdminAuthGuard loading={authLoading} user={user}>
      <div className="p-8 max-w-7xl mx-auto">
        {/* Header */}
        <div className="flex justify-between items-center mb-8">
          <div>
            <h1 className="text-3xl font-bold mb-2">API Keys Management</h1>
            <p className="text-gray-600">
              Manage API keys, view usage statistics, and control access
            </p>
          </div>
          <Link href="/admin" className="text-blue-600 hover:underline flex items-center gap-2">
            ← Back to Dashboard
          </Link>
        </div>

        {/* Toasts */}
        {toasts.length > 0 && (
          <div className="fixed top-4 right-4 z-50 space-y-2">
            {toasts.map(toast => (
              <div
                key={toast.id}
                className={cn(
                  'p-4 rounded shadow-lg flex items-center justify-between gap-4 min-w-[300px]',
                  toast.type === 'success' && 'bg-green-50 border border-green-600 text-green-800',
                  toast.type === 'error' && 'bg-red-50 border border-red-600 text-red-800',
                  toast.type === 'info' && 'bg-blue-50 border border-blue-600 text-blue-800'
                )}
              >
                <span>{toast.message}</span>
                <button
                  onClick={() => removeToast(toast.id)}
                  className="text-sm font-bold hover:opacity-70"
                >
                  ✕
                </button>
              </div>
            ))}
          </div>
        )}

        {/* Filters */}
        <Card className="mb-6">
          <CardHeader>
            <CardTitle>Filters</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
              <div>
                <Label htmlFor="search">Search</Label>
                <Input
                  id="search"
                  placeholder="Key name or prefix..."
                  value={searchTerm}
                  onChange={e => setSearchTerm(e.target.value)}
                />
              </div>
              <div>
                <Label htmlFor="status">Status</Label>
                <Select value={statusFilter} onValueChange={setStatusFilter}>
                  <SelectTrigger id="status">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="all">All</SelectItem>
                    <SelectItem value="active">Active</SelectItem>
                    <SelectItem value="revoked">Revoked</SelectItem>
                    <SelectItem value="expired">Expired</SelectItem>
                  </SelectContent>
                </Select>
              </div>
              <div>
                <Label htmlFor="userId">User ID</Label>
                <Input
                  id="userId"
                  placeholder="Filter by user ID..."
                  value={userIdFilter}
                  onChange={e => setUserIdFilter(e.target.value)}
                />
              </div>
              <div className="flex items-end">
                <label className="flex items-center gap-2">
                  <Checkbox
                    checked={includeRevoked}
                    onCheckedChange={checked => setIncludeRevoked(checked === true)}
                  />
                  <span className="text-sm">Include Revoked</span>
                </label>
              </div>
            </div>
          </CardContent>
        </Card>

        {/* Actions */}
        <div className="mb-4 flex gap-4 flex-wrap">
          <Dialog
            open={createModal.isOpen && !createModal.createdKey}
            onOpenChange={open => {
              if (!open) {
                setCreateModal({ isOpen: false });
                setNewKeyForm({ keyName: '', scopes: 'read:games,read:rules', expiresAt: '' });
              }
            }}
          >
            <Button
              onClick={() => setCreateModal({ isOpen: true })}
              data-testid="create-key-button"
            >
              <Key className="h-4 w-4 mr-2" />
              Create API Key
            </Button>
            <DialogContent>
              <DialogHeader>
                <DialogTitle>Create New API Key</DialogTitle>
                <DialogDescription>
                  Create a new API key with custom scopes and expiration
                </DialogDescription>
              </DialogHeader>
              <div className="space-y-4">
                <div>
                  <Label htmlFor="keyName">Key Name *</Label>
                  <Input
                    id="keyName"
                    placeholder="e.g., Production API Key"
                    value={newKeyForm.keyName}
                    onChange={e => setNewKeyForm(prev => ({ ...prev, keyName: e.target.value }))}
                  />
                </div>
                <div>
                  <Label htmlFor="scopes">Scopes</Label>
                  <Input
                    id="scopes"
                    placeholder="e.g., read:games,read:rules"
                    value={newKeyForm.scopes}
                    onChange={e => setNewKeyForm(prev => ({ ...prev, scopes: e.target.value }))}
                  />
                </div>
                <div>
                  <Label htmlFor="expiresAt">Expires At (Optional)</Label>
                  <Input
                    id="expiresAt"
                    type="datetime-local"
                    value={newKeyForm.expiresAt}
                    onChange={e => setNewKeyForm(prev => ({ ...prev, expiresAt: e.target.value }))}
                  />
                </div>
              </div>
              <DialogFooter>
                <Button variant="outline" onClick={() => setCreateModal({ isOpen: false })}>
                  Cancel
                </Button>
                <Button onClick={handleCreateKey}>Create Key</Button>
              </DialogFooter>
            </DialogContent>
          </Dialog>

          <Button variant="outline" onClick={handleExport} data-testid="export-csv-button">
            <Download className="h-4 w-4 mr-2" />
            Export CSV
          </Button>
        </div>

        {/* Bulk Action Bar */}
        <BulkActionBar
          selectedCount={selectedKeys.size}
          totalCount={filteredKeys.length}
          itemLabel="keys"
          itemLabelSingular="key"
          actions={[
            {
              id: 'delete',
              label: 'Delete',
              icon: Trash2,
              variant: 'destructive',
              onClick: () => handleBulkDelete(),
              tooltip: 'Delete selected API keys',
            },
          ]}
          onClearSelection={() => setSelectedKeys(new Set())}
          testId="bulk-action-bar"
        />

        {/* API Keys Table */}
        <Card>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead className="w-12">
                  <Checkbox
                    checked={selectedKeys.size === filteredKeys.length && filteredKeys.length > 0}
                    onCheckedChange={toggleAllKeys}
                  />
                </TableHead>
                <TableHead>Key Name</TableHead>
                <TableHead>Prefix</TableHead>
                <TableHead>Scopes</TableHead>
                <TableHead>Created</TableHead>
                <TableHead>Expires</TableHead>
                <TableHead>Last Used</TableHead>
                <TableHead>Status</TableHead>
                <TableHead>Usage</TableHead>
                <TableHead>Actions</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {filteredKeys.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={10} className="text-center py-8 text-gray-500">
                    No API keys found
                  </TableCell>
                </TableRow>
              ) : (
                filteredKeys.map(keyData => (
                  <TableRow key={keyData.apiKey.id}>
                    <TableCell>
                      <Checkbox
                        checked={selectedKeys.has(keyData.apiKey.id)}
                        onCheckedChange={() => toggleKeySelection(keyData.apiKey.id)}
                      />
                    </TableCell>
                    <TableCell className="font-medium">{keyData.apiKey.keyName}</TableCell>
                    <TableCell>
                      <code className="text-xs bg-gray-100 px-2 py-1 rounded">
                        {keyData.apiKey.keyPrefix}
                      </code>
                    </TableCell>
                    <TableCell className="text-sm">{keyData.apiKey.scopes}</TableCell>
                    <TableCell className="text-sm">
                      {formatDate(keyData.apiKey.createdAt)}
                    </TableCell>
                    <TableCell className="text-sm">
                      {keyData.apiKey.expiresAt ? (
                        <span className={isExpired(keyData.apiKey.expiresAt) ? 'text-red-600' : ''}>
                          {formatDate(keyData.apiKey.expiresAt)}
                        </span>
                      ) : (
                        'Never'
                      )}
                    </TableCell>
                    <TableCell className="text-sm">
                      {formatDate(keyData.apiKey.lastUsedAt)}
                    </TableCell>
                    <TableCell>
                      {isExpired(keyData.apiKey.expiresAt) ? (
                        <Badge variant="destructive">Expired</Badge>
                      ) : keyData.apiKey.isActive ? (
                        <Badge variant="default">Active</Badge>
                      ) : (
                        <Badge variant="secondary">Revoked</Badge>
                      )}
                    </TableCell>
                    <TableCell>
                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={() => showStats(keyData)}
                        data-testid={`stats-button-${keyData.apiKey.id}`}
                        aria-label="View usage statistics"
                      >
                        <BarChart3 className="h-4 w-4" />
                      </Button>
                    </TableCell>
                    <TableCell>
                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={() => handleDelete(keyData.apiKey.id, keyData.apiKey.keyName)}
                        data-testid={`delete-button-${keyData.apiKey.id}`}
                        aria-label="Delete API key"
                      >
                        <Trash2 className="h-4 w-4 text-red-600" />
                      </Button>
                    </TableCell>
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>
        </Card>

        {/* Confirmation Dialog */}
        <Dialog
          open={confirmation.isOpen}
          onOpenChange={open => {
            if (!open)
              setConfirmation({ isOpen: false, title: '', message: '', onConfirm: () => {} });
          }}
        >
          <DialogContent>
            <DialogHeader>
              <DialogTitle>{confirmation.title}</DialogTitle>
              <DialogDescription>{confirmation.message}</DialogDescription>
            </DialogHeader>
            <DialogFooter>
              <Button
                variant="outline"
                onClick={() =>
                  setConfirmation({ isOpen: false, title: '', message: '', onConfirm: () => {} })
                }
              >
                Cancel
              </Button>
              <Button variant="destructive" onClick={confirmation.onConfirm}>
                Confirm
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>

        {/* Created Key Dialog */}
        <Dialog
          open={createModal.isOpen && !!createModal.createdKey}
          onOpenChange={open => {
            if (!open) setCreateModal({ isOpen: false });
          }}
        >
          <DialogContent>
            <DialogHeader>
              <DialogTitle>API Key Created Successfully</DialogTitle>
              <DialogDescription>
                Save this key securely - it will only be shown once
              </DialogDescription>
            </DialogHeader>
            {createModal.createdKey && (
              <div className="space-y-4">
                <div>
                  <Label>Key Name</Label>
                  <p className="font-medium">{createModal.createdKey.keyName}</p>
                </div>
                <div>
                  <Label>API Key</Label>
                  <div className="flex items-center gap-2">
                    <code className="flex-1 text-xs bg-gray-100 px-3 py-2 rounded font-mono break-all">
                      {showPlaintextKey ? createModal.createdKey.plaintextKey : '••••••••••••••••'}
                    </code>
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => setShowPlaintextKey(!showPlaintextKey)}
                    >
                      {showPlaintextKey ? (
                        <EyeOff className="h-4 w-4" />
                      ) : (
                        <Eye className="h-4 w-4" />
                      )}
                    </Button>
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => {
                        // eslint-disable-next-line @typescript-eslint/no-non-null-assertion -- Key exists when dialog shows
                        navigator.clipboard.writeText(createModal.createdKey!.plaintextKey);
                        addToast('success', 'API key copied to clipboard');
                      }}
                    >
                      Copy
                    </Button>
                  </div>
                </div>
              </div>
            )}
            <DialogFooter>
              <Button onClick={() => setCreateModal({ isOpen: false })}>I've Saved It</Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>

        {/* Stats Dialog */}
        <Dialog
          open={statsModal.isOpen}
          onOpenChange={open => {
            if (!open) setStatsModal({ isOpen: false });
          }}
        >
          <DialogContent>
            <DialogHeader>
              <DialogTitle>Usage Statistics</DialogTitle>
              <DialogDescription>{statsModal.apiKey?.apiKey.keyName}</DialogDescription>
            </DialogHeader>
            {statsModal.apiKey && (
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <Label>Total Usage</Label>
                  <p className="text-2xl font-bold">
                    {statsModal.apiKey.usageStats.totalUsageCount}
                  </p>
                </div>
                <div>
                  <Label>Last 24 Hours</Label>
                  <p className="text-2xl font-bold">
                    {statsModal.apiKey.usageStats.usageCountLast24Hours}
                  </p>
                </div>
                <div>
                  <Label>Last 7 Days</Label>
                  <p className="text-2xl font-bold">
                    {statsModal.apiKey.usageStats.usageCountLast7Days}
                  </p>
                </div>
                <div>
                  <Label>Last 30 Days</Label>
                  <p className="text-2xl font-bold">
                    {statsModal.apiKey.usageStats.usageCountLast30Days}
                  </p>
                </div>
                <div className="col-span-2">
                  <Label>Average Requests/Day</Label>
                  <p className="text-2xl font-bold">
                    {statsModal.apiKey.usageStats.averageRequestsPerDay.toFixed(2)}
                  </p>
                </div>
                <div className="col-span-2">
                  <Label>Last Used</Label>
                  <p className="text-sm">{formatDate(statsModal.apiKey.usageStats.lastUsedAt)}</p>
                </div>
              </div>
            )}
            <DialogFooter>
              <Button onClick={() => setStatsModal({ isOpen: false })}>Close</Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      </div>
    </AdminAuthGuard>
  );
}
