'use client';
import { useEffect, useState, useCallback } from 'react';

export type LibraryView = 'grid' | 'list';
const STORAGE_KEY = 'mai-library-view';

export function useLibraryView(): {
  view: LibraryView;
  setView: (v: LibraryView) => void;
} {
  const [view, setViewState] = useState<LibraryView>('grid');

  useEffect(() => {
    const stored = localStorage.getItem(STORAGE_KEY);
    if (stored === 'list' || stored === 'grid') setViewState(stored);
  }, []);

  const setView = useCallback((v: LibraryView): void => {
    setViewState(v);
    localStorage.setItem(STORAGE_KEY, v);
  }, []);

  return { view, setView };
}

export function LibraryViewToggle(): React.JSX.Element {
  const { view, setView } = useLibraryView();
  return (
    <div className="inline-flex items-center gap-1 rounded-md border border-border p-0.5">
      <button
        type="button"
        aria-label="Vista griglia"
        aria-pressed={view === 'grid'}
        onClick={() => setView('grid')}
        className={`rounded px-2 py-1 text-xs ${
          view === 'grid' ? 'bg-accent text-foreground' : 'text-muted-foreground'
        }`}
      >
        ▦
      </button>
      <button
        type="button"
        aria-label="Vista lista"
        aria-pressed={view === 'list'}
        onClick={() => setView('list')}
        className={`rounded px-2 py-1 text-xs ${
          view === 'list' ? 'bg-accent text-foreground' : 'text-muted-foreground'
        }`}
      >
        ☰
      </button>
    </div>
  );
}
