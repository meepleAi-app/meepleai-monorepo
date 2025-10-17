/**
 * PDF-05: Upload queue component
 * Displays the list of files being uploaded with aggregate progress
 */

import type { UploadQueueItem as UploadQueueItemType, UploadQueueStats } from '../hooks/useUploadQueue';
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
            <button
              onClick={onClearCompleted}
              aria-label="Clear completed uploads from queue"
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
              Clear Completed
            </button>
          )}
        </div>

        {/* Overall Progress Bar */}
        <div
          role="progressbar"
          aria-label="Overall upload progress"
          aria-valuenow={totalProgress}
          aria-valuemin={0}
          aria-valuemax={100}
          style={{
            width: '100%',
            height: '10px',
            backgroundColor: '#e0e0e0',
            borderRadius: '5px',
            overflow: 'hidden'
          }}
        >
          <div
            style={{
              width: `${totalProgress}%`,
              height: '100%',
              backgroundColor: '#0070f3',
              transition: 'width 0.3s ease'
            }}
          />
        </div>

        {/* Stats Summary */}
        <div style={{ display: 'flex', gap: '16px', marginTop: '12px', fontSize: '13px', flexWrap: 'wrap' }}>
          {stats.pending > 0 && (
            <div style={{ color: '#666' }}>
              <span style={{ fontWeight: 600 }}>{stats.pending}</span> pending
            </div>
          )}
          {stats.uploading > 0 && (
            <div style={{ color: '#0070f3' }}>
              <span style={{ fontWeight: 600 }}>{stats.uploading}</span> uploading
            </div>
          )}
          {stats.processing > 0 && (
            <div style={{ color: '#ff9800' }}>
              <span style={{ fontWeight: 600 }}>{stats.processing}</span> processing
            </div>
          )}
          {stats.succeeded > 0 && (
            <div style={{ color: '#34a853' }}>
              <span style={{ fontWeight: 600 }}>{stats.succeeded}</span> succeeded
            </div>
          )}
          {stats.failed > 0 && (
            <div style={{ color: '#d93025' }}>
              <span style={{ fontWeight: 600 }}>{stats.failed}</span> failed
            </div>
          )}
          {stats.cancelled > 0 && (
            <div style={{ color: '#999' }}>
              <span style={{ fontWeight: 600 }}>{stats.cancelled}</span> cancelled
            </div>
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
