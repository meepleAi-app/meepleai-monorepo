/**
 * PDF-05: Upload queue item component
 * Displays a single file in the upload queue with progress bar and actions
 */

import type { UploadQueueItem as UploadQueueItemType, UploadStatus } from '../hooks/useUploadQueue';

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
  const { id, file, status, progress, error, retryCount } = item;

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
      style={{
        padding: '16px',
        border: '1px solid #e0e0e0',
        borderRadius: '6px',
        backgroundColor: statusBgColor,
        marginBottom: '12px',
        transition: 'background-color 0.3s ease'
      }}
    >
      {/* Header: Filename and Status */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: '8px' }}>
        <div style={{ flex: 1, minWidth: 0 }}>
          <div
            style={{
              fontSize: '15px',
              fontWeight: 600,
              color: '#202124',
              overflow: 'hidden',
              textOverflow: 'ellipsis',
              whiteSpace: 'nowrap'
            }}
            title={file.name}
          >
            {file.name}
          </div>
          <div style={{ fontSize: '13px', color: '#5f6368', marginTop: '2px' }}>
            {formatFileSize(file.size)}
            {retryCount > 0 && ` • Retry ${retryCount}`}
          </div>
        </div>
        <div
          role="status"
          aria-live="polite"
          aria-label={`Upload status: ${statusLabel}`}
          style={{
            fontSize: '13px',
            fontWeight: 600,
            color: statusColor,
            padding: '4px 12px',
            borderRadius: '12px',
            backgroundColor: 'white',
            border: `1px solid ${statusColor}`,
            whiteSpace: 'nowrap',
            marginLeft: '12px'
          }}
        >
          {statusLabel}
        </div>
      </div>

      {/* Progress Bar */}
      {showProgressBar && (
        <div
          role="progressbar"
          aria-label={`Upload progress for ${file.name}`}
          aria-valuenow={progress}
          aria-valuemin={0}
          aria-valuemax={100}
          style={{
            width: '100%',
            height: '8px',
            backgroundColor: '#e0e0e0',
            borderRadius: '4px',
            overflow: 'hidden',
            marginBottom: '8px'
          }}
        >
          <div
            style={{
              width: `${progress}%`,
              height: '100%',
              backgroundColor: statusColor,
              transition: 'width 0.3s ease'
            }}
          />
        </div>
      )}

      {/* Progress Percentage */}
      {showProgressBar && (
        <div style={{ fontSize: '13px', color: '#5f6368', marginBottom: '8px' }}>
          {progress}% complete
        </div>
      )}

      {/* Error Message */}
      {status === 'failed' && error && (
        <div
          role="alert"
          style={{
            fontSize: '13px',
            color: '#d93025',
            padding: '8px 12px',
            backgroundColor: 'white',
            border: '1px solid #d93025',
            borderRadius: '4px',
            marginBottom: '8px'
          }}
        >
          Error: {error}
        </div>
      )}

      {/* Action Buttons */}
      <div style={{ display: 'flex', gap: '8px', marginTop: '12px' }}>
        {showCancelButton && (
          <button
            onClick={() => onCancel(id)}
            aria-label={`Cancel upload of ${file.name}`}
            style={{
              padding: '6px 14px',
              fontSize: '13px',
              fontWeight: 500,
              color: '#d93025',
              backgroundColor: 'white',
              border: '1px solid #d93025',
              borderRadius: '4px',
              cursor: 'pointer',
              transition: 'background-color 0.2s ease'
            }}
            onMouseEnter={(e) => {
              e.currentTarget.style.backgroundColor = '#ffebee';
            }}
            onMouseLeave={(e) => {
              e.currentTarget.style.backgroundColor = 'white';
            }}
          >
            Cancel
          </button>
        )}

        {showRetryButton && (
          <button
            onClick={() => onRetry(id)}
            aria-label={`Retry upload of ${file.name}`}
            style={{
              padding: '6px 14px',
              fontSize: '13px',
              fontWeight: 500,
              color: 'white',
              backgroundColor: '#0070f3',
              border: 'none',
              borderRadius: '4px',
              cursor: 'pointer',
              transition: 'background-color 0.2s ease'
            }}
            onMouseEnter={(e) => {
              e.currentTarget.style.backgroundColor = '#0051cc';
            }}
            onMouseLeave={(e) => {
              e.currentTarget.style.backgroundColor = '#0070f3';
            }}
          >
            Retry
          </button>
        )}

        {showRemoveButton && (
          <button
            onClick={() => onRemove(id)}
            aria-label={`Remove ${file.name} from queue`}
            style={{
              padding: '6px 14px',
              fontSize: '13px',
              fontWeight: 500,
              color: '#5f6368',
              backgroundColor: 'white',
              border: '1px solid #dadce0',
              borderRadius: '4px',
              cursor: 'pointer',
              transition: 'background-color 0.2s ease'
            }}
            onMouseEnter={(e) => {
              e.currentTarget.style.backgroundColor = '#f5f5f5';
            }}
            onMouseLeave={(e) => {
              e.currentTarget.style.backgroundColor = 'white';
            }}
          >
            Remove
          </button>
        )}
      </div>
    </div>
  );
}
