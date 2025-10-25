import React, { useState, useEffect } from 'react';

interface InlineCommentIndicatorProps {
  lineNumber: number;
  commentCount: number;
  hasUnresolved: boolean;
  onClick: () => void;
  previewText?: string;
}

export const InlineCommentIndicator: React.FC<InlineCommentIndicatorProps> = ({
  lineNumber,
  commentCount,
  hasUnresolved,
  onClick,
  previewText,
}) => {
  const [showTooltip, setShowTooltip] = useState(false);
  const [tooltipTimer, setTooltipTimer] = useState<NodeJS.Timeout | null>(null);

  // Clean up timer on unmount
  useEffect(() => {
    return () => {
      if (tooltipTimer) {
        clearTimeout(tooltipTimer);
      }
    };
  }, [tooltipTimer]);

  const handleMouseEnter = () => {
    const timer = setTimeout(() => {
      setShowTooltip(true);
    }, 500);
    setTooltipTimer(timer);
  };

  const handleMouseLeave = () => {
    if (tooltipTimer) {
      clearTimeout(tooltipTimer);
      setTooltipTimer(null);
    }
    setShowTooltip(false);
  };

  const handleClick = () => {
    onClick();
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' || e.key === ' ') {
      e.preventDefault();
      onClick();
    }
  };

  const truncatedPreview = previewText
    ? previewText.length > 100
      ? `${previewText.substring(0, 100)}...`
      : previewText
    : '';

  const ariaLabel = `View ${commentCount} comment${commentCount !== 1 ? 's' : ''} on line ${lineNumber}`;

  return (
    <div style={{ position: 'relative', display: 'inline-block' }}>
      <button
        role="button"
        aria-label={ariaLabel}
        aria-pressed="false"
        title={truncatedPreview || ariaLabel}
        onClick={handleClick}
        onKeyDown={handleKeyDown}
        onMouseEnter={handleMouseEnter}
        onMouseLeave={handleMouseLeave}
        style={{
          position: 'relative',
          width: '32px',
          height: '32px',
          borderRadius: '50%',
          border: '1px solid',
          cursor: 'pointer',
          transition: 'all 0.2s ease',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          background: hasUnresolved ? '#fff3cd' : '#f5f5f5',
          borderColor: hasUnresolved ? '#ff9800' : '#ddd',
          color: hasUnresolved ? '#ff9800' : '#666',
          padding: 0,
          outline: 'none',
        }}
        onMouseOver={(e) => {
          e.currentTarget.style.transform = 'scale(1.1)';
          e.currentTarget.style.boxShadow = '0 2px 8px rgba(0,0,0,0.15)';
        }}
        onMouseOut={(e) => {
          e.currentTarget.style.transform = 'scale(1)';
          e.currentTarget.style.boxShadow = 'none';
        }}
        onFocus={(e) => {
          e.currentTarget.style.boxShadow = '0 0 0 3px rgba(0,112,243,0.3)';
        }}
        onBlur={(e) => {
          e.currentTarget.style.boxShadow = 'none';
        }}
      >
        {/* MessageCircle icon (SVG inline) */}
        <svg
          width="16"
          height="16"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          strokeWidth="2"
          strokeLinecap="round"
          strokeLinejoin="round"
        >
          <path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z" />
        </svg>

        {/* Count badge (only show if count > 1) */}
        {commentCount > 1 && (
          <span
            style={{
              position: 'absolute',
              top: '-4px',
              right: '-4px',
              width: '16px',
              height: '16px',
              borderRadius: '50%',
              background: hasUnresolved ? '#ff9800' : '#0070f3',
              color: 'white',
              fontSize: '10px',
              fontWeight: 'bold',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              lineHeight: 1,
            }}
          >
            {commentCount}
          </span>
        )}

        {/* Unresolved indicator dot (only show if unresolved) */}
        {hasUnresolved && (
          <>
            <style>
              {`
                @keyframes pulse {
                  0%, 100% {
                    opacity: 1;
                    transform: scale(1);
                  }
                  50% {
                    opacity: 0.7;
                    transform: scale(1.1);
                  }
                }
              `}
            </style>
            <span
              style={{
                position: 'absolute',
                bottom: '-2px',
                right: '-2px',
                width: '8px',
                height: '8px',
                borderRadius: '50%',
                background: '#d93025',
                border: '2px solid white',
                animation: 'pulse 2s ease-in-out infinite',
              }}
            />
          </>
        )}
      </button>

      {/* Hover tooltip */}
      {showTooltip && truncatedPreview && (
        <div
          style={{
            position: 'absolute',
            bottom: '100%',
            left: '50%',
            transform: 'translateX(-50%)',
            marginBottom: '8px',
            maxWidth: '300px',
            padding: '8px 12px',
            background: '#333',
            color: 'white',
            fontSize: '12px',
            borderRadius: '4px',
            boxShadow: '0 2px 8px rgba(0,0,0,0.2)',
            whiteSpace: 'normal',
            wordBreak: 'break-word',
            zIndex: 1000,
            pointerEvents: 'none',
          }}
        >
          {truncatedPreview}
          {/* Tooltip arrow */}
          <div
            style={{
              position: 'absolute',
              top: '100%',
              left: '50%',
              transform: 'translateX(-50%)',
              width: 0,
              height: 0,
              borderLeft: '6px solid transparent',
              borderRight: '6px solid transparent',
              borderTop: '6px solid #333',
            }}
          />
        </div>
      )}
    </div>
  );
};
