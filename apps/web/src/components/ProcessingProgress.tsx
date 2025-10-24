/**
 * ProcessingProgress Component (PDF-08)
 * Displays real-time PDF processing progress with polling and cancellation support
 */

'use client';

import { useEffect, useState, useCallback, useRef, type CSSProperties } from 'react';
import { api } from '../lib/api';
import {
  ProcessingStep,
  type ProcessingProgress as ProcessingProgressType,
  isProcessingComplete,
  getStepLabel,
  getStepOrder
} from '../types/pdf';
import { SkeletonLoader } from './loading';

interface ProcessingProgressProps {
  pdfId: string;
  onComplete?: () => void;
  onError?: (error: string) => void;
}

const POLL_INTERVAL_MS = 2000;
const MAX_POLL_DURATION_MS = 600000; // 10 minutes max

/**
 * Formats seconds into human-readable time (e.g., "2 min 30 sec")
 */
function formatTimeRemaining(seconds: number): string {
  if (seconds <= 0) {
    return 'Less than a minute';
  }

  const minutes = Math.floor(seconds / 60);
  const remainingSeconds = seconds % 60;

  if (minutes === 0) {
    return `${remainingSeconds} sec`;
  }

  if (remainingSeconds === 0) {
    return `${minutes} min`;
  }

  return `${minutes} min ${remainingSeconds} sec`;
}

export function ProcessingProgress({ pdfId, onComplete, onError }: ProcessingProgressProps) {
  const [progress, setProgress] = useState<ProcessingProgressType | null>(null);
  const [loading, setLoading] = useState(true);
  const [networkError, setNetworkError] = useState<string | null>(null);
  const [showCancelDialog, setShowCancelDialog] = useState(false);
  const [canceling, setCanceling] = useState(false);

  const pollStartTimeRef = useRef<number>(Date.now());
  const intervalRef = useRef<NodeJS.Timeout | null>(null);
  const isMountedRef = useRef(true);
  const latestProgressRef = useRef<ProcessingProgressType | null>(null);
  const hasNotifiedCompletionRef = useRef(false);

  useEffect(() => {
    isMountedRef.current = true;
    return () => {
      isMountedRef.current = false;
    };
  }, []);

  useEffect(() => {
    hasNotifiedCompletionRef.current = false;
    latestProgressRef.current = null;
    setProgress(null);
    setLoading(true);
    setNetworkError(null);
  }, [pdfId]);

  // Fetch progress from API
  const fetchProgress = useCallback(async () => {
    try {
      const data = await api.pdf.getProcessingProgress(pdfId);

      if (!isMountedRef.current) {
        return;
      }

      if (!data) {
        setNetworkError('Failed to fetch processing progress');
        setLoading(false);
        return;
      }

      latestProgressRef.current = data;
      setProgress(data);
      setNetworkError(null);
      setLoading(false);

      // Check for completion
      if (isProcessingComplete(data.currentStep)) {
        if (
          data.currentStep === ProcessingStep.Completed &&
          onComplete &&
          !hasNotifiedCompletionRef.current
        ) {
          hasNotifiedCompletionRef.current = true;
          onComplete();
        } else if (data.currentStep === ProcessingStep.Failed && onError && data.errorMessage) {
          onError(data.errorMessage);
        }
      }
    } catch (error) {
      if (!isMountedRef.current) {
        return;
      }

      const errorMessage = error instanceof Error ? error.message : 'Unknown error';
      setNetworkError(errorMessage);
      setLoading(false);
    }
  }, [pdfId, onComplete, onError]);

  // Setup polling
  useEffect(() => {
    pollStartTimeRef.current = Date.now();

    if (intervalRef.current) {
      clearInterval(intervalRef.current);
      intervalRef.current = null;
    }

    // Initial fetch
    void fetchProgress();

    // Setup interval for polling
    intervalRef.current = setInterval(() => {
      if (!isMountedRef.current) {
        return;
      }

      // Check max poll duration
      const elapsed = Date.now() - pollStartTimeRef.current;
      if (elapsed > MAX_POLL_DURATION_MS) {
        if (intervalRef.current) {
          clearInterval(intervalRef.current);
          intervalRef.current = null;
        }
        setNetworkError('Processing timeout exceeded (10 minutes). Please refresh to check status.');
        return;
      }

      const currentProgress = latestProgressRef.current;
      if (currentProgress && isProcessingComplete(currentProgress.currentStep)) {
        if (intervalRef.current) {
          clearInterval(intervalRef.current);
          intervalRef.current = null;
        }
        return;
      }

      void fetchProgress();
    }, POLL_INTERVAL_MS);

    // Cleanup on unmount
    return () => {
      if (intervalRef.current) {
        clearInterval(intervalRef.current);
        intervalRef.current = null;
      }
    };
  }, [fetchProgress]);

  // Handle cancel
  const handleCancelClick = useCallback(() => {
    setShowCancelDialog(true);
  }, []);

  const handleConfirmCancel = useCallback(async () => {
    setCanceling(true);
    setShowCancelDialog(false);

    try {
      await api.pdf.cancelProcessing(pdfId);

      if (!isMountedRef.current) {
        return;
      }

      // Fetch updated status
      await fetchProgress();
    } catch (error) {
      if (!isMountedRef.current) {
        return;
      }

      const errorMessage = error instanceof Error ? error.message : 'Failed to cancel processing';
      setNetworkError(errorMessage);
    } finally {
      if (isMountedRef.current) {
        setCanceling(false);
      }
    }
  }, [pdfId, fetchProgress]);

  const handleCancelDialogClose = useCallback(() => {
    setShowCancelDialog(false);
  }, []);

  // Styles
  const containerStyle: CSSProperties = {
    padding: '24px',
    border: '1px solid #e0e0e0',
    borderRadius: '8px',
    backgroundColor: '#f9fafb'
  };

  const headerStyle: CSSProperties = {
    marginTop: 0,
    marginBottom: '16px',
    fontSize: '18px',
    fontWeight: 600,
    color: '#333'
  };

  const progressBarContainerStyle: CSSProperties = {
    width: '100%',
    height: '12px',
    backgroundColor: '#e5e7eb',
    borderRadius: '999px',
    overflow: 'hidden',
    marginBottom: '16px'
  };

  const progressBarFillStyle: CSSProperties = {
    width: `${progress?.percentComplete ?? 0}%`,
    height: '100%',
    backgroundColor:
      progress?.currentStep === ProcessingStep.Completed
        ? '#34a853'
        : progress?.currentStep === ProcessingStep.Failed
          ? '#d93025'
          : '#0070f3',
    transition: 'width 0.6s ease'
  };

  const stepIndicatorContainerStyle: CSSProperties = {
    display: 'flex',
    justifyContent: 'space-between',
    marginBottom: '20px',
    gap: '8px'
  };

  const stepIndicatorStyle = (step: ProcessingStep): CSSProperties => {
    const currentStepOrder = progress ? getStepOrder(progress.currentStep) : -1;
    const stepOrder = getStepOrder(step);
    const isActive = progress?.currentStep === step;
    const isCompleted = stepOrder < currentStepOrder;

    return {
      flex: 1,
      textAlign: 'center',
      padding: '8px 4px',
      borderRadius: '4px',
      fontSize: '12px',
      backgroundColor: isActive ? '#e3f2fd' : isCompleted ? '#e8f5e9' : '#f5f5f5',
      border: `2px solid ${isActive ? '#0070f3' : isCompleted ? '#34a853' : '#ddd'}`,
      color: isActive ? '#0070f3' : isCompleted ? '#34a853' : '#666',
      fontWeight: isActive ? 600 : 400
    };
  };

  const statusTextStyle: CSSProperties = {
    marginBottom: '12px',
    fontSize: '16px',
    color: '#444',
    fontWeight: 500
  };

  const timeRemainingStyle: CSSProperties = {
    marginBottom: '16px',
    fontSize: '14px',
    color: '#666'
  };

  const errorMessageStyle: CSSProperties = {
    padding: '12px',
    backgroundColor: '#ffebee',
    border: '1px solid #d93025',
    borderRadius: '4px',
    color: '#d93025',
    marginBottom: '16px',
    fontSize: '14px'
  };

  const networkErrorStyle: CSSProperties = {
    padding: '12px',
    backgroundColor: '#fff3e0',
    border: '1px solid #ff9800',
    borderRadius: '4px',
    color: '#ff6f00',
    marginBottom: '16px',
    fontSize: '14px'
  };

  const buttonContainerStyle: CSSProperties = {
    display: 'flex',
    gap: '12px'
  };

  const cancelButtonStyle: CSSProperties = {
    padding: '10px 20px',
    backgroundColor: canceling ? '#ccc' : '#d93025',
    color: 'white',
    border: 'none',
    borderRadius: '4px',
    fontSize: '14px',
    fontWeight: 500,
    cursor: canceling ? 'not-allowed' : 'pointer'
  };

  const dialogOverlayStyle: CSSProperties = {
    position: 'fixed',
    top: 0,
    left: 0,
    right: 0,
    bottom: 0,
    backgroundColor: 'rgba(0, 0, 0, 0.5)',
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    zIndex: 1000
  };

  const dialogStyle: CSSProperties = {
    backgroundColor: 'white',
    padding: '24px',
    borderRadius: '8px',
    maxWidth: '400px',
    width: '90%'
  };

  const dialogTitleStyle: CSSProperties = {
    marginTop: 0,
    marginBottom: '16px',
    fontSize: '18px',
    fontWeight: 600,
    color: '#333'
  };

  const dialogTextStyle: CSSProperties = {
    marginBottom: '20px',
    fontSize: '14px',
    color: '#666',
    lineHeight: 1.5
  };

  const dialogButtonContainerStyle: CSSProperties = {
    display: 'flex',
    gap: '12px',
    justifyContent: 'flex-end'
  };

  const dialogCancelButtonStyle: CSSProperties = {
    padding: '8px 16px',
    backgroundColor: '#666',
    color: 'white',
    border: 'none',
    borderRadius: '4px',
    fontSize: '14px',
    fontWeight: 500,
    cursor: 'pointer'
  };

  const dialogConfirmButtonStyle: CSSProperties = {
    padding: '8px 16px',
    backgroundColor: '#d93025',
    color: 'white',
    border: 'none',
    borderRadius: '4px',
    fontSize: '14px',
    fontWeight: 500,
    cursor: 'pointer'
  };

  // Non-terminal steps for step indicator
  const steps = [
    ProcessingStep.Uploading,
    ProcessingStep.Extracting,
    ProcessingStep.Chunking,
    ProcessingStep.Embedding,
    ProcessingStep.Indexing
  ];

  if (loading && !progress) {
    return (
      <div style={containerStyle}>
        <SkeletonLoader variant="processingProgress" ariaLabel="Loading processing progress" />
      </div>
    );
  }

  return (
    <div data-testid="processing-progress" style={containerStyle}>
      <h3 style={headerStyle}>PDF Processing Progress</h3>

      {/* Progress Bar */}
      <div
        role="progressbar"
        aria-label="PDF processing progress"
        aria-valuenow={progress?.percentComplete ?? 0}
        aria-valuemin={0}
        aria-valuemax={100}
        aria-live="polite"
        style={progressBarContainerStyle}
      >
        <div style={progressBarFillStyle} />
      </div>

      {/* Step Indicators */}
      <div style={stepIndicatorContainerStyle} aria-label="Processing steps">
        {steps.map((step) => (
          <div key={step} style={stepIndicatorStyle(step)} title={getStepLabel(step)}>
            {step}
          </div>
        ))}
      </div>

      {/* Current Status */}
      {progress && (
        <div>
          <p style={statusTextStyle}>
            <strong>Processing status:</strong> {getStepLabel(progress.currentStep)}
          </p>

          {/* Time Remaining */}
          {progress.estimatedTimeRemaining !== undefined &&
            progress.estimatedTimeRemaining !== null &&
            !isProcessingComplete(progress.currentStep) && (
              <p style={timeRemainingStyle}>
                <strong>Estimated time remaining:</strong>{' '}
                {formatTimeRemaining(progress.estimatedTimeRemaining)}
              </p>
            )}

          {/* Progress Percentage */}
          <p style={timeRemainingStyle}>
            <strong>Progress:</strong> {progress.percentComplete}%
          </p>

          {/* Error Message */}
          {progress.currentStep === ProcessingStep.Failed && progress.errorMessage && (
            <div style={errorMessageStyle} role="alert">
              <strong>Error:</strong> {progress.errorMessage}
            </div>
          )}

          {/* Network Error */}
          {networkError && (
            <div style={networkErrorStyle} role="alert">
              <strong>Network Error:</strong> {networkError}
            </div>
          )}

          {/* Cancel Button */}
          {!isProcessingComplete(progress.currentStep) && (
            <div style={buttonContainerStyle}>
              <button
                onClick={handleCancelClick}
                disabled={canceling}
                style={cancelButtonStyle}
                aria-label="Cancel PDF processing"
              >
                {canceling ? 'Canceling...' : 'Cancel Processing'}
              </button>
            </div>
          )}
        </div>
      )}

      {/* Cancel Confirmation Dialog */}
      {showCancelDialog && (
        <div style={dialogOverlayStyle} onClick={handleCancelDialogClose} role="dialog" aria-modal="true" aria-labelledby="cancel-dialog-title">
          <div style={dialogStyle} onClick={(e) => e.stopPropagation()}>
            <h4 id="cancel-dialog-title" style={dialogTitleStyle}>
              Cancel PDF Processing?
            </h4>
            <p style={dialogTextStyle}>
              Are you sure you want to cancel the PDF processing? This action cannot be undone.
            </p>
            <div style={dialogButtonContainerStyle}>
              <button onClick={handleCancelDialogClose} style={dialogCancelButtonStyle}>
                No, Continue Processing
              </button>
              <button onClick={handleConfirmCancel} style={dialogConfirmButtonStyle}>
                Yes, Cancel
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
