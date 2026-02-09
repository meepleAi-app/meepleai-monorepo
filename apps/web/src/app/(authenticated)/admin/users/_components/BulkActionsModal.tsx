/**
 * Bulk User Actions Modal - Issue #3947
 *
 * Modal for performing operations on multiple users:
 * - Password Reset
 * - Role Change
 * - Import/Export
 */

'use client';

import { useState } from 'react';

import { Key, UserCog, Download, AlertCircle } from 'lucide-react';

import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/navigation/tabs';
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
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { Textarea } from '@/components/ui/primitives/textarea';
import {
  bulkPasswordReset,
  bulkRoleChange,
  bulkImportUsers,
  bulkExportUsers,
} from '@/lib/api/admin-bulk-users';

interface BulkActionsModalProps {
  open: boolean;
  onClose: () => void;
  selectedUserIds: string[];
  selectedUserEmails: string[];
  onSuccess: () => void;
}

export function BulkActionsModal({
  open,
  onClose,
  selectedUserIds,
  selectedUserEmails: _selectedUserEmails,
  onSuccess,
}: BulkActionsModalProps) {
  const [loading, setLoading] = useState(false);
  const [message, setMessage] = useState('');
  const [newRole, setNewRole] = useState('User');

  const handlePasswordReset = async () => {
    setLoading(true);
    try {
      const result = await bulkPasswordReset({
        userIds: selectedUserIds,
        sendEmail: true,
        message: message || undefined,
      });

      alert(`Success: ${result.successCount}, Failed: ${result.failureCount}`);
      if (result.successCount > 0) {
        onSuccess();
        onClose();
      }
    } catch (err) {
      alert(err instanceof Error ? err.message : 'Failed to reset passwords');
    } finally {
      setLoading(false);
    }
  };

  const handleRoleChange = async () => {
    setLoading(true);
    try {
      const result = await bulkRoleChange({
        userIds: selectedUserIds,
        newRole,
      });

      alert(`Success: ${result.successCount}, Failed: ${result.failureCount}`);
      if (result.successCount > 0) {
        onSuccess();
        onClose();
      }
    } catch (err) {
      alert(err instanceof Error ? err.message : 'Failed to change roles');
    } finally {
      setLoading(false);
    }
  };

  const handleExport = async () => {
    try {
      const blob = await bulkExportUsers(selectedUserIds.length > 0 ? selectedUserIds : undefined);
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `users-export-${new Date().toISOString().split('T')[0]}.csv`;
      a.click();
      window.URL.revokeObjectURL(url);
    } catch (err) {
      alert(err instanceof Error ? err.message : 'Failed to export users');
    }
  };

  const handleImport = async (file: File) => {
    setLoading(true);
    try {
      const text = await file.text();
      const result = await bulkImportUsers(text);

      alert(`Imported: ${result.successCount}, Failed: ${result.failureCount}`);
      if (result.successCount > 0) {
        onSuccess();
      }
    } catch (err) {
      alert(err instanceof Error ? err.message : 'Failed to import users');
    } finally {
      setLoading(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onClose}>
      <DialogContent className="max-w-2xl">
        <DialogHeader>
          <DialogTitle>Bulk User Actions</DialogTitle>
          <DialogDescription>
            {selectedUserIds.length} user(s) selected
          </DialogDescription>
        </DialogHeader>

        <Tabs defaultValue="password-reset" className="w-full">
          <TabsList className="grid w-full grid-cols-3">
            <TabsTrigger value="password-reset">Password Reset</TabsTrigger>
            <TabsTrigger value="role-change">Role Change</TabsTrigger>
            <TabsTrigger value="import-export">Import/Export</TabsTrigger>
          </TabsList>

          {/* Password Reset Tab */}
          <TabsContent value="password-reset" className="space-y-4">
            <Alert>
              <AlertCircle className="h-4 w-4" />
              <AlertDescription>
                Password reset emails will be sent to {selectedUserIds.length} user(s).
              </AlertDescription>
            </Alert>

            <div className="space-y-2">
              <Label htmlFor="reset-message">Optional message</Label>
              <Textarea
                id="reset-message"
                placeholder="Explain why password reset is required..."
                value={message}
                onChange={e => setMessage(e.target.value)}
                rows={3}
              />
            </div>

            <DialogFooter>
              <Button variant="outline" onClick={onClose}>
                Cancel
              </Button>
              <Button onClick={handlePasswordReset} disabled={loading}>
                <Key className="h-4 w-4 mr-2" />
                Reset {selectedUserIds.length} Password(s)
              </Button>
            </DialogFooter>
          </TabsContent>

          {/* Role Change Tab */}
          <TabsContent value="role-change" className="space-y-4">
            <Alert>
              <AlertDescription>
                Change roles for {selectedUserIds.length} user(s).
              </AlertDescription>
            </Alert>

            <div className="space-y-2">
              <Label htmlFor="new-role">New Role</Label>
              <Select value={newRole} onValueChange={setNewRole}>
                <SelectTrigger id="new-role">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="User">User</SelectItem>
                  <SelectItem value="Editor">Editor</SelectItem>
                  <SelectItem value="Admin">Admin</SelectItem>
                </SelectContent>
              </Select>
            </div>

            <DialogFooter>
              <Button variant="outline" onClick={onClose}>
                Cancel
              </Button>
              <Button onClick={handleRoleChange} disabled={loading}>
                <UserCog className="h-4 w-4 mr-2" />
                Change to {newRole}
              </Button>
            </DialogFooter>
          </TabsContent>

          {/* Import/Export Tab */}
          <TabsContent value="import-export" className="space-y-4">
            <div className="space-y-4">
              <div>
                <Label>Export Users</Label>
                <p className="text-sm text-muted-foreground mb-2">
                  {selectedUserIds.length > 0
                    ? `Export ${selectedUserIds.length} selected user(s)`
                    : 'Export all users'}
                </p>
                <Button variant="outline" onClick={handleExport} className="w-full">
                  <Download className="h-4 w-4 mr-2" />
                  Export to CSV
                </Button>
              </div>

              <div>
                <Label htmlFor="import-file">Import Users</Label>
                <p className="text-sm text-muted-foreground mb-2">
                  Upload CSV: email,displayName,role
                </p>
                <Input
                  id="import-file"
                  type="file"
                  accept=".csv"
                  onChange={e => {
                    const file = e.target.files?.[0];
                    if (file) handleImport(file);
                  }}
                  disabled={loading}
                />
              </div>
            </div>
          </TabsContent>
        </Tabs>
      </DialogContent>
    </Dialog>
  );
}
