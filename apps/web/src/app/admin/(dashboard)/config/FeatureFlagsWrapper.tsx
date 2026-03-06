'use client';

import { useState, useEffect, useCallback } from 'react';

import FeatureFlagsTab from '@/components/admin/FeatureFlagsTab';
import type { SystemConfigurationDto } from '@/lib/api';
import { api } from '@/lib/api';

export function FeatureFlagsWrapper() {
  const [configurations, setConfigurations] = useState<SystemConfigurationDto[]>([]);
  const [loading, setLoading] = useState(true);

  const fetchConfigurations = useCallback(async () => {
    try {
      setLoading(true);
      const result = await api.config.getConfigurations(undefined, undefined, true, 1, 100);
      setConfigurations(result.items);
    } catch {
      setConfigurations([]);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchConfigurations();
  }, [fetchConfigurations]);

  if (loading) {
    return (
      <div className="h-[400px] bg-white/40 dark:bg-zinc-800/40 backdrop-blur-sm rounded-2xl border border-slate-200/60 dark:border-zinc-700/40 animate-pulse" />
    );
  }

  return (
    <FeatureFlagsTab configurations={configurations} onConfigurationChange={fetchConfigurations} />
  );
}
