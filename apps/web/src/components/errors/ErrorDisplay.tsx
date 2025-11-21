/**
 * ErrorDisplay component for user-friendly error messages
 * Implements PDF-06: Error handling with correlation ID tracking
 * Enhanced with toast integration and retry logic for Issue #1095
 */

import { type CSSProperties, useState, useEffect } from 'react';
import { type CategorizedError, getErrorIcon, getErrorTitle } from '@/lib/errorUtils';
import { showErrorToast, shouldShowToast } from '@/lib/toastUtils';

interface ErrorDisplayProps {
  error: CategorizedError;
  onRetry?: () => void;
  onDismiss?: () => void;
  showTechnicalDetails?: boolean;
  /**
   * Show toast notification for transient errors
   * @default true
   */
  showToast?: boolean;
  /**
   * Enable automatic retry with exponential backoff
   * @default false
   */
  autoRetry?: boolean;
  /**
   * Maximum number of automatic retries
   * @default 3
   */
  maxRetries?: number;
}

export function ErrorDisplay({
  error,
  onRetry,
  onDismiss,
  showTechnicalDetails = false,
  showToast = true,
  autoRetry = false,
  maxRetries = 3
}: ErrorDisplayProps) {
  const [showDetails, setShowDetails] = useState(false);
  const [retryCount, setRetryCount] = useState(0);
  const [isRetrying, setIsRetrying] = useState(false);

  // Track error ID to detect actual error transitions (P1 Badge fix)
  // Use correlationId or message as stable identifier to avoid resetting on every object change
  const errorId = error.correlationId || error.message;
  const [lastErrorId, setLastErrorId] = useState(errorId);

  // Reset retry state only when error ID changes (different error scenario)
  useEffect(() => {
    if (errorId !== lastErrorId) {
      setRetryCount(0);
      setIsRetrying(false);
      setLastErrorId(errorId);
    }
  }, [errorId, lastErrorId]);

  // Show toast for transient errors on mount
  useEffect(() => {
    if (showToast && shouldShowToast(error)) {
      showErrorToast(error);
    }
  }, [error, showToast]);

  // Handle automatic retry with exponential backoff
  const handleRetry = async () => {
    if (!onRetry) return;

    setIsRetrying(true);
    setRetryCount(prev => prev + 1);

    try {
      await onRetry();
    } finally {
      setIsRetrying(false);
    }
  };

  // Auto-retry logic
  useEffect(() => {
    if (!autoRetry || !error.canRetry || retryCount >= maxRetries || !onRetry) {
      return;
    }

    // Exponential backoff: 1s, 2s, 4s
    const delay = Math.min(1000 * Math.pow(2, retryCount), 5000);
    const timeout = setTimeout(() => {
      handleRetry();
    }, delay);

    return () => clearTimeout(timeout);
  }, [autoRetry, error.canRetry, retryCount, maxRetries, onRetry]);

  const containerStyle: CSSProperties = {
    padding: '20px',
    border: '2px solid',
    borderColor: error.category === 'network' ? 'hsl(var(--accent))' : 'hsl(var(--destructive))',
    borderRadius: '8px',
    backgroundColor: error.category === 'network' ? 'hsl(var(--accent) / 0.1)' : 'hsl(var(--destructive) / 0.1)',
    marginBottom: '20px'
  };

  const headerStyle: CSSProperties = {
    display: 'flex',
    alignItems: 'center',
    gap: '12px',
    marginBottom: '16px'
  };

  const iconStyle: CSSProperties = {
    fontSize: '32px',
    lineHeight: 1
  };

  const titleStyle: CSSProperties = {
    margin: 0,
    fontSize: '20px',
    fontWeight: 600,
    color: 'hsl(var(--foreground))'
  };

  const messageStyle: CSSProperties = {
    marginBottom: '16px',
    fontSize: '16px',
    lineHeight: 1.5,
    color: 'hsl(var(--foreground))'
  };

  const suggestionsStyle: CSSProperties = {
    marginBottom: '16px',
    padding: '12px',
    backgroundColor: 'hsl(var(--muted))',
    borderRadius: '4px'
  };

  const suggestionsTitleStyle: CSSProperties = {
    marginTop: 0,
    marginBottom: '8px',
    fontSize: '14px',
    fontWeight: 600,
    color: 'hsl(var(--foreground))'
  };

  const suggestionListStyle: CSSProperties = {
    margin: 0,
    paddingLeft: '20px',
    fontSize: '14px',
    color: 'hsl(var(--muted-foreground))'
  };

  const correlationIdStyle: CSSProperties = {
    marginTop: '16px',
    padding: '12px',
    backgroundColor: 'hsl(var(--muted))',
    borderRadius: '4px',
    fontSize: '13px',
    fontFamily: 'monospace',
    color: 'hsl(var(--muted-foreground))',
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between'
  };

  const buttonContainerStyle: CSSProperties = {
    display: 'flex',
    gap: '12px',
    flexWrap: 'wrap',
    marginTop: '16px'
  };

  const buttonStyle: CSSProperties = {
    padding: '10px 20px',
    border: 'none',
    borderRadius: '4px',
    fontSize: '14px',
    fontWeight: 500,
    cursor: 'pointer',
    transition: 'background-color 0.2s'
  };

  // Use semantic tokens for WCAG 2.1 AA compliance (Issue #841)
  const retryButtonStyle: CSSProperties = {
    ...buttonStyle,
    backgroundColor: 'hsl(var(--secondary))',  // WCAG AA compliant green
    color: 'hsl(var(--secondary-foreground))'
  };

  const dismissButtonStyle: CSSProperties = {
    ...buttonStyle,
    backgroundColor: 'hsl(var(--muted))',
    color: 'hsl(var(--muted-foreground))'
  };

  const detailsButtonStyle: CSSProperties = {
    ...buttonStyle,
    backgroundColor: 'transparent',
    color: 'hsl(var(--primary))',
    border: '1px solid hsl(var(--primary))'
  };

  const technicalDetailsStyle: CSSProperties = {
    marginTop: '16px',
    padding: '12px',
    backgroundColor: 'hsl(var(--muted))',
    borderRadius: '4px',
    fontSize: '12px',
    fontFamily: 'monospace',
    color: 'hsl(var(--foreground))',
    whiteSpace: 'pre-wrap',
    wordBreak: 'break-word'
  };

  const copyCorrelationId = () => {
    if (error.correlationId) {
      navigator.clipboard.writeText(error.correlationId).catch(() => {
        // Fallback: select the text
        const element = document.getElementById('correlation-id-text');
        if (element) {
          const range = document.createRange();
          range.selectNodeContents(element);
          const selection = window.getSelection();
          selection?.removeAllRanges();
          selection?.addRange(range);
        }
      });
    }
  };

  return (
    <div style={containerStyle} role="alert" aria-live="assertive">
      <div style={headerStyle}>
        <span style={iconStyle} aria-hidden="true">
          {getErrorIcon(error.category)}
        </span>
        <h3 style={titleStyle}>{getErrorTitle(error.category)}</h3>
      </div>

      <div style={messageStyle}>{error.message}</div>

      {error.suggestions.length > 0 && (
        <div style={suggestionsStyle}>
          <h4 style={suggestionsTitleStyle}>What you can try:</h4>
          <ul style={suggestionListStyle}>
            {error.suggestions.map((suggestion, index) => (
              <li key={index}>{suggestion}</li>
            ))}
          </ul>
        </div>
      )}

      {error.correlationId && (
        <div style={correlationIdStyle}>
          <span>
            <strong>Error ID:</strong>{' '}
            <span id="correlation-id-text">{error.correlationId}</span>
          </span>
          <button
            onClick={copyCorrelationId}
            style={{
              ...buttonStyle,
              padding: '4px 8px',
              fontSize: '12px',
              backgroundColor: 'hsl(var(--muted))',
              color: 'hsl(var(--foreground))'
            }}
            title="Copy to clipboard"
          >
            Copy
          </button>
        </div>
      )}

      <div style={buttonContainerStyle}>
        {onRetry && error.canRetry && (
          <button onClick={handleRetry} style={retryButtonStyle} disabled={isRetrying}>
            {isRetrying ? 'Retrying...' : retryCount > 0 ? `Retry (${retryCount}/${maxRetries})` : 'Retry'}
          </button>
        )}

        {onDismiss && (
          <button onClick={onDismiss} style={dismissButtonStyle}>
            {error.canRetry ? 'Cancel' : 'Go Back'}
          </button>
        )}

        {showTechnicalDetails && error.technicalMessage && (
          <button onClick={() => setShowDetails(!showDetails)} style={detailsButtonStyle}>
            {showDetails ? 'Hide' : 'Show'} Technical Details
          </button>
        )}
      </div>

      {showDetails && error.technicalMessage && (
        <div style={technicalDetailsStyle}>
          <strong>Technical Details:</strong>
          <br />
          {error.technicalMessage}
          {error.statusCode && (
            <>
              <br />
              <strong>Status Code:</strong> {error.statusCode}
            </>
          )}
        </div>
      )}
    </div>
  );
}
