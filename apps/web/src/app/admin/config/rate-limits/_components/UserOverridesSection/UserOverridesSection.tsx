'use client';

import { useState, useEffect } from 'react';

import { format } from 'date-fns';
import { Plus, Trash2 } from 'lucide-react';

import { toast } from '@/components/layout/Toast';
import { Spinner } from '@/components/loading/Spinner';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Button } from '@/components/ui/primitives/button';
import { useApiClient } from '@/lib/api/context';
import { type RateLimitOverrideDto } from '@/types';

import { CreateOverrideDialog } from './CreateOverrideDialog';

export function UserOverridesSection() {
  const { rateLimits } = useApiClient();
  const [overrides, setOverrides] = useState<RateLimitOverrideDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [deleting, setDeleting] = useState<string | null>(null);
  const [showCreateDialog, setShowCreateDialog] = useState(false);

  useEffect(() => {
    loadOverrides();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const loadOverrides = async () => {
    setLoading(true);
    try {
      const result = await rateLimits.getUserOverrides();
      setOverrides(result);
    } catch (err: unknown) {
      const error = err as Error;
      toast.error(`Failed to load overrides: ${error.message}`);
    } finally {
      setLoading(false);
    }
  };

  const handleDelete = async (userId: string, userName: string) => {
    if (!confirm(`Remove rate limit override for ${userName}?`)) {
      return;
    }

    setDeleting(userId);
    try {
      await rateLimits.removeOverride(userId);
      toast.success(`Removed override for ${userName}`);
      await loadOverrides();
    } catch (err: unknown) {
      const error = err as Error;
      toast.error(`Failed to remove override: ${error.message}`);
    } finally {
      setDeleting(null);
    }
  };

  const handleCreateSuccess = () => {
    setShowCreateDialog(false);
    loadOverrides();
  };

  return (
    <>
      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <div>
              <CardTitle>User Overrides</CardTitle>
              <CardDescription>Custom limits for specific users</CardDescription>
            </div>
            <Button onClick={() => setShowCreateDialog(true)}>
              <Plus className="w-4 h-4 mr-2" />
              Add Override
            </Button>
          </div>
        </CardHeader>
        <CardContent>
          {loading ? (
            <div className="flex items-center justify-center py-8">
              <Spinner />
            </div>
          ) : overrides.length === 0 ? (
            <div className="text-center py-8">
              <p className="text-gray-600 mb-2">No overrides</p>
              <p className="text-sm text-gray-500">
                All users are using their tier&apos;s default limits
              </p>
            </div>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full border-collapse">
                <thead>
                  <tr className="border-b">
                    <th className="py-3 px-4 text-left font-medium">User</th>
                    <th className="py-3 px-4 text-center font-medium">Tier</th>
                    <th className="py-3 px-4 text-center font-medium">Override Limits</th>
                    <th className="py-3 px-4 text-center font-medium">Expires</th>
                    <th className="py-3 px-4 text-left font-medium">Reason</th>
                    <th className="py-3 px-4 text-center font-medium">Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {overrides.map((override) => (
                    <tr key={override.userId} className="border-b hover:bg-gray-50">
                      <td className="py-3 px-4">
                        <div className="flex items-center gap-3">
                          <div className="w-8 h-8 rounded-full bg-blue-500 text-white flex items-center justify-center font-semibold">
                            {override.userName[0].toUpperCase()}
                          </div>
                          <div>
                            <p className="font-medium">{override.userName}</p>
                            <p className="text-sm text-gray-500">{override.userEmail}</p>
                          </div>
                        </div>
                      </td>
                      <td className="py-3 px-4 text-center">
                        <span className="inline-block px-2 py-1 text-xs bg-gray-100 rounded">
                          {override.userTier}
                        </span>
                      </td>
                      <td className="py-3 px-4">
                        <div className="text-sm space-y-1">
                          {override.maxPendingRequests !== undefined && (
                            <div>Pending: {override.maxPendingRequests}</div>
                          )}
                          {override.maxRequestsPerMonth !== undefined && (
                            <div>Monthly: {override.maxRequestsPerMonth}</div>
                          )}
                          {override.cooldownAfterRejection !== undefined && (
                            <div>Cooldown: {override.cooldownAfterRejection.days}d</div>
                          )}
                        </div>
                      </td>
                      <td className="py-3 px-4 text-center">
                        {override.expiresAt ? (
                          <div>
                            <div className="text-sm">
                              {format(new Date(override.expiresAt), 'MMM d, yyyy')}
                            </div>
                            {override.isExpired && (
                              <span className="text-xs text-red-600">(expired)</span>
                            )}
                          </div>
                        ) : (
                          <span className="text-sm text-gray-500">Never</span>
                        )}
                      </td>
                      <td className="py-3 px-4">
                        <p className="text-sm text-gray-700">{override.reason}</p>
                      </td>
                      <td className="py-3 px-4 text-center">
                        <Button
                          size="sm"
                          variant="ghost"
                          onClick={() => handleDelete(override.userId, override.userName)}
                          disabled={deleting === override.userId}
                        >
                          <Trash2 className="w-4 h-4 text-red-600" />
                        </Button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </CardContent>
      </Card>

      <CreateOverrideDialog
        open={showCreateDialog}
        onClose={() => setShowCreateDialog(false)}
        onSuccess={handleCreateSuccess}
      />
    </>
  );
}
