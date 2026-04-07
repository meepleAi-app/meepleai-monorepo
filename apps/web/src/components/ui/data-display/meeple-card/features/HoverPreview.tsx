'use client';

import { useState, useRef, useCallback, type ReactNode } from 'react';

interface HoverPreviewProps {
  children: ReactNode;
  content: ReactNode;
  delay?: number;
}

export function HoverPreview({ children, content, delay = 500 }: HoverPreviewProps) {
  const [visible, setVisible] = useState(false);
  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const handleEnter = useCallback(() => {
    timerRef.current = setTimeout(() => setVisible(true), delay);
  }, [delay]);

  const handleLeave = useCallback(() => {
    if (timerRef.current) clearTimeout(timerRef.current);
    setVisible(false);
  }, []);

  return (
    <div className="relative" onMouseEnter={handleEnter} onMouseLeave={handleLeave}>
      {children}
      {visible && (
        <div className="absolute left-full top-0 z-50 ml-2 w-64 rounded-xl border border-[var(--mc-border)] bg-[var(--mc-bg-card)] p-3 shadow-[var(--mc-shadow-xl)] backdrop-blur-[12px]">
          {content}
        </div>
      )}
    </div>
  );
}
