/**
 * ErrorDisplay component for user-friendly error messages
 * Implements PDF-06: Error handling with correlation ID tracking
 */

import { type CSSProperties, useState } from 'react';
import { type CategorizedError, getErrorIcon, getErrorTitle } from '../lib/errorUtils';

interface ErrorDisplayProps {
  error: CategorizedError;
  onRetry?: () => void;
  onDismiss?: () => void;
  showTechnicalDetails?: boolean;
}

export function ErrorDisplay({ error, onRetry, onDismiss, showTechnicalDetails = false }: ErrorDisplayProps) {
  const [showDetails, setShowDetails] = useState(false);

  const containerStyle: CSSProperties = {
    padding: '20px',
    border: '2px solid',
    borderColor: error.category === 'network' ? '#ff9800' : '#f44336',
    borderRadius: '8px',
    backgroundColor: error.category === 'network' ? '#fff3e0' : '#ffebee',
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
    color: '#333'
  };

  const messageStyle: CSSProperties = {
    marginBottom: '16px',
    fontSize: '16px',
    lineHeight: 1.5,
    color: '#444'
  };

  const suggestionsStyle: CSSProperties = {
    marginBottom: '16px',
    padding: '12px',
    backgroundColor: 'rgba(255, 255, 255, 0.7)',
    borderRadius: '4px'
  };

  const suggestionsTitleStyle: CSSProperties = {
    marginTop: 0,
    marginBottom: '8px',
    fontSize: '14px',
    fontWeight: 600,
    color: '#555'
  };

  const suggestionListStyle: CSSProperties = {
    margin: 0,
    paddingLeft: '20px',
    fontSize: '14px',
    color: '#666'
  };

  const correlationIdStyle: CSSProperties = {
    marginTop: '16px',
    padding: '12px',
    backgroundColor: 'rgba(0, 0, 0, 0.05)',
    borderRadius: '4px',
    fontSize: '13px',
    fontFamily: 'monospace',
    color: '#666',
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

  const retryButtonStyle: CSSProperties = {
    ...buttonStyle,
    backgroundColor: '#4caf50',
    color: 'white'
  };

  const dismissButtonStyle: CSSProperties = {
    ...buttonStyle,
    backgroundColor: '#757575',
    color: 'white'
  };

  const detailsButtonStyle: CSSProperties = {
    ...buttonStyle,
    backgroundColor: 'transparent',
    color: '#1976d2',
    border: '1px solid #1976d2'
  };

  const technicalDetailsStyle: CSSProperties = {
    marginTop: '16px',
    padding: '12px',
    backgroundColor: 'rgba(0, 0, 0, 0.05)',
    borderRadius: '4px',
    fontSize: '12px',
    fontFamily: 'monospace',
    color: '#444',
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
              backgroundColor: '#e0e0e0',
              color: '#333'
            }}
            title="Copy to clipboard"
          >
            Copy
          </button>
        </div>
      )}

      <div style={buttonContainerStyle}>
        {onRetry && error.canRetry && (
          <button onClick={onRetry} style={retryButtonStyle}>
            Retry
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
