/**
 * PDF-05: Upload queue component
 * Displays the list of files being uploaded with aggregate progress
 */

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Progress } from '@/components/ui/progress';
import type {
  UploadQueueItem as UploadQueueItemType,
  UploadQueueStats,
} from '@/hooks/useUploadQueue';

import { UploadQueueItem } from './UploadQueueItem';

interface UploadQueueProps {
  items: UploadQueueItemType[];
  stats: UploadQueueStats;
  onCancel: (id: string) => void;
  onRetry: (id: string) => void;
  onRemove: (id: string) => void;
  onClearCompleted: () => void;
}

export function UploadQueue({
  items,
  stats,
  onCancel,
  onRetry,
  onRemove,
  onClearCompleted,
}: UploadQueueProps) {
  if (items.length === 0) {
    return (
      <div className="p-8 text-center text-gray-600 text-sm border-2 border-dashed border-gray-300 rounded-lg bg-gray-50">
        No files in queue. Select files to begin uploading.
      </div>
    );
  }

  const totalProgress =
    items.length > 0
      ? Math.round(items.reduce((sum, item) => sum + item.progress, 0) / items.length)
      : 0;

  const activeCount = stats.uploading + stats.processing;
  const completedCount = stats.succeeded + stats.failed + stats.cancelled;
  const hasCompleted = completedCount > 0;

  return (
    <div data-testid="upload-queue">
      {/* Aggregate Progress Header */}
      <div className="p-4 bg-gray-50 rounded-md mb-4 border border-gray-300">
        <div className="flex justify-between items-center mb-3">
          <div>
            <div className="text-base font-semibold text-gray-900">Upload Queue</div>
            <div className="text-xs text-gray-600 mt-1">
              {activeCount > 0 ? (
                <>
                  Uploading {activeCount} of {stats.total} files ({totalProgress}% total)
                </>
              ) : stats.pending > 0 ? (
                <>{stats.pending} files waiting to upload</>
              ) : (
                <>All uploads complete</>
              )}
            </div>
          </div>
          {hasCompleted && (
            <Button
              onClick={onClearCompleted}
              variant="outline"
              size="sm"
              aria-label="Clear completed uploads from queue"
            >
              Clear Completed
            </Button>
          )}
        </div>

        {/* Overall Progress Bar */}
        <Progress value={totalProgress} className="h-2.5" aria-label="Overall upload progress" />

        {/* Stats Summary */}
        <div className="flex gap-2 mt-3 flex-wrap">
          {stats.pending > 0 && <Badge variant="secondary">{stats.pending} pending</Badge>}
          {stats.uploading > 0 && <Badge variant="default">{stats.uploading} uploading</Badge>}
          {stats.processing > 0 && (
            <Badge variant="secondary" className="bg-orange-100 text-orange-600">
              {stats.processing} processing
            </Badge>
          )}
          {stats.succeeded > 0 && (
            <Badge variant="default" className="bg-green-100 text-green-600">
              {stats.succeeded} succeeded
            </Badge>
          )}
          {stats.failed > 0 && <Badge variant="destructive">{stats.failed} failed</Badge>}
          {stats.cancelled > 0 && <Badge variant="outline">{stats.cancelled} cancelled</Badge>}
        </div>
      </div>

      {/* Queue Items */}
      <div role="list" aria-label="Upload queue items">
        {items.map(item => (
          <div key={item.id} role="listitem">
            <UploadQueueItem
              item={item}
              onCancel={onCancel}
              onRetry={onRetry}
              onRemove={onRemove}
            />
          </div>
        ))}
      </div>
    </div>
  );
}
