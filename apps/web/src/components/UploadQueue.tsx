/**
 * PDF-05: Upload queue component
 * Displays the list of files being uploaded with aggregate progress
 */

import type { UploadQueueItem as UploadQueueItemType, UploadQueueStats } from '../hooks/useUploadQueue';
import { UploadQueueItem } from './UploadQueueItem';
import { Button } from '@/components/ui/button';
import { Progress } from '@/components/ui/progress';
import { Badge } from '@/components/ui/badge';

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
  onClearCompleted
}: UploadQueueProps) {
  if (items.length === 0) {
    return (
      <div
        style={{
          padding: '32px',
          textAlign: 'center',
          color: '#5f6368',
          fontSize: '14px',
          border: '2px dashed #dadce0',
          borderRadius: '8px',
          backgroundColor: '#fafafa'
        }}
      >
        No files in queue. Select files to begin uploading.
      </div>
    );
  }

  const totalProgress = items.length > 0
    ? Math.round(items.reduce((sum, item) => sum + item.progress, 0) / items.length)
    : 0;

  const activeCount = stats.uploading + stats.processing;
  const completedCount = stats.succeeded + stats.failed + stats.cancelled;
  const hasCompleted = completedCount > 0;

  return (
    <div data-testid="upload-queue">
      {/* Aggregate Progress Header */}
      <div
        style={{
          padding: '16px',
          backgroundColor: '#f8f9fa',
          borderRadius: '6px',
          marginBottom: '16px',
          border: '1px solid #e0e0e0'
        }}
      >
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '12px' }}>
          <div>
            <div style={{ fontSize: '16px', fontWeight: 600, color: '#202124' }}>
              Upload Queue
            </div>
            <div style={{ fontSize: '13px', color: '#5f6368', marginTop: '4px' }}>
              {activeCount > 0 ? (
                <>
                  Uploading {activeCount} of {stats.total} files ({totalProgress}% total)
                </>
              ) : stats.pending > 0 ? (
                <>
                  {stats.pending} files waiting to upload
                </>
              ) : (
                <>
                  All uploads complete
                </>
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
        <Progress
          value={totalProgress}
          className="h-2.5"
          aria-label="Overall upload progress"
        />

        {/* Stats Summary */}
        <div style={{ display: 'flex', gap: '8px', marginTop: '12px', flexWrap: 'wrap' }}>
          {stats.pending > 0 && (
            <Badge variant="secondary">
              {stats.pending} pending
            </Badge>
          )}
          {stats.uploading > 0 && (
            <Badge variant="default">
              {stats.uploading} uploading
            </Badge>
          )}
          {stats.processing > 0 && (
            <Badge variant="secondary" style={{ backgroundColor: '#fff3e0', color: '#ff9800' }}>
              {stats.processing} processing
            </Badge>
          )}
          {stats.succeeded > 0 && (
            <Badge variant="default" style={{ backgroundColor: '#e8f5e9', color: '#34a853' }}>
              {stats.succeeded} succeeded
            </Badge>
          )}
          {stats.failed > 0 && (
            <Badge variant="destructive">
              {stats.failed} failed
            </Badge>
          )}
          {stats.cancelled > 0 && (
            <Badge variant="outline">
              {stats.cancelled} cancelled
            </Badge>
          )}
        </div>
      </div>

      {/* Queue Items */}
      <div role="list" aria-label="Upload queue items">
        {items.map((item) => (
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
