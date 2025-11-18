/**
 * PDF-05: Upload queue item component
 * Displays a single file in the upload queue with progress bar and actions
 */

import type { UploadQueueItem as UploadQueueItemType, UploadStatus } from '@/hooks/useUploadQueue';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Progress } from '@/components/ui/progress';

interface UploadQueueItemProps {
  item: UploadQueueItemType;
  onCancel: (id: string) => void;
  onRetry: (id: string) => void;
  onRemove: (id: string) => void;
}

const STATUS_LABELS: Record<UploadStatus, string> = {
  pending: 'Pending',
  uploading: 'Uploading',
  processing: 'Processing',
  success: 'Success',
  failed: 'Failed',
  cancelled: 'Cancelled'
};

const STATUS_COLORS: Record<UploadStatus, string> = {
  pending: '#666',
  uploading: '#0070f3',
  processing: '#ff9800',
  success: '#34a853',
  failed: '#d93025',
  cancelled: '#999'
};

const STATUS_BG_COLORS: Record<UploadStatus, string> = {
  pending: '#f5f5f5',
  uploading: '#e3f2fd',
  processing: '#fff3e0',
  success: '#e8f5e9',
  failed: '#ffebee',
  cancelled: '#fafafa'
};

function formatFileSize(bytes: number): string {
  if (bytes < 1024) {
    return `${bytes} B`;
  }
  if (bytes < 1024 * 1024) {
    return `${(bytes / 1024).toFixed(1)} KB`;
  }
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

export function UploadQueueItem({ item, onCancel, onRetry, onRemove }: UploadQueueItemProps) {
  const { id, file, status, progress, error, retryCount, correlationId } = item;

  const statusLabel = STATUS_LABELS[status];
  const statusColor = STATUS_COLORS[status];
  const statusBgColor = STATUS_BG_COLORS[status];

  const showProgressBar = status === 'uploading' || status === 'processing';
  const showCancelButton = status === 'uploading' || status === 'processing';
  const showRetryButton = status === 'failed';
  const showRemoveButton = status === 'pending' || status === 'cancelled';

  return (
    <div
      data-testid={`upload-queue-item-${id}`}
      className="p-4 border border-gray-300 rounded-md mb-3 transition-colors duration-300"
      style={{
        backgroundColor: statusBgColor
      }}
    >
      {/* Header: Filename and Status */}
      <div className="flex justify-between items-start mb-2">
        <div className="flex-1 min-w-0">
          <div
            className="text-sm font-semibold text-gray-900 overflow-hidden text-ellipsis whitespace-nowrap"
            title={file.name}
          >
            {file.name}
          </div>
          <div className="text-xs text-gray-600 mt-0.5">
            {formatFileSize(file.size)}
            {retryCount > 0 && ` • Retry ${retryCount}`}
          </div>
        </div>
        <Badge
          variant={status === 'success' ? 'default' : status === 'failed' ? 'destructive' : 'secondary'}
          role="status"
          aria-live="polite"
          aria-label={`Upload status: ${statusLabel}`}
          style={{ color: statusColor, borderColor: statusColor }}
        >
          {statusLabel}
        </Badge>
      </div>

      {/* Progress Bar */}
      {showProgressBar && (
        <>
          <Progress
            value={progress}
            className="h-2 mb-2"
            aria-label={`Upload progress for ${file.name}`}
          />
          <div className="text-xs text-gray-600 mb-2">
            {progress}% complete
          </div>
        </>
      )}

      {/* Error Message */}
      {status === 'failed' && error && (
        <div
          role="alert"
          data-testid={`upload-error-${id}`}
          className="text-xs text-red-600 p-2 bg-white border border-red-600 rounded mb-2"
        >
          <div>Error: {error}</div>
          {correlationId && (
            <div className="mt-1 text-[11px]">
              Error ID: {correlationId}
            </div>
          )}
        </div>
      )}

      {/* Action Buttons */}
      <div className="flex gap-2 mt-3">
        {showCancelButton && (
          <Button
            onClick={() => onCancel(id)}
            variant="destructive"
            size="sm"
            aria-label={`Cancel upload of ${file.name}`}
          >
            Cancel
          </Button>
        )}

        {showRetryButton && (
          <Button
            onClick={() => onRetry(id)}
            size="sm"
            aria-label={`Retry upload of ${file.name}`}
          >
            Retry
          </Button>
        )}

        {showRemoveButton && (
          <Button
            onClick={() => onRemove(id)}
            variant="outline"
            size="sm"
            aria-label={`Remove ${file.name} from queue`}
          >
            Remove
          </Button>
        )}
      </div>
    </div>
  );
}