'use client';

import type { ReactElement } from 'react';

export interface ReaderModeToggleProps {
  isReaderMode: boolean;
  onToggle: () => void;
}

/**
 * Reader-mode toggle button per mockup §1b lines 1697-1727.
 * Toggles between default (16pt) and reader-mode (24pt + 1.7 line-height + 65ch).
 *
 * #1558 H · Aaron CORE spec 2026-05-23.
 */
export function ReaderModeToggle({ isReaderMode, onToggle }: ReaderModeToggleProps): ReactElement {
  return (
    <button
      type="button"
      onClick={onToggle}
      aria-pressed={isReaderMode}
      aria-label={isReaderMode ? 'Disattiva Reader Mode' : 'Attiva Reader Mode'}
      className={`reader-mode-toggle${isReaderMode ? ' active' : ''}`}
    >
      <span className="ico" aria-hidden="true">
        📖
      </span>
      <span>{isReaderMode ? 'Reader ✓' : 'Reader'}</span>
    </button>
  );
}
