import React from 'react';

export interface DiffViewModeToggleProps {
  currentMode: 'list' | 'side-by-side';
  onModeChange: (mode: 'list' | 'side-by-side') => void;
}

/**
 * Toggle between list and side-by-side diff views
 */
export function DiffViewModeToggle({ currentMode, onModeChange }: DiffViewModeToggleProps) {
  return (
    <div className="diff-view-mode-toggle" role="radiogroup" aria-label="Diff view mode">
      <button
        onClick={() => onModeChange('list')}
        className={`view-mode-button ${currentMode === 'list' ? 'view-mode-button--active' : ''}`}
        role="radio"
        aria-checked={currentMode === 'list'}
        aria-label="List view"
      >
        📋 List
      </button>
      <button
        onClick={() => onModeChange('side-by-side')}
        className={`view-mode-button ${currentMode === 'side-by-side' ? 'view-mode-button--active' : ''}`}
        role="radio"
        aria-checked={currentMode === 'side-by-side'}
        aria-label="Side-by-side view"
      >
        ⇄ Side-by-Side
      </button>
    </div>
  );
}
