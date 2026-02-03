'use client';

import { useState, useEffect } from 'react';

import { format } from 'date-fns';
import { Pencil } from 'lucide-react';

import { toast } from '@/components/layout/Toast';
import { Spinner } from '@/components/loading/Spinner';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Button } from '@/components/ui/primitives/button';
import { useApiClient } from '@/lib/api/context';
import { type RateLimitConfigDto, type UpdateTierConfigRequest } from '@/types';

import { TierConfigEditDialog } from './TierConfigEditDialog';

export function TierConfigSection() {
  const { rateLimits } = useApiClient();
  const [configs, setConfigs] = useState<RateLimitConfigDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState<string | null>(null);
  const [editingTier, setEditingTier] = useState<string | null>(null);

  useEffect(() => {
    loadConfigs();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const loadConfigs = async () => {
    setLoading(true);
    try {
      const result = await rateLimits.getTierConfigs();
      setConfigs(result);
    } catch (err: unknown) {
      const error = err as Error;
      toast.error(`Failed to load tier configs: ${error.message}`);
    } finally {
      setLoading(false);
    }
  };

  const handleSave = async (tier: string, data: UpdateTierConfigRequest) => {
    setSaving(tier);
    try {
      await rateLimits.updateTierConfig(tier as RateLimitConfigDto['tier'], data);
      toast.success(`Updated ${tier} tier limits`);
      await loadConfigs(); // Reload to get updated data
      setEditingTier(null);
    } catch (err: unknown) {
      const error = err as Error;
      toast.error(`Failed to update tier: ${error.message}`);
    } finally {
      setSaving(null);
    }
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center py-8">
        <Spinner />
      </div>
    );
  }

  return (
    <>
      <Card>
        <CardHeader>
          <CardTitle>Tier Limits</CardTitle>
          <CardDescription>Configure limits for each subscription tier</CardDescription>
        </CardHeader>
        <CardContent>
          <div className="overflow-x-auto">
            <table className="w-full border-collapse">
              <thead>
                <tr className="border-b">
                  <th className="py-3 px-4 text-left font-medium">Tier</th>
                  <th className="py-3 px-4 text-center font-medium">Max Pending</th>
                  <th className="py-3 px-4 text-center font-medium">Max Monthly</th>
                  <th className="py-3 px-4 text-center font-medium">Rejection Cooldown</th>
                  <th className="py-3 px-4 text-left font-medium">Last Updated</th>
                  <th className="py-3 px-4 text-center font-medium">Actions</th>
                </tr>
              </thead>
              <tbody>
                {configs.map((config) => (
                  <tr key={config.tier} className="border-b hover:bg-gray-50">
                    <td className="py-3 px-4">
                      <div className="flex items-center gap-2">
                        <span className="font-semibold">{config.tierDisplayName}</span>
                        {config.tier === 'Admin' && (
                          <span className="text-xs bg-blue-100 text-blue-800 px-2 py-0.5 rounded">
                            Unlimited
                          </span>
                        )}
                      </div>
                    </td>
                    <td className="py-3 px-4 text-center">{config.maxPendingRequests}</td>
                    <td className="py-3 px-4 text-center">{config.maxRequestsPerMonth}</td>
                    <td className="py-3 px-4 text-center">{config.cooldownDisplay}</td>
                    <td className="py-3 px-4">
                      <div>
                        <div className="text-sm">
                          {format(new Date(config.updatedAt), 'MMM d, yyyy')}
                        </div>
                        {config.updatedByAdminName && (
                          <div className="text-xs text-gray-500">by {config.updatedByAdminName}</div>
                        )}
                      </div>
                    </td>
                    <td className="py-3 px-4 text-center">
                      {config.tier !== 'Admin' ? (
                        <Button
                          size="sm"
                          variant="ghost"
                          onClick={() => setEditingTier(config.tier)}
                          disabled={saving === config.tier}
                        >
                          <Pencil className="w-4 h-4" />
                        </Button>
                      ) : (
                        <span className="text-xs text-gray-400">Non-editable</span>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <div className="mt-4 text-sm text-gray-600">
            <p>
              <strong>Note:</strong> Admin tier has unlimited access and cannot be modified.
            </p>
          </div>
        </CardContent>
      </Card>

      <TierConfigEditDialog
        tier={editingTier}
        config={configs.find((c) => c.tier === editingTier)}
        open={!!editingTier}
        onClose={() => setEditingTier(null)}
        // eslint-disable-next-line @typescript-eslint/no-non-null-assertion -- Safe: callback only called when dialog is open (editingTier is truthy)
        onSave={(data) => handleSave(editingTier!, data)}
        isLoading={saving === editingTier}
      />
    </>
  );
}
