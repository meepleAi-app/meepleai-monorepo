/**
 * PDF-05: Upload summary component
 * Displays final statistics after all uploads complete
 */

import type { UploadQueueStats } from '../hooks/useUploadQueue';

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
      style={{
        padding: '20px',
        border: `2px solid ${summaryColor}`,
        borderRadius: '8px',
        backgroundColor: summaryBgColor,
        marginBottom: '20px'
      }}
    >
      {/* Header */}
      <div style={{ display: 'flex', alignItems: 'center', marginBottom: '16px' }}>
        <div
          aria-hidden="true"
          style={{
            fontSize: '32px',
            marginRight: '12px',
            lineHeight: 1
          }}
        >
          {summaryIcon}
        </div>
        <div>
          <div style={{ fontSize: '18px', fontWeight: 600, color: '#202124' }}>
            Upload Complete
          </div>
          <div style={{ fontSize: '14px', color: '#5f6368', marginTop: '2px' }}>
            {allSucceeded
              ? `All ${stats.total} files uploaded successfully!`
              : `${stats.succeeded} succeeded, ${stats.failed} failed${hasCancelled ? `, ${stats.cancelled} cancelled` : ''}`
            }
          </div>
        </div>
      </div>

      {/* Detailed Stats */}
      <div
        style={{
          display: 'grid',
          gridTemplateColumns: 'repeat(auto-fit, minmax(120px, 1fr))',
          gap: '12px',
          marginBottom: '16px'
        }}
      >
        <div style={{ textAlign: 'center', padding: '12px', backgroundColor: 'white', borderRadius: '6px' }}>
          <div style={{ fontSize: '24px', fontWeight: 700, color: '#202124' }}>
            {stats.total}
          </div>
          <div style={{ fontSize: '13px', color: '#5f6368', marginTop: '4px' }}>
            Total
          </div>
        </div>

        <div style={{ textAlign: 'center', padding: '12px', backgroundColor: 'white', borderRadius: '6px' }}>
          <div style={{ fontSize: '24px', fontWeight: 700, color: '#34a853' }}>
            {stats.succeeded}
          </div>
          <div style={{ fontSize: '13px', color: '#5f6368', marginTop: '4px' }}>
            Succeeded
          </div>
        </div>

        {stats.failed > 0 && (
          <div style={{ textAlign: 'center', padding: '12px', backgroundColor: 'white', borderRadius: '6px' }}>
            <div style={{ fontSize: '24px', fontWeight: 700, color: '#d93025' }}>
              {stats.failed}
            </div>
            <div style={{ fontSize: '13px', color: '#5f6368', marginTop: '4px' }}>
              Failed
            </div>
          </div>
        )}

        {stats.cancelled > 0 && (
          <div style={{ textAlign: 'center', padding: '12px', backgroundColor: 'white', borderRadius: '6px' }}>
            <div style={{ fontSize: '24px', fontWeight: 700, color: '#999' }}>
              {stats.cancelled}
            </div>
            <div style={{ fontSize: '13px', color: '#5f6368', marginTop: '4px' }}>
              Cancelled
            </div>
          </div>
        )}
      </div>

      {/* Messages */}
      {hasFailures && (
        <div
          role="alert"
          style={{
            padding: '12px',
            backgroundColor: 'white',
            border: '1px solid #d93025',
            borderRadius: '6px',
            marginBottom: '16px'
          }}
        >
          <div style={{ fontSize: '14px', fontWeight: 600, color: '#d93025', marginBottom: '4px' }}>
            Some uploads failed
          </div>
          <div style={{ fontSize: '13px', color: '#5f6368' }}>
            Review the failed items in the queue and use the Retry button to try again.
          </div>
        </div>
      )}

      {allSucceeded && (
        <div
          style={{
            padding: '12px',
            backgroundColor: 'white',
            border: '1px solid #34a853',
            borderRadius: '6px',
            marginBottom: '16px'
          }}
        >
          <div style={{ fontSize: '13px', color: '#5f6368' }}>
            All files have been uploaded and are being processed. You can view them in the Uploaded PDFs section below.
          </div>
        </div>
      )}

      {/* Action Buttons */}
      <div style={{ display: 'flex', gap: '12px', flexWrap: 'wrap' }}>
        <button
          onClick={onClose}
          aria-label="Close upload summary"
          style={{
            padding: '10px 20px',
            fontSize: '14px',
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
          Close
        </button>

        <button
          onClick={onClearAll}
          aria-label="Clear all items from queue"
          style={{
            padding: '10px 20px',
            fontSize: '14px',
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
          Clear Queue
        </button>
      </div>
    </div>
  );
}
