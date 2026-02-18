'use client';

import { FileIcon, CheckCircleIcon, LoaderIcon, XCircleIcon } from 'lucide-react';

import { Badge } from '@/components/ui/badge';

interface QueueItem {
  id: string;
  fileName: string;
  status: 'processing' | 'completed' | 'error';
  progress: number;
  size: string;
}

const MOCK_QUEUE: QueueItem[] = [
  {
    id: '1',
    fileName: 'catan-rulebook.pdf',
    status: 'processing',
    progress: 65,
    size: '2.4 MB',
  },
  {
    id: '2',
    fileName: 'wingspan-strategy-guide.pdf',
    status: 'completed',
    progress: 100,
    size: '1.8 MB',
  },
  {
    id: '3',
    fileName: 'pandemic-faq.docx',
    status: 'processing',
    progress: 35,
    size: '0.9 MB',
  },
  {
    id: '4',
    fileName: 'scythe-rules-v2.pdf',
    status: 'error',
    progress: 0,
    size: '3.1 MB',
  },
];

const statusConfig = {
  processing: {
    icon: LoaderIcon,
    badge: 'bg-blue-100 text-blue-900 dark:bg-blue-900/30 dark:text-blue-300',
    label: 'Processing',
  },
  completed: {
    icon: CheckCircleIcon,
    badge: 'bg-green-100 text-green-900 dark:bg-green-900/30 dark:text-green-300',
    label: 'Completed',
  },
  error: {
    icon: XCircleIcon,
    badge: 'bg-red-100 text-red-900 dark:bg-red-900/30 dark:text-red-300',
    label: 'Error',
  },
};

export function ProcessingQueue() {
  return (
    <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-xl p-6 border border-slate-200/50 dark:border-zinc-700/50">
      <h2 className="font-quicksand text-xl font-bold text-slate-900 dark:text-zinc-100 mb-4">
        Processing Queue
      </h2>

      <div className="space-y-4">
        {MOCK_QUEUE.map((item) => {
          const config = statusConfig[item.status];
          const StatusIcon = config.icon;

          return (
            <div
              key={item.id}
              className="p-4 bg-slate-50/50 dark:bg-zinc-900/50 rounded-lg border border-slate-200/50 dark:border-zinc-700/50"
            >
              <div className="flex items-center justify-between mb-2">
                <div className="flex items-center gap-3">
                  <FileIcon className="w-5 h-5 text-gray-600 dark:text-zinc-400" />
                  <div>
                    <div className="font-medium text-slate-900 dark:text-zinc-100 text-sm">
                      {item.fileName}
                    </div>
                    <div className="text-xs text-slate-500 dark:text-zinc-500">{item.size}</div>
                  </div>
                </div>
                <Badge variant="outline" className={config.badge}>
                  <StatusIcon className="w-3 h-3 mr-1" />
                  {config.label}
                </Badge>
              </div>

              {item.status === 'processing' && (
                <div className="space-y-1">
                  <div className="h-2 bg-gray-200 dark:bg-zinc-700 rounded-full overflow-hidden">
                    <div
                      className="h-full bg-gradient-to-r from-amber-500 to-amber-400 transition-all duration-300 relative overflow-hidden"
                      style={{ width: `${item.progress}%` }}
                    >
                      <div className="absolute inset-0 bg-gradient-to-r from-transparent via-white/30 to-transparent animate-shimmer" />
                    </div>
                  </div>
                  <div className="text-xs text-slate-600 dark:text-zinc-400 text-right">
                    {item.progress}%
                  </div>
                </div>
              )}
            </div>
          );
        })}
      </div>
    </div>
  );
}
