/**
 * PDF-05: Upload summary component
 * Displays final statistics after all uploads complete
 */

import { Button } from '@/components/ui/button';
import type { UploadQueueStats } from '@/hooks/useUploadQueue';

interface UploadSummaryProps {
  stats: UploadQueueStats;
  onClose: () => void;
  onClearAll: () => void;
}

export function UploadSummary({ stats, onClose, onClearAll }: UploadSummaryProps) {
  const hasFailures = stats.failed > 0;
  const hasCancelled = stats.cancelled > 0;
  const allSucceeded = stats.succeeded === stats.total && stats.total > 0;

  const summaryIcon = allSucceeded ? '✅' : hasFailures ? '⚠️' : '✓';
  const summaryColor = allSucceeded ? '#34a853' : hasFailures ? '#ff9800' : '#0070f3';
  const summaryBgColor = allSucceeded ? '#e8f5e9' : hasFailures ? '#fff3e0' : '#e3f2fd';

  return (
    <div
      role="status"
      aria-live="polite"
      aria-label="Upload summary"
      data-testid="upload-summary"
      className="p-5 border-2 rounded-lg mb-5"
      style={{
        borderColor: summaryColor,
        backgroundColor: summaryBgColor,
      }}
    >
      {/* Header */}
      <div className="flex items-center mb-4">
        <div aria-hidden="true" className="text-[32px] mr-3 leading-none">
          {summaryIcon}
        </div>
        <div>
          <div className="text-lg font-semibold text-gray-800">Upload Complete</div>
          <div className="text-sm text-gray-600 mt-0.5">
            {allSucceeded
              ? `All ${stats.total} files uploaded successfully!`
              : `${stats.succeeded} succeeded, ${stats.failed} failed${hasCancelled ? `, ${stats.cancelled} cancelled` : ''}`}
          </div>
        </div>
      </div>

      {/* Detailed Stats */}
      <div
        className="grid gap-3 mb-4"
        style={{ gridTemplateColumns: 'repeat(auto-fit, minmax(120px, 1fr))' }}
      >
        <div className="text-center p-3 bg-white rounded-md">
          <div className="text-2xl font-bold text-gray-800">{stats.total}</div>
          <div className="text-sm text-gray-600 mt-1">Total</div>
        </div>

        <div className="text-center p-3 bg-white rounded-md">
          <div className="text-2xl font-bold text-green-600">{stats.succeeded}</div>
          <div className="text-sm text-gray-600 mt-1">Succeeded</div>
        </div>

        {stats.failed > 0 && (
          <div className="text-center p-3 bg-white rounded-md">
            <div className="text-2xl font-bold text-red-600">{stats.failed}</div>
            <div className="text-sm text-gray-600 mt-1">Failed</div>
          </div>
        )}

        {stats.cancelled > 0 && (
          <div className="text-center p-3 bg-white rounded-md">
            <div className="text-2xl font-bold text-gray-600">{stats.cancelled}</div>
            <div className="text-sm text-gray-600 mt-1">Cancelled</div>
          </div>
        )}
      </div>

      {/* Messages */}
      {hasFailures && (
        <div role="alert" className="p-3 bg-white border border-red-600 rounded-md mb-4">
          <div className="text-sm font-semibold text-red-600 mb-1">Some uploads failed</div>
          <div className="text-sm text-gray-600">
            Review the failed items in the queue and use the Retry button to try again.
          </div>
        </div>
      )}

      {allSucceeded && (
        <div className="p-3 bg-white border border-green-600 rounded-md mb-4">
          <div className="text-sm text-gray-600">
            All files have been uploaded and are being processed. You can view them in the Uploaded
            PDFs section below.
          </div>
        </div>
      )}

      {/* Action Buttons */}
      <div className="flex gap-3 flex-wrap">
        <Button onClick={onClose} aria-label="Close upload summary">
          Close
        </Button>

        <Button onClick={onClearAll} variant="outline" aria-label="Clear all items from queue">
          Clear Queue
        </Button>
      </div>
    </div>
  );
}
