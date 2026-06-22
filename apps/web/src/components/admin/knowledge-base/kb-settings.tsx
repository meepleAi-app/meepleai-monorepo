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
    grpcPort?: string;
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
  const valueClass =
    typeof value === 'boolean'
      ? value
        ? 'text-entity-toolkit'
        : 'text-muted-foreground'
      : 'text-foreground';

  return (
    <div className="flex items-center justify-between py-1.5 border-b border-border/60 last:border-0">
      <span className="font-mono text-[9.5px] font-bold uppercase tracking-wide text-muted-foreground">
        {label}
      </span>
      <span className={`font-mono text-[11.5px] font-semibold ${valueClass}`}>{displayValue}</span>
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
    <div className="rounded-[10px] border border-border/60 bg-card overflow-hidden">
      <div className="flex items-center gap-2.5 border-b border-border/60 bg-background px-3.5 py-2.5">
        <Icon className="h-3.5 w-3.5 text-entity-kb shrink-0" />
        <h3 className="font-quicksand text-[13px] font-extrabold text-foreground">{title}</h3>
      </div>
      <div className="p-3.5 space-y-0">{children}</div>
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
      // Fetch vector stats to confirm pgvector index availability
      const stats = await adminClient.getVectorStats();
      return stats;
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
          <div key={i} className="h-48 rounded-[10px] bg-muted animate-pulse" />
        ))}
      </div>
    );
  }

  if (!settings) {
    return (
      <div className="rounded-[10px] border border-border/60 bg-card p-4">
        <p className="text-sm text-muted-foreground">Unable to load KB settings</p>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Info Banner */}
      <div className="flex items-start gap-3 rounded-[10px] border border-entity-chat/30 bg-entity-chat/12 p-4">
        <InfoIcon className="h-4 w-4 text-entity-chat mt-0.5 shrink-0" />
        <div className="flex-1 min-w-0">
          <p className="font-quicksand text-[13px] font-extrabold text-entity-chat">
            Read-only Configuration
          </p>
          <p className="mt-1 text-xs text-muted-foreground">
            These settings are configured via environment variables and config files. To modify
            them, update the server configuration and restart the API service.
          </p>
        </div>
        <div className="flex items-center gap-2 flex-shrink-0">
          <span className="inline-flex items-center gap-1 rounded-full bg-muted px-2 py-0.5 font-mono text-[10px] font-bold text-muted-foreground">
            read-only
          </span>
          <Button
            variant="outline"
            size="sm"
            onClick={() => refetch()}
            disabled={isRefetching}
            className="gap-1.5 h-7 px-2.5 text-[11px]"
          >
            <RefreshCwIcon className={`h-3.5 w-3.5 ${isRefetching ? 'animate-spin' : ''}`} />
            Refresh
          </Button>
        </div>
      </div>

      {/* Settings Cards Grid */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-3">
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
          {settings.vectorDatabase.grpcPort && (
            <SettingRow label="gRPC Port" value={settings.vectorDatabase.grpcPort} />
          )}
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
            <p className="text-xs text-muted-foreground mt-2">
              Reranker not configured. Set RERANKER_URL to enable cross-encoder reranking.
            </p>
          )}
        </SettingsCard>

        {/* Storage */}
        <SettingsCard title="File Storage" icon={HardDriveIcon}>
          <SettingRow label="Provider" value={settings.storage.provider} />
          <p className="text-xs text-muted-foreground mt-2">
            {settings.storage.provider === 'local'
              ? 'Using local filesystem storage. Set STORAGE_PROVIDER=s3 for cloud storage.'
              : 'Using S3-compatible cloud storage.'}
          </p>
        </SettingsCard>
      </div>

      {/* RAG Enhancements */}
      <RagEnhancementsTab />

      {/* Danger Zone */}
      <div className="rounded-[10px] border border-entity-event/40 bg-entity-event/5 overflow-hidden">
        {/* Danger Zone header */}
        <div className="flex items-center gap-2.5 border-b border-entity-event/25 px-4 py-3 bg-gradient-to-r from-entity-event/14 to-entity-event/4">
          <AlertTriangleIcon className="h-4 w-4 text-entity-event shrink-0" />
          <h2 className="font-quicksand text-[14px] font-extrabold text-entity-event">
            Danger Zone
          </h2>
          <span className="font-mono text-[10.5px] text-foreground ml-2">
            These actions may affect system performance and data availability.
          </span>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-2 divide-y md:divide-y-0 md:divide-x divide-entity-event/15">
          {/* Rebuild Index */}
          <div className="p-4 flex flex-col gap-3">
            <div className="flex items-start gap-3">
              <div className="h-8 w-8 rounded-lg bg-entity-event/14 flex items-center justify-center shrink-0">
                <RefreshCwIcon className="h-4 w-4 text-entity-event" />
              </div>
              <div>
                <p className="font-quicksand text-[13.5px] font-extrabold text-foreground">
                  Rebuild Vector Index
                </p>
                <p className="text-xs text-muted-foreground mt-0.5 leading-relaxed">
                  Triggers reindexing of all pgvector embeddings. May temporarily affect search.
                </p>
              </div>
            </div>

            {!showRebuildConfirm ? (
              <Button
                variant="destructive"
                size="sm"
                onClick={() => setShowRebuildConfirm(true)}
                disabled={rebuildIndexMutation.isPending}
                className="self-start gap-1.5"
              >
                <RefreshCwIcon className="h-4 w-4" />
                Rebuild Index
              </Button>
            ) : (
              <div className="rounded-lg border border-entity-event/25 bg-background p-3 flex flex-col gap-2">
                <p className="font-mono text-[11px] text-muted-foreground">
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
                  <p className="text-xs text-entity-toolkit mt-1">
                    Rebuild triggered successfully.
                  </p>
                )}
                {rebuildIndexMutation.isError && (
                  <p className="text-xs text-entity-event mt-1">
                    Failed to trigger rebuild. Please try again.
                  </p>
                )}
              </div>
            )}
          </div>

          {/* Clear Cache */}
          <div className="p-4 flex flex-col gap-3">
            <div className="flex items-start gap-3">
              <div className="h-8 w-8 rounded-lg bg-entity-event/14 flex items-center justify-center shrink-0">
                <Trash2Icon className="h-4 w-4 text-entity-event" />
              </div>
              <div>
                <p className="font-quicksand text-[13.5px] font-extrabold text-foreground">
                  Clear KB Cache
                </p>
                <p className="text-xs text-muted-foreground mt-0.5 leading-relaxed">
                  Signals a cache reset. Cached entries will expire based on their TTL.
                </p>
              </div>
            </div>

            {!showClearConfirm ? (
              <Button
                variant="outline"
                size="sm"
                className="self-start gap-1.5 border-entity-event/55 text-entity-event hover:bg-entity-event/8"
                onClick={() => setShowClearConfirm(true)}
                disabled={clearCacheMutation.isPending}
              >
                <Trash2Icon className="h-4 w-4" />
                Clear Cache
              </Button>
            ) : (
              <div className="rounded-lg border border-entity-event/25 bg-background p-3 flex flex-col gap-2">
                <p className="font-mono text-[11px] text-muted-foreground">
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
                  <p className="text-xs text-entity-toolkit mt-1">
                    {clearCacheMutation.data?.message}
                  </p>
                )}
                {clearCacheMutation.isError && (
                  <p className="text-xs text-entity-event mt-1">
                    Failed to clear cache. Please try again.
                  </p>
                )}
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
