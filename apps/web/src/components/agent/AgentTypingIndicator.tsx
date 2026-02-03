/**
 * AgentTypingIndicator - Typing animation (Issue #3187)
 *
 * Animated dots indicator displayed during SSE streaming
 */

import React from 'react';

export function AgentTypingIndicator() {
  return (
    <div className="flex items-start" aria-label="Agent is typing">
      <div className="bg-[#f1f3f4] p-3 rounded-lg">
        <div className="flex items-center gap-1">
          <div className="w-2 h-2 bg-[#94a3b8] rounded-full animate-bounce [animation-delay:-0.3s]" />
          <div className="w-2 h-2 bg-[#94a3b8] rounded-full animate-bounce [animation-delay:-0.15s]" />
          <div className="w-2 h-2 bg-[#94a3b8] rounded-full animate-bounce" />
        </div>
      </div>
    </div>
  );
}
