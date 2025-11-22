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
    <div className="relative inline-block">
      <button
        role="button"
        aria-label={ariaLabel}
        aria-pressed="false"
        title={truncatedPreview || ariaLabel}
        onClick={handleClick}
        onKeyDown={handleKeyDown}
        onMouseEnter={handleMouseEnter}
        onMouseLeave={handleMouseLeave}
        className="relative w-8 h-8 rounded-full border cursor-pointer transition-all duration-200 flex items-center justify-center p-0 outline-none"
        style={{
          background: hasUnresolved ? '#fff3cd' : '#f5f5f5',
          borderColor: hasUnresolved ? '#ff9800' : '#ddd',
          color: hasUnresolved ? '#ff9800' : '#666'
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
            className="absolute -top-1 -right-1 w-4 h-4 rounded-full text-white text-[10px] font-bold flex items-center justify-center leading-none"
            style={{
              background: hasUnresolved ? '#ff9800' : '#0070f3'
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
              data-testid="inline-comment-unresolved-dot"
              className="absolute -bottom-0.5 -right-0.5 w-2 h-2 rounded-full bg-red-600 border-2 border-white"
              style={{
                animation: 'pulse 2s ease-in-out infinite'
              }}
            />
          </>
        )}
      </button>

      {/* Hover tooltip */}
      {showTooltip && truncatedPreview && (
        <div
          className="absolute bottom-full left-1/2 -translate-x-1/2 mb-2 max-w-[300px] px-3 py-2 bg-gray-800 text-white text-xs rounded shadow-lg whitespace-normal break-words z-[1000] pointer-events-none"
        >
          {truncatedPreview}
          {/* Tooltip arrow */}
          <div
            className="absolute top-full left-1/2 -translate-x-1/2 w-0 h-0 border-l-[6px] border-l-transparent border-r-[6px] border-r-transparent border-t-[6px] border-t-gray-800"
          />
        </div>
      )}
    </div>
  );
};