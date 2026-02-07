'use client';

/**
 * Feature Flags Management Page (Issue #3701)
 * Minimal implementation - toggle feature flags with admin auth
 */

import { useState, useEffect, useCallback } from 'react';
import { AdminAuthGuard } from '@/components/admin';
import { useAuthUser } from '@/components/auth/AuthProvider';
import { api } from '@/lib/api';
import Link from 'next/link';

type FeatureFlag = {
  key: string;
  enabled: boolean;
  roleRestriction?: string | null;
  tierRestriction?: string | null;
  description?: string | null;
};

export function FeatureFlagsClient() {
  const { user, loading: authLoading } = useAuthUser();
  const [flags, setFlags] = useState<FeatureFlag[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const fetchFlags = useCallback(async () => {
    try {
      setLoading(true);
      const response = await fetch('/api/v1/admin/feature-flags', {
        credentials: 'include',
      });

      if (!response.ok) throw new Error('Failed to fetch feature flags');

      const data = await response.json();
      setFlags(data.featureFlags || []);
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchFlags();
  }, [fetchFlags]);

  const handleToggle = async (key: string, currentlyEnabled: boolean) => {
    try {
      const response = await fetch(`/api/v1/admin/feature-flags/${key}/toggle`, {
        method: 'POST',
        credentials: 'include',
      });

      if (!response.ok) throw new Error('Toggle failed');

      // Optimistic update
      setFlags(prev => prev.map(f =>
        f.key === key ? { ...f, enabled: !currentlyEnabled } : f
      ));
    } catch (err) {
      alert(err instanceof Error ? err.message : 'Failed to toggle');
      fetchFlags(); // Revert on error
    }
  };

  if (!user) return null;

  return (
    <AdminAuthGuard loading={authLoading} user={user}>
      <div className="p-8 max-w-6xl mx-auto">
        <div className="flex justify-between items-center mb-8">
          <h1 className="text-3xl font-bold">Feature Flags</h1>
          <Link href="/admin" className="text-blue-600 hover:underline">
            ← Back to Admin
          </Link>
        </div>

        {loading && <p>Loading...</p>}
        {error && <div className="p-4 bg-red-50 border border-red-300 rounded text-red-800">{error}</div>}

        {!loading && !error && (
          <div className="bg-white shadow rounded-lg overflow-hidden">
            <table className="w-full">
              <thead className="bg-gray-50 border-b">
                <tr>
                  <th className="text-left p-4">Feature</th>
                  <th className="text-left p-4">Description</th>
                  <th className="text-center p-4">Role</th>
                  <th className="text-center p-4">Tier</th>
                  <th className="text-center p-4">Status</th>
                  <th className="text-center p-4">Actions</th>
                </tr>
              </thead>
              <tbody>
                {flags.map(flag => (
                  <tr key={flag.key} className="border-b hover:bg-gray-50">
                    <td className="p-4 font-medium">{flag.key}</td>
                    <td className="p-4 text-sm text-gray-600">{flag.description || '-'}</td>
                    <td className="p-4 text-center text-sm">{flag.roleRestriction || 'All'}</td>
                    <td className="p-4 text-center text-sm">{flag.tierRestriction || 'All'}</td>
                    <td className="p-4 text-center">
                      <span className={`inline-block px-2 py-1 rounded text-xs font-medium ${
                        flag.enabled ? 'bg-green-100 text-green-800' : 'bg-gray-100 text-gray-800'
                      }`}>
                        {flag.enabled ? 'Enabled' : 'Disabled'}
                      </span>
                    </td>
                    <td className="p-4 text-center">
                      <button
                        onClick={() => handleToggle(flag.key, flag.enabled)}
                        className={`px-4 py-2 rounded text-sm font-medium ${
                          flag.enabled
                            ? 'bg-gray-200 hover:bg-gray-300 text-gray-800'
                            : 'bg-blue-600 hover:bg-blue-700 text-white'
                        }`}
                      >
                        {flag.enabled ? 'Disable' : 'Enable'}
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </AdminAuthGuard>
  );
}
