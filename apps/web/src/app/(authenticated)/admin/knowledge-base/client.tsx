'use client';

/**
 * Admin Knowledge Base Overview Client
 * Issue #4892 - Shows storage health, vector collections, and links to sub-sections
 */

import { useCallback, useEffect, useState } from 'react';

import { Activity, Database, FileText, HardDrive, Layers, RefreshCw } from 'lucide-react';
import Link from 'next/link';

import { AdminAuthGuard } from '@/components/admin/AdminAuthGuard';
import { useAuthUser } from '@/components/auth/AuthProvider';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';
import { api } from '@/lib/api';
import type { PdfStorageHealth, VectorCollection } from '@/lib/api/schemas';

// ─── Types ────────────────────────────────────────────────────────────────────

type ToastMessage = {
  id: string;
  type: 'success' | 'error' | 'info';
  message: string;
};

// ─── Helpers ──────────────────────────────────────────────────────────────────

function formatBytes(bytes: number): string {
  if (bytes === 0) return '0 B';
  const k = 1024;
  const sizes = ['B', 'KB', 'MB', 'GB'];
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  return `${parseFloat((bytes / Math.pow(k, i)).toFixed(1))} ${sizes[i]}`;
}

function healthColor(health: number): string {
  if (health >= 0.9) return 'text-green-600';
  if (health >= 0.7) return 'text-amber-500';
  return 'text-red-500';
}

// ─── Sub-components ───────────────────────────────────────────────────────────

function StorageSkeleton() {
  return (
    <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
      {[1, 2, 3].map((i) => (
        <Card key={i}>
          <CardHeader className="pb-2">
            <Skeleton className="h-4 w-32" />
          </CardHeader>
          <CardContent className="space-y-2">
            <Skeleton className="h-6 w-20" />
            <Skeleton className="h-4 w-full" />
            <Skeleton className="h-4 w-3/4" />
          </CardContent>
        </Card>
      ))}
    </div>
  );
}

function StorageHealthCards({ health }: { health: PdfStorageHealth }) {
  return (
    <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
      {/* PostgreSQL */}
      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-sm font-medium flex items-center gap-2">
            <Database className="h-4 w-4 text-blue-500" />
            PostgreSQL
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-1 text-sm">
          <div className="flex justify-between">
            <span className="text-muted-foreground">Documents</span>
            <span className="font-medium">{health.postgres.totalDocuments.toLocaleString()}</span>
          </div>
          <div className="flex justify-between">
            <span className="text-muted-foreground">Chunks</span>
            <span className="font-medium">{health.postgres.totalChunks.toLocaleString()}</span>
          </div>
          <div className="flex justify-between">
            <span className="text-muted-foreground">Size</span>
            <span className="font-medium">
              {health.postgres.estimatedChunksSizeMB.toFixed(1)} MB
            </span>
          </div>
        </CardContent>
      </Card>

      {/* Qdrant */}
      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-sm font-medium flex items-center gap-2">
            <Layers className="h-4 w-4 text-purple-500" />
            Qdrant
            <span
              className={`text-xs ${health.qdrant.isAvailable ? 'text-green-600' : 'text-red-500'}`}
            >
              {health.qdrant.isAvailable ? '● Online' : '● Offline'}
            </span>
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-1 text-sm">
          <div className="flex justify-between">
            <span className="text-muted-foreground">Vectors</span>
            <span className="font-medium">{health.qdrant.vectorCount.toLocaleString()}</span>
          </div>
          <div className="flex justify-between">
            <span className="text-muted-foreground">Memory</span>
            <span className="font-medium">{health.qdrant.memoryFormatted}</span>
          </div>
        </CardContent>
      </Card>

      {/* File Storage */}
      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-sm font-medium flex items-center gap-2">
            <HardDrive className="h-4 w-4 text-orange-500" />
            File Storage
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-1 text-sm">
          <div className="flex justify-between">
            <span className="text-muted-foreground">Files</span>
            <span className="font-medium">{health.fileStorage.totalFiles.toLocaleString()}</span>
          </div>
          <div className="flex justify-between">
            <span className="text-muted-foreground">Total Size</span>
            <span className="font-medium">{health.fileStorage.totalSizeFormatted}</span>
          </div>
          <div className="flex justify-between">
            <span className="text-muted-foreground">Health</span>
            <span className={`font-medium capitalize ${health.overallHealth === 'Healthy' ? 'text-green-600' : 'text-amber-500'}`}>
              {health.overallHealth}
            </span>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}

function VectorCollectionsTable({ collections }: { collections: VectorCollection[] }) {
  if (collections.length === 0) {
    return (
      <p className="text-sm text-muted-foreground py-4 text-center">
        No vector collections found.
      </p>
    );
  }

  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b">
            <th className="text-left py-2 pr-4 font-medium text-muted-foreground">Name</th>
            <th className="text-right py-2 pr-4 font-medium text-muted-foreground">Vectors</th>
            <th className="text-right py-2 pr-4 font-medium text-muted-foreground">Dimensions</th>
            <th className="text-left py-2 pr-4 font-medium text-muted-foreground">Storage</th>
            <th className="text-right py-2 font-medium text-muted-foreground">Health</th>
          </tr>
        </thead>
        <tbody>
          {collections.map((col) => (
            <tr key={col.name} className="border-b last:border-0">
              <td className="py-2 pr-4 font-mono text-xs">{col.name}</td>
              <td className="py-2 pr-4 text-right">{col.vectorCount.toLocaleString()}</td>
              <td className="py-2 pr-4 text-right">{col.dimensions}</td>
              <td className="py-2 pr-4 text-muted-foreground">{col.storage}</td>
              <td className={`py-2 text-right font-medium ${healthColor(col.health)}`}>
                {(col.health * 100).toFixed(0)}%
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

// ─── Main Component ───────────────────────────────────────────────────────────

export function KnowledgeBaseOverviewClient() {
  const { user, loading: authLoading } = useAuthUser();

  const [storageHealth, setStorageHealth] = useState<PdfStorageHealth | null>(null);
  const [collections, setCollections] = useState<VectorCollection[]>([]);
  const [dataLoading, setDataLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [toasts, setToasts] = useState<ToastMessage[]>([]);

  const addToast = useCallback((type: 'success' | 'error' | 'info', message: string) => {
    const id = `toast-${Date.now()}-${Math.random()}`;
    setToasts((prev) => [...prev, { id, type, message }]);
    setTimeout(() => {
      setToasts((prev) => prev.filter((t) => t.id !== id));
    }, 5000);
  }, []);

  const fetchData = useCallback(async () => {
    setDataLoading(true);
    setError(null);
    try {
      const [health, vecs] = await Promise.all([
        api.admin.getPdfStorageHealth(),
        api.admin.getVectorCollections(),
      ]);
      setStorageHealth(health);
      setCollections(vecs.collections);
    } catch (err) {
      const msg = err instanceof Error ? err.message : 'Failed to load KB overview';
      setError(msg);
      addToast('error', msg);
    } finally {
      setDataLoading(false);
    }
  }, [addToast]);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  return (
    <AdminAuthGuard user={user} loading={authLoading}>
      {/* Toast container */}
      <div className="fixed top-4 right-4 z-50 flex flex-col gap-2">
        {toasts.map((toast) => (
          <div
            key={toast.id}
            className={`px-4 py-3 rounded-lg shadow-lg text-sm font-medium text-white ${
              toast.type === 'success'
                ? 'bg-green-600'
                : toast.type === 'error'
                  ? 'bg-red-600'
                  : 'bg-blue-600'
            }`}
          >
            {toast.message}
          </div>
        ))}
      </div>

      <div className="space-y-6">
        {/* Header */}
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-2xl font-bold font-quicksand">Knowledge Base</h1>
            <p className="text-muted-foreground text-sm font-nunito">
              Storage health, vector collections, and RAG pipeline management
            </p>
          </div>
          <Button
            variant="outline"
            size="sm"
            onClick={fetchData}
            disabled={dataLoading}
            className="font-nunito"
          >
            <RefreshCw className={`h-4 w-4 mr-2 ${dataLoading ? 'animate-spin' : ''}`} />
            Refresh
          </Button>
        </div>

        {/* Quick Links */}
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <Link href="/admin/pdfs">
            <Card className="hover:border-primary/50 transition-colors cursor-pointer">
              <CardContent className="flex items-center gap-4 py-4">
                <div className="rounded-lg bg-blue-100 p-3">
                  <FileText className="h-5 w-5 text-blue-600" />
                </div>
                <div>
                  <p className="font-medium font-nunito">Documents</p>
                  <p className="text-sm text-muted-foreground">
                    Manage PDF documents and reindex
                  </p>
                </div>
              </CardContent>
            </Card>
          </Link>
          <Link href="/admin/knowledge-base/queue">
            <Card className="hover:border-primary/50 transition-colors cursor-pointer">
              <CardContent className="flex items-center gap-4 py-4">
                <div className="rounded-lg bg-amber-100 p-3">
                  <Activity className="h-5 w-5 text-amber-600" />
                </div>
                <div>
                  <p className="font-medium font-nunito">Processing Queue</p>
                  <p className="text-sm text-muted-foreground">
                    Monitor document processing jobs
                  </p>
                </div>
              </CardContent>
            </Card>
          </Link>
        </div>

        {/* Error */}
        {error && !dataLoading && (
          <Alert variant="destructive">
            <AlertDescription>{error}</AlertDescription>
          </Alert>
        )}

        {/* Storage Health */}
        <div>
          <h2 className="text-lg font-semibold font-quicksand mb-3">Storage Health</h2>
          {dataLoading ? (
            <StorageSkeleton />
          ) : storageHealth ? (
            <StorageHealthCards health={storageHealth} />
          ) : null}
        </div>

        {/* Vector Collections */}
        <Card>
          <CardHeader>
            <CardTitle className="text-base font-semibold flex items-center gap-2">
              <Layers className="h-4 w-4" />
              Vector Collections
            </CardTitle>
          </CardHeader>
          <CardContent>
            {dataLoading ? (
              <div className="space-y-2">
                {[1, 2].map((i) => (
                  <Skeleton key={i} className="h-8 w-full" />
                ))}
              </div>
            ) : (
              <VectorCollectionsTable collections={collections} />
            )}
          </CardContent>
        </Card>
      </div>
    </AdminAuthGuard>
  );
}
