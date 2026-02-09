'use client';

/**
 * Tier Limits Configuration Page (Issue #3702)
 * View and edit token/resource limits per subscription tier
 */

import { useState, useEffect, useCallback } from 'react';

import { Edit2 } from 'lucide-react';
import Link from 'next/link';

import { AdminAuthGuard } from '@/components/admin';
import { useAuthUser } from '@/components/auth/AuthProvider';

type TierLimit = {
  tierId: string;
  tierName: string;
  tokensPerMonth: number;
  tokensPerDay: number;
  messagesPerDay: number;
  maxCollectionSize: number;
  maxPdfUploadsPerMonth: number;
  maxAgentsCreated: number;
};

export function TierLimitsClient() {
  const { user, loading: authLoading } = useAuthUser();
  const [limits, setLimits] = useState<TierLimit[]>([]);
  const [loading, setLoading] = useState(true);

  // Fetch tier limits
  const fetchLimits = useCallback(async () => {
    try {
      setLoading(true);
      const response = await fetch('/api/v1/admin/tokens/tiers', {
        credentials: 'include',
      });

      if (!response.ok) throw new Error('Failed to fetch');

      const data = await response.json();

      // Map API response to TierLimit type
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      const mappedLimits = (data.tiers || data || []).map((tier: any) => ({
        tierId: tier.id,
        tierName: tier.name || 'Unknown',
        tokensPerMonth: tier.limits?.tokensPerMonth || tier.monthlyTokenLimit || 0,
        tokensPerDay: tier.limits?.tokensPerDay || 0,
        messagesPerDay: tier.limits?.messagesPerDay || 0,
        maxCollectionSize: tier.limits?.maxCollectionSize || 0,
        maxPdfUploadsPerMonth: tier.limits?.maxPdfUploadsPerMonth || 0,
        maxAgentsCreated: tier.limits?.maxAgentsCreated || 0,
      }));

      setLimits(mappedLimits);
    } catch (err) {
      console.error('Failed to load limits:', err);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchLimits();
  }, [fetchLimits]);

  const formatNumber = (num: number) => {
    if (num === 2147483647) return 'Unlimited'; // int.MaxValue
    return num.toLocaleString();
  };

  if (!user) return null;

  return (
    <AdminAuthGuard loading={authLoading} user={user}>
      <div className="p-8 max-w-7xl mx-auto">
        <div className="flex justify-between items-center mb-8">
          <div>
            <h1 className="text-3xl font-bold">Tier Limits Configuration</h1>
            <p className="text-gray-600 mt-2">Configure resource limits per subscription tier</p>
          </div>
          <Link href="/admin" className="text-blue-600 hover:underline">
            ← Back to Admin
          </Link>
        </div>

        {loading ? (
          <p>Loading tier limits...</p>
        ) : (
          <div className="bg-white shadow rounded-lg overflow-hidden">
            <table className="w-full text-sm">
              <thead className="bg-gray-50 border-b">
                <tr>
                  <th className="text-left p-4 font-semibold">Tier</th>
                  <th className="text-right p-4 font-semibold">Tokens/Month</th>
                  <th className="text-right p-4 font-semibold">Tokens/Day</th>
                  <th className="text-right p-4 font-semibold">Messages/Day</th>
                  <th className="text-right p-4 font-semibold">Collection Size</th>
                  <th className="text-right p-4 font-semibold">PDF Uploads/Month</th>
                  <th className="text-right p-4 font-semibold">Agents</th>
                  <th className="text-center p-4">Actions</th>
                </tr>
              </thead>
              <tbody>
                {limits.map(limit => {
                  const tierColors = {
                    free: 'bg-gray-100 text-gray-800',
                    basic: 'bg-blue-100 text-blue-800',
                    pro: 'bg-purple-100 text-purple-800',
                    enterprise: 'bg-orange-100 text-orange-800',
                  };
                  const colorClass = tierColors[limit.tierName.toLowerCase() as keyof typeof tierColors] || 'bg-gray-100';

                  return (
                    <tr key={limit.tierId} className="border-b hover:bg-gray-50">
                      <td className="p-4">
                        <span className={`inline-block px-3 py-1 rounded font-medium text-sm ${colorClass}`}>
                          {limit.tierName}
                        </span>
                      </td>
                      <td className="p-4 text-right font-mono">{formatNumber(limit.tokensPerMonth)}</td>
                      <td className="p-4 text-right font-mono">{formatNumber(limit.tokensPerDay)}</td>
                      <td className="p-4 text-right font-mono">{formatNumber(limit.messagesPerDay)}</td>
                      <td className="p-4 text-right font-mono">{formatNumber(limit.maxCollectionSize)}</td>
                      <td className="p-4 text-right font-mono">{formatNumber(limit.maxPdfUploadsPerMonth)}</td>
                      <td className="p-4 text-right font-mono">{formatNumber(limit.maxAgentsCreated)}</td>
                      <td className="p-4 text-center">
                        <button
                          className="p-2 hover:bg-gray-100 rounded"
                          title="Edit limits (coming soon)"
                          disabled
                        >
                          <Edit2 className="h-4 w-4 text-gray-400" />
                        </button>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>

            {limits.length === 0 && !loading && (
              <div className="p-8 text-center text-gray-500">
                No tier limits configured. They may need to be seeded in the database.
              </div>
            )}
          </div>
        )}

        <div className="mt-6 p-4 bg-blue-50 border border-blue-200 rounded">
          <p className="text-sm text-blue-900">
            <strong>Note:</strong> Tier limits are currently managed via TokenTier system (Issue #3692).
            Edit functionality requires UpdateTierLimitsCommand integration (Level 2 confirmation).
          </p>
        </div>
      </div>
    </AdminAuthGuard>
  );
}
