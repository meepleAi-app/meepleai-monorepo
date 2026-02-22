'use client';

/**
 * ShowcaseCanvas — Component preview area
 *
 * Provides background toggle (light / dark / grid), zoom, and padding controls.
 * Renders the story component inside a centered container.
 */

import type { ReactNode } from 'react';

import { Sun, Moon, Grid3X3, ZoomIn, ZoomOut } from 'lucide-react';

import { cn } from '@/lib/utils';

type BgMode = 'light' | 'dark' | 'grid';

interface ShowcaseCanvasProps {
  /** The rendered component */
  children: ReactNode;
  /** Canvas background mode */
  bgMode: BgMode;
  /** Zoom level (0.5 – 2) */
  zoom: number;
  /** Callback for background change */
  onBgChange: (mode: BgMode) => void;
  /** Callback for zoom change */
  onZoomChange: (zoom: number) => void;
  /** Story title */
  title?: string;
  /** Story description */
  description?: string;
  /** Preset picker */
  presetPicker?: ReactNode;
}

const BG_CLASSES: Record<BgMode, string> = {
  light: 'bg-white',
  dark: 'bg-zinc-900',
  grid: 'bg-white bg-[linear-gradient(to_right,#e5e7eb_1px,transparent_1px),linear-gradient(to_bottom,#e5e7eb_1px,transparent_1px)] bg-[size:20px_20px]',
};

const ZOOM_STEPS = [0.5, 0.75, 1, 1.25, 1.5, 2];

export function ShowcaseCanvas({
  children,
  bgMode,
  zoom,
  onBgChange,
  onZoomChange,
  title,
  description,
  presetPicker,
}: ShowcaseCanvasProps) {
  const zoomIdx = ZOOM_STEPS.indexOf(zoom);

  const handleZoomIn = () => {
    const next = ZOOM_STEPS[Math.min(zoomIdx + 1, ZOOM_STEPS.length - 1)];
    if (next !== undefined) onZoomChange(next);
  };

  const handleZoomOut = () => {
    const prev = ZOOM_STEPS[Math.max(zoomIdx - 1, 0)];
    if (prev !== undefined) onZoomChange(prev);
  };

  return (
    <div className="flex h-full flex-col overflow-hidden">
      {/* Toolbar */}
      <div className="flex items-center justify-between border-b border-border/60 bg-card/80 px-4 py-2 backdrop-blur-sm">
        <div className="flex flex-col">
          {title && (
            <span className="text-sm font-semibold font-quicksand text-foreground">{title}</span>
          )}
          {description && (
            <span className="text-xs text-muted-foreground">{description}</span>
          )}
        </div>

        <div className="flex items-center gap-2">
          {/* Preset picker slot */}
          {presetPicker}

          {/* Divider */}
          <div className="h-4 w-px bg-border/60" />

          {/* Background toggles */}
          <div className="flex items-center gap-0.5 rounded-md border border-border/60 bg-background p-0.5">
            <button
              onClick={() => onBgChange('light')}
              title="Light background"
              className={cn(
                'rounded p-1 transition-colors',
                bgMode === 'light'
                  ? 'bg-amber-100 text-amber-700'
                  : 'text-muted-foreground hover:text-foreground'
              )}
            >
              <Sun className="h-3.5 w-3.5" />
            </button>
            <button
              onClick={() => onBgChange('dark')}
              title="Dark background"
              className={cn(
                'rounded p-1 transition-colors',
                bgMode === 'dark'
                  ? 'bg-zinc-700 text-zinc-100'
                  : 'text-muted-foreground hover:text-foreground'
              )}
            >
              <Moon className="h-3.5 w-3.5" />
            </button>
            <button
              onClick={() => onBgChange('grid')}
              title="Grid background"
              className={cn(
                'rounded p-1 transition-colors',
                bgMode === 'grid'
                  ? 'bg-blue-100 text-blue-700'
                  : 'text-muted-foreground hover:text-foreground'
              )}
            >
              <Grid3X3 className="h-3.5 w-3.5" />
            </button>
          </div>

          {/* Zoom controls */}
          <div className="flex items-center gap-0.5 rounded-md border border-border/60 bg-background p-0.5">
            <button
              onClick={handleZoomOut}
              disabled={zoomIdx <= 0}
              title="Zoom out"
              className="rounded p-1 text-muted-foreground hover:text-foreground disabled:opacity-40"
            >
              <ZoomOut className="h-3.5 w-3.5" />
            </button>
            <span className="min-w-[2.5rem] text-center text-[11px] text-muted-foreground">
              {Math.round(zoom * 100)}%
            </span>
            <button
              onClick={handleZoomIn}
              disabled={zoomIdx >= ZOOM_STEPS.length - 1}
              title="Zoom in"
              className="rounded p-1 text-muted-foreground hover:text-foreground disabled:opacity-40"
            >
              <ZoomIn className="h-3.5 w-3.5" />
            </button>
          </div>
        </div>
      </div>

      {/* Preview area */}
      {/* eslint-disable-next-line security/detect-object-injection */}
      <div className={cn('flex-1 overflow-auto p-8', BG_CLASSES[bgMode])}>
        <div
          className="flex min-h-full items-start justify-center"
          style={{ transformOrigin: 'top center', transform: `scale(${zoom})` }}
        >
          {children}
        </div>
      </div>
    </div>
  );
}
