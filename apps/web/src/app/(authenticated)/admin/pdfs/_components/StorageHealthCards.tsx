'use client';

import { Database, HardDrive, Server, Activity } from 'lucide-react';

import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui';
import type { PdfStorageHealth } from '@/lib/api/clients/pdfClient';

interface StorageHealthCardsProps {
  data: PdfStorageHealth | undefined;
  isLoading: boolean;
}

function formatBytes(bytes: number): string {
  if (bytes === 0) return '0 B';
  const k = 1024;
  const sizes = ['B', 'KB', 'MB', 'GB', 'TB'];
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  return `${(bytes / Math.pow(k, i)).toFixed(1)} ${sizes[i]}`;
}

const healthColors: Record<string, string> = {
  healthy: 'text-green-600 bg-green-100 dark:bg-green-900/30 dark:text-green-400',
  warning: 'text-amber-600 bg-amber-100 dark:bg-amber-900/30 dark:text-amber-400',
  critical: 'text-red-600 bg-red-100 dark:bg-red-900/30 dark:text-red-400',
};

export function StorageHealthCards({ data, isLoading }: StorageHealthCardsProps) {
  if (isLoading) {
    return (
      <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-4 gap-4">
        {[...Array(4)].map((_, i) => (
          <Card key={i}>
            <CardHeader className="pb-2">
              <div className="h-4 w-24 bg-muted animate-pulse rounded" />
            </CardHeader>
            <CardContent>
              <div className="h-8 w-16 bg-muted animate-pulse rounded" />
            </CardContent>
          </Card>
        ))}
      </div>
    );
  }

  if (!data) return null;

  return (
    <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-4 gap-4">
      {/* PostgreSQL */}
      <Card>
        <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
          <CardTitle className="text-sm font-medium">PostgreSQL</CardTitle>
          <Database className="h-4 w-4 text-muted-foreground" />
        </CardHeader>
        <CardContent>
          <div className="text-2xl font-bold">{data.postgres.totalDocuments}</div>
          <p className="text-xs text-muted-foreground">
            {data.postgres.totalChunks.toLocaleString()} chunks
            {' · '}
            {data.postgres.estimatedChunksSizeMB.toFixed(1)} MB
          </p>
        </CardContent>
      </Card>

      {/* Qdrant */}
      <Card>
        <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
          <CardTitle className="text-sm font-medium">Qdrant Vectors</CardTitle>
          <Server className="h-4 w-4 text-muted-foreground" />
        </CardHeader>
        <CardContent>
          <div className="text-2xl font-bold">{data.qdrant.vectorCount.toLocaleString()}</div>
          <p className="text-xs text-muted-foreground">
            {data.qdrant.memoryFormatted}
            {' · '}
            <span className={data.qdrant.isAvailable ? 'text-green-600' : 'text-red-600'}>
              {data.qdrant.isAvailable ? 'Available' : 'Unavailable'}
            </span>
          </p>
        </CardContent>
      </Card>

      {/* File Storage */}
      <Card>
        <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
          <CardTitle className="text-sm font-medium">File Storage</CardTitle>
          <HardDrive className="h-4 w-4 text-muted-foreground" />
        </CardHeader>
        <CardContent>
          <div className="text-2xl font-bold">{data.fileStorage.totalFiles}</div>
          <p className="text-xs text-muted-foreground">
            {data.fileStorage.totalSizeFormatted} total
          </p>
        </CardContent>
      </Card>

      {/* Overall Health */}
      <Card>
        <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
          <CardTitle className="text-sm font-medium">System Health</CardTitle>
          <Activity className="h-4 w-4 text-muted-foreground" />
        </CardHeader>
        <CardContent>
          <div className="flex items-center gap-2">
            <span
              className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ${healthColors[data.overallHealth] || healthColors.critical}`}
            >
              {data.overallHealth.charAt(0).toUpperCase() + data.overallHealth.slice(1)}
            </span>
          </div>
          <p className="text-xs text-muted-foreground mt-1">
            {new Date(data.measuredAt).toLocaleTimeString()}
          </p>
        </CardContent>
      </Card>
    </div>
  );
}
