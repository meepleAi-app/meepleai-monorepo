'use client';

import { useState } from 'react';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  AlertTriangleIcon,
  BoxIcon,
  CpuIcon,
  DatabaseIcon,
  HardDriveIcon,
  InfoIcon,
  LayersIcon,
  RefreshCwIcon,
  ServerIcon,
  Trash2Icon,
} from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { createAdminClient } from '@/lib/api/clients/adminClient';
import { HttpClient } from '@/lib/api/core/httpClient';

import { RagEnhancementsTab } from './RagEnhancementsTab';

const httpClient = new HttpClient();
const adminClient = createAdminClient({ httpClient });

interface KBSettingsData {
  embedding: {
    provider: string;
    model: string;
    serviceUrl: string;
  };
  vectorDatabase: {
    type: string;
    url: string;
    grpcPort: string;
  };
  chunking: {
    defaultChunkSize: number;
    chunkOverlap: number;
    minChunkSize: number;
    maxChunkSize: number;
    embeddingTokenLimit: number;
    charsPerToken: number;
  };
  cache: {
    redis: {
      host: string;
      port: string;
    };
    hybridCache: {
      defaultExpiration: string;
      l2Enabled: boolean;
    };
    multiTier: {
      enabled: boolean;
      l1Ttl: string;
      l2Ttl: string;
    };
  };
  reranker: {
    configured: boolean;
    url: string | null;
  };
  storage: {
    provider: string;
  };
}

interface ClearCacheResult {
  success: boolean;
  message: string;
  clearedAt: string | null;
}

function SettingRow({ label, value }: { label: string; value: string | number | boolean }) {
  const displayValue =
    typeof value === 'boolean' ? (value ? 'Enabled' : 'Disabled') : String(value);
  const boolColor =
    typeof value === 'boolean'
      ? value
        ? 'text-green-600 dark:text-green-400'
        : 'text-slate-400 dark:text-zinc-500'
      : 'text-slate-900 dark:text-zinc-100';

  return (
    <div className="flex items-center justify-between py-2 border-b border-slate-100 dark:border-zinc-800 last:border-0">
      <span className="text-sm text-slate-600 dark:text-zinc-400">{label}</span>
      <span className={`text-sm font-mono ${boolColor}`}>{displayValue}</span>
    </div>
  );
}

function SettingsCard({
  title,
  icon: Icon,
  children,
}: {
  title: string;
  icon: React.ComponentType<{ className?: string }>;
  children: React.ReactNode;
}) {
  return (
    <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-xl p-5 border border-slate-200/50 dark:border-zinc-700/50">
      <div className="flex items-center gap-2 mb-4">
        <Icon className="h-5 w-5 text-blue-500" />
        <h3 className="font-semibold text-slate-900 dark:text-zinc-100">{title}</h3>
      </div>
      <div className="space-y-0">{children}</div>
    </div>
  );
}

export function KBSettings() {
  const queryClient = useQueryClient();
  const [showClearConfirm, setShowClearConfirm] = useState(false);
  const [showRebuildConfirm, setShowRebuildConfirm] = useState(false);

  const {
    data: settings,
    isLoading,
    refetch,
    isRefetching,
  } = useQuery({
    queryKey: ['admin', 'kb', 'settings'],
    queryFn: () => adminClient.getKBSettings() as Promise<KBSettingsData | null>,
    staleTime: 300_000,
  });

  const clearCacheMutation = useMutation({
    mutationFn: () => adminClient.clearKBCache() as Promise<ClearCacheResult>,
    onSuccess: () => {
      setShowClearConfirm(false);
      queryClient.invalidateQueries({ queryKey: ['admin'] });
    },
  });

  const rebuildIndexMutation = useMutation({
    mutationFn: async () => {
      // Rebuild all Qdrant collections
      const collections = await adminClient.getVectorCollections();
      const results = [];
      for (const col of collections.collections) {
        const result = await adminClient.rebuildQdrantIndex(col.name);
        results.push(result);
      }
      return results;
    },
    onSuccess: () => {
      setShowRebuildConfirm(false);
      queryClient.invalidateQueries({ queryKey: ['admin'] });
    },
  });

  if (isLoading) {
    return (
      <div className="space-y-4">
        {Array.from({ length: 4 }).map((_, i) => (
          <div key={i} className="h-48 bg-white/40 dark:bg-zinc-800/40 rounded-xl animate-pulse" />
        ))}
      </div>
    );
  }

  if (!settings) {
    return (
      <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-xl p-6 border border-slate-200/50 dark:border-zinc-700/50">
        <p className="text-sm text-slate-600 dark:text-zinc-400">Unable to load KB settings</p>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Info Banner */}
      <div className="flex items-start gap-3 bg-blue-50/70 dark:bg-blue-900/20 backdrop-blur-md rounded-xl p-4 border border-blue-200/50 dark:border-blue-800/50">
        <InfoIcon className="h-5 w-5 text-blue-500 mt-0.5 shrink-0" />
        <div>
          <p className="text-sm font-medium text-blue-900 dark:text-blue-200">
            Read-only Configuration
          </p>
          <p className="text-xs text-blue-700 dark:text-blue-300 mt-1">
            These settings are configured via environment variables and config files. To modify
            them, update the server configuration and restart the API service.
          </p>
        </div>
      </div>

      {/* Refresh Button */}
      <div className="flex justify-end">
        <Button
          variant="outline"
          size="sm"
          onClick={() => refetch()}
          disabled={isRefetching}
          className="gap-2"
        >
          <RefreshCwIcon className={`h-4 w-4 ${isRefetching ? 'animate-spin' : ''}`} />
          Refresh
        </Button>
      </div>

      {/* Settings Cards Grid */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        {/* Embedding Model */}
        <SettingsCard title="Embedding Model" icon={CpuIcon}>
          <SettingRow label="Provider" value={settings.embedding.provider} />
          <SettingRow label="Model" value={settings.embedding.model} />
          <SettingRow label="Service URL" value={settings.embedding.serviceUrl} />
        </SettingsCard>

        {/* Vector Database */}
        <SettingsCard title="Vector Database" icon={DatabaseIcon}>
          <SettingRow label="Type" value={settings.vectorDatabase.type} />
          <SettingRow label="URL" value={settings.vectorDatabase.url} />
          <SettingRow label="gRPC Port" value={settings.vectorDatabase.grpcPort} />
        </SettingsCard>

        {/* Chunking Settings */}
        <SettingsCard title="Text Chunking" icon={LayersIcon}>
          <SettingRow
            label="Default Chunk Size"
            value={`${settings.chunking.defaultChunkSize} chars`}
          />
          <SettingRow label="Chunk Overlap" value={`${settings.chunking.chunkOverlap} chars`} />
          <SettingRow label="Min Chunk Size" value={`${settings.chunking.minChunkSize} chars`} />
          <SettingRow label="Max Chunk Size" value={`${settings.chunking.maxChunkSize} chars`} />
          <SettingRow
            label="Token Limit"
            value={`${settings.chunking.embeddingTokenLimit} tokens`}
          />
          <SettingRow label="Chars/Token Ratio" value={settings.chunking.charsPerToken} />
        </SettingsCard>

        {/* Cache Settings */}
        <SettingsCard title="Cache Configuration" icon={ServerIcon}>
          <SettingRow
            label="Redis Host"
            value={`${settings.cache.redis.host}:${settings.cache.redis.port}`}
          />
          <SettingRow
            label="HybridCache Expiration"
            value={settings.cache.hybridCache.defaultExpiration}
          />
          <SettingRow label="HybridCache L2" value={settings.cache.hybridCache.l2Enabled} />
          <SettingRow label="Multi-Tier Cache" value={settings.cache.multiTier.enabled} />
          <SettingRow label="L1 TTL" value={settings.cache.multiTier.l1Ttl} />
          <SettingRow label="L2 TTL" value={settings.cache.multiTier.l2Ttl} />
        </SettingsCard>

        {/* Reranker */}
        <SettingsCard title="Reranker Service" icon={BoxIcon}>
          <SettingRow label="Configured" value={settings.reranker.configured} />
          {settings.reranker.url && <SettingRow label="URL" value={settings.reranker.url} />}
          {!settings.reranker.configured && (
            <p className="text-xs text-amber-600 dark:text-amber-400 mt-2">
              Reranker not configured. Set RERANKER_URL to enable cross-encoder reranking.
            </p>
          )}
        </SettingsCard>

        {/* Storage */}
        <SettingsCard title="File Storage" icon={HardDriveIcon}>
          <SettingRow label="Provider" value={settings.storage.provider} />
          <p className="text-xs text-slate-500 dark:text-zinc-500 mt-2">
            {settings.storage.provider === 'local'
              ? 'Using local filesystem storage. Set STORAGE_PROVIDER=s3 for cloud storage.'
              : 'Using S3-compatible cloud storage.'}
          </p>
        </SettingsCard>
      </div>

      {/* RAG Enhancements */}
      <RagEnhancementsTab />

      {/* Danger Zone */}
      <div className="bg-red-50/70 dark:bg-red-900/20 backdrop-blur-md rounded-xl p-6 border-2 border-red-200 dark:border-red-800">
        <div className="flex items-center gap-2 mb-4">
          <AlertTriangleIcon className="w-5 h-5 text-red-600 dark:text-red-400" />
          <h2 className="font-quicksand text-xl font-bold text-red-900 dark:text-red-300">
            Danger Zone
          </h2>
        </div>
        <p className="text-sm text-red-700 dark:text-red-400 mb-4">
          These actions may affect system performance and data availability.
        </p>

        <div className="space-y-3">
          {/* Rebuild Index */}
          {!showRebuildConfirm ? (
            <div className="flex items-center justify-between">
              <div>
                <p className="text-sm font-medium text-red-900 dark:text-red-200">
                  Rebuild Vector Index
                </p>
                <p className="text-xs text-red-600 dark:text-red-400">
                  Triggers reindexing of all Qdrant collections. May temporarily affect search.
                </p>
              </div>
              <Button
                variant="destructive"
                size="sm"
                onClick={() => setShowRebuildConfirm(true)}
                disabled={rebuildIndexMutation.isPending}
              >
                <RefreshCwIcon className="h-4 w-4 mr-1" />
                Rebuild Index
              </Button>
            </div>
          ) : (
            <div className="bg-red-100 dark:bg-red-900/40 rounded-lg p-4">
              <p className="text-sm font-medium text-red-900 dark:text-red-200 mb-3">
                Are you sure you want to rebuild all vector indices?
              </p>
              <div className="flex gap-2">
                <Button
                  variant="destructive"
                  size="sm"
                  onClick={() => rebuildIndexMutation.mutate()}
                  disabled={rebuildIndexMutation.isPending}
                >
                  {rebuildIndexMutation.isPending ? (
                    <>
                      <RefreshCwIcon className="h-4 w-4 mr-1 animate-spin" />
                      Rebuilding...
                    </>
                  ) : (
                    'Yes, Rebuild'
                  )}
                </Button>
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => setShowRebuildConfirm(false)}
                  disabled={rebuildIndexMutation.isPending}
                >
                  Cancel
                </Button>
              </div>
              {rebuildIndexMutation.isSuccess && (
                <p className="text-xs text-green-600 dark:text-green-400 mt-2">
                  Rebuild triggered successfully.
                </p>
              )}
              {rebuildIndexMutation.isError && (
                <p className="text-xs text-red-600 dark:text-red-400 mt-2">
                  Failed to trigger rebuild. Please try again.
                </p>
              )}
            </div>
          )}

          <div className="border-t border-red-200 dark:border-red-800" />

          {/* Clear Cache */}
          {!showClearConfirm ? (
            <div className="flex items-center justify-between">
              <div>
                <p className="text-sm font-medium text-red-900 dark:text-red-200">Clear KB Cache</p>
                <p className="text-xs text-red-600 dark:text-red-400">
                  Signals a cache reset. Cached entries will expire based on their TTL.
                </p>
              </div>
              <Button
                variant="outline"
                size="sm"
                className="border-red-300 text-red-700 hover:bg-red-50 dark:border-red-800 dark:text-red-400 dark:hover:bg-red-900/20"
                onClick={() => setShowClearConfirm(true)}
                disabled={clearCacheMutation.isPending}
              >
                <Trash2Icon className="h-4 w-4 mr-1" />
                Clear Cache
              </Button>
            </div>
          ) : (
            <div className="bg-red-100 dark:bg-red-900/40 rounded-lg p-4">
              <p className="text-sm font-medium text-red-900 dark:text-red-200 mb-3">
                Are you sure you want to clear the KB cache?
              </p>
              <div className="flex gap-2">
                <Button
                  variant="destructive"
                  size="sm"
                  onClick={() => clearCacheMutation.mutate()}
                  disabled={clearCacheMutation.isPending}
                >
                  {clearCacheMutation.isPending ? (
                    <>
                      <RefreshCwIcon className="h-4 w-4 mr-1 animate-spin" />
                      Clearing...
                    </>
                  ) : (
                    'Yes, Clear Cache'
                  )}
                </Button>
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => setShowClearConfirm(false)}
                  disabled={clearCacheMutation.isPending}
                >
                  Cancel
                </Button>
              </div>
              {clearCacheMutation.isSuccess && (
                <p className="text-xs text-green-600 dark:text-green-400 mt-2">
                  {clearCacheMutation.data?.message}
                </p>
              )}
              {clearCacheMutation.isError && (
                <p className="text-xs text-red-600 dark:text-red-400 mt-2">
                  Failed to clear cache. Please try again.
                </p>
              )}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
