'use client';

import { ChevronDown, ChevronRight } from 'lucide-react';
import { useState } from 'react';

import { useQuery } from '@tanstack/react-query';

import { useApiClient } from '@/lib/api/context';

const STATE_COLORS: Record<string, string> = {
  Pending: 'bg-gray-400',
  Uploading: 'bg-blue-400',
  Extracting: 'bg-purple-400',
  Chunking: 'bg-indigo-400',
  Embedding: 'bg-violet-400',
  Indexing: 'bg-fuchsia-400',
  Ready: 'bg-green-500',
  Failed: 'bg-red-500',
};

function formatFileSize(bytes: number): string {
  if (bytes === 0) return '0 B';
  const k = 1024;
  const sizes = ['B', 'KB', 'MB', 'GB'];
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  return `${(bytes / Math.pow(k, i)).toFixed(1)} ${sizes[i]}`;
}

export function PdfMiniAnalytics() {
  const [isOpen, setIsOpen] = useState(false);
  const apiClient = useApiClient();

  const { data } = useQuery({
    queryKey: ['admin', 'pdf-status-distribution'],
    queryFn: () => apiClient.pdf.getStatusDistribution(),
    enabled: isOpen,
    refetchInterval: isOpen ? 30_000 : false,
  });

  if (!data && !isOpen) {
    return (
      <button
        onClick={() => setIsOpen(true)}
        className="flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground transition-colors"
      >
        <ChevronRight className="h-4 w-4" />
        Show Analytics
      </button>
    );
  }

  return (
    <div className="space-y-3">
      <button
        onClick={() => setIsOpen(!isOpen)}
        className="flex items-center gap-1 text-sm font-medium hover:text-foreground transition-colors"
      >
        {isOpen ? <ChevronDown className="h-4 w-4" /> : <ChevronRight className="h-4 w-4" />}
        Analytics
      </button>

      {isOpen && data && (
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4 pl-5">
          {/* Status Distribution */}
          <div className="space-y-2">
            <h4 className="text-xs font-medium text-muted-foreground">Status Distribution</h4>
            <div className="space-y-1.5">
              {Object.entries(data.countByState).map(([state, count]) => {
                const percent = data.totalDocuments > 0 ? (count / data.totalDocuments) * 100 : 0;
                return (
                  <div key={state} className="flex items-center gap-2">
                    <span className="text-xs w-20 text-muted-foreground">{state}</span>
                    <div className="flex-1 h-2 bg-muted rounded-full overflow-hidden">
                      <div
                        className={`h-full rounded-full ${STATE_COLORS[state] || 'bg-gray-300'}`}
                        style={{ width: `${Math.max(percent, 1)}%` }}
                      />
                    </div>
                    <span className="text-xs text-muted-foreground w-8 text-right">{count}</span>
                  </div>
                );
              })}
            </div>
          </div>

          {/* Top 5 by Size */}
          <div className="space-y-2">
            <h4 className="text-xs font-medium text-muted-foreground">Top 5 by Size</h4>
            <div className="space-y-1">
              {data.topBySize.map((item, i) => (
                <div key={item.id} className="flex items-center justify-between text-xs">
                  <span className="truncate max-w-[200px] text-muted-foreground" title={item.fileName}>
                    {i + 1}. {item.fileName}
                  </span>
                  <span className="font-mono ml-2 whitespace-nowrap">
                    {formatFileSize(item.fileSizeBytes)}
                  </span>
                </div>
              ))}
              {data.topBySize.length === 0 && (
                <span className="text-xs text-muted-foreground">No documents</span>
              )}
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
