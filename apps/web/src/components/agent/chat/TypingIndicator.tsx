/**
 * TypingIndicator - Typing animation with cursor (Issue #3243)
 *
 * Displays animated dots with blinking cursor (▊) during SSE streaming
 */

import React from 'react';

export function TypingIndicator() {
  return (
    <div className="flex items-start" aria-label="Agent is typing">
      <div className="bg-gray-800 p-3 rounded-lg">
        <div className="flex items-center gap-1.5">
          {/* Animated dots */}
          <div className="w-2 h-2 bg-gray-400 rounded-full animate-bounce [animation-delay:-0.3s]" />
          <div className="w-2 h-2 bg-gray-400 rounded-full animate-bounce [animation-delay:-0.15s]" />
          <div className="w-2 h-2 bg-gray-400 rounded-full animate-bounce" />

          {/* Blinking cursor - using Tailwind animate-pulse for blink effect */}
          <span className="ml-1 text-gray-300 text-base font-mono animate-pulse">▊</span>
        </div>
      </div>
    </div>
  );
}
