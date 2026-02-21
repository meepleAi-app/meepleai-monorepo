'use client';

/**
 * WhiteboardTool — collaborative drawing canvas + structured grid
 * Epic #4968: Game Session Toolkit v2
 *
 * Draw mode: SVG-backed freehand drawing (mouse / touch)
 * Structured mode: grid with draggable tokens
 * Both mode: split view
 */

import React, { useCallback, useRef, useState } from 'react';

import { LayoutGrid, PenLine, Trash2 } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';

import type {
  GridSize,
  Stroke,
  StrokePoint,
  WhiteboardMode,
  WhiteboardState,
  WhiteboardToken,
} from './types';

// ── Helpers ───────────────────────────────────────────────────────────────────

function gridDimensions(size: GridSize): { cols: number; rows: number } {
  const [c, r] = size.split('x').map(Number);
  return { cols: c ?? 6, rows: r ?? 6 };
}

function pointsToPath(points: StrokePoint[]): string {
  if (points.length < 2) return '';
  const [first, ...rest] = points;
  if (!first) return '';
  return `M${first.x},${first.y} ` + rest.map((p) => `L${p.x},${p.y}`).join(' ');
}

const GRID_SIZES: GridSize[] = ['4x4', '6x6', '8x8', '10x10'];
const STROKE_COLORS = ['#1e293b', '#ef4444', '#3b82f6', '#22c55e', '#f59e0b', '#8b5cf6'];
const STROKE_WIDTH = 3;

// ── Draw canvas ───────────────────────────────────────────────────────────────

interface DrawCanvasProps {
  strokes: Stroke[];
  onStrokesChange: (strokes: Stroke[]) => void;
  selectedColor: string;
}

function DrawCanvas({ strokes, onStrokesChange, selectedColor }: DrawCanvasProps) {
  const svgRef = useRef<SVGSVGElement>(null);
  const [currentPoints, setCurrentPoints] = useState<StrokePoint[]>([]);
  const isDrawing = useRef(false);

  const getPoint = useCallback(
    (e: React.MouseEvent | React.TouchEvent): StrokePoint | null => {
      const svg = svgRef.current;
      if (!svg) return null;
      const rect = svg.getBoundingClientRect();
      const clientX = 'touches' in e ? e.touches[0]?.clientX ?? 0 : e.clientX;
      const clientY = 'touches' in e ? e.touches[0]?.clientY ?? 0 : e.clientY;
      return { x: clientX - rect.left, y: clientY - rect.top };
    },
    [],
  );

  const onStart = useCallback(
    (e: React.MouseEvent | React.TouchEvent) => {
      e.preventDefault();
      isDrawing.current = true;
      const pt = getPoint(e);
      if (pt) setCurrentPoints([pt]);
    },
    [getPoint],
  );

  const onMove = useCallback(
    (e: React.MouseEvent | React.TouchEvent) => {
      if (!isDrawing.current) return;
      e.preventDefault();
      const pt = getPoint(e);
      if (pt) setCurrentPoints((prev) => [...prev, pt]);
    },
    [getPoint],
  );

  const onEnd = useCallback(() => {
    if (!isDrawing.current || currentPoints.length < 2) {
      isDrawing.current = false;
      setCurrentPoints([]);
      return;
    }
    isDrawing.current = false;
    const newStroke: Stroke = {
      id: `stroke-${Date.now()}`,
      points: currentPoints,
      color: selectedColor,
      width: STROKE_WIDTH,
      participantId: 'local',
      timestamp: new Date(),
    };
    onStrokesChange([...strokes, newStroke]);
    setCurrentPoints([]);
  }, [currentPoints, selectedColor, strokes, onStrokesChange]);

  return (
    <svg
      ref={svgRef}
      className="w-full h-full touch-none cursor-crosshair bg-white dark:bg-slate-900 rounded-lg"
      style={{ minHeight: 320 }}
      onMouseDown={onStart}
      onMouseMove={onMove}
      onMouseUp={onEnd}
      onMouseLeave={onEnd}
      onTouchStart={onStart}
      onTouchMove={onMove}
      onTouchEnd={onEnd}
      aria-label="Drawing canvas"
      role="img"
    >
      {/* Committed strokes */}
      {strokes.map((stroke) => (
        <path
          key={stroke.id}
          d={pointsToPath(stroke.points)}
          stroke={stroke.color}
          strokeWidth={stroke.width}
          strokeLinecap="round"
          strokeLinejoin="round"
          fill="none"
        />
      ))}
      {/* In-progress stroke */}
      {currentPoints.length >= 2 && (
        <path
          d={pointsToPath(currentPoints)}
          stroke={selectedColor}
          strokeWidth={STROKE_WIDTH}
          strokeLinecap="round"
          strokeLinejoin="round"
          fill="none"
          opacity={0.7}
        />
      )}
    </svg>
  );
}

// ── Structured grid ───────────────────────────────────────────────────────────

interface GridViewProps {
  tokens: WhiteboardToken[];
  gridSize: GridSize;
  showGrid: boolean;
  onStructuredChange: (
    tokens: WhiteboardToken[],
    gridSize: GridSize,
    showGrid: boolean,
    mode: WhiteboardMode,
  ) => void;
  mode: WhiteboardMode;
}

function GridView({ tokens, gridSize, showGrid, onStructuredChange, mode }: GridViewProps) {
  const { cols, rows } = gridDimensions(gridSize);

  const toggleToken = (col: number, row: number) => {
    const existing = tokens.find((t) => t.x === col && t.y === row);
    if (existing) {
      onStructuredChange(
        tokens.filter((t) => t !== existing),
        gridSize,
        showGrid,
        mode,
      );
    } else {
      const newToken: WhiteboardToken = {
        id: `token-${Date.now()}`,
        x: col,
        y: row,
        label: '●',
        color: '#f59e0b',
        shape: 'circle',
      };
      onStructuredChange([...tokens, newToken], gridSize, showGrid, mode);
    }
  };

  return (
    <div className="overflow-auto">
      <div
        className="inline-grid gap-0.5"
        style={{ gridTemplateColumns: `repeat(${cols}, minmax(36px, 1fr))` }}
        role="grid"
        aria-label={`${cols}×${rows} game grid`}
      >
        {Array.from({ length: rows }, (_, row) =>
          Array.from({ length: cols }, (_, col) => {
            const token = tokens.find((t) => t.x === col && t.y === row);
            return (
              <button
                key={`${col}-${row}`}
                role="gridcell"
                aria-label={`Cell ${col + 1},${row + 1}${token ? ' (occupied)' : ''}`}
                onClick={() => toggleToken(col, row)}
                className={cn(
                  'aspect-square flex items-center justify-center rounded text-lg transition-colors',
                  showGrid
                    ? 'border border-slate-200 dark:border-slate-700'
                    : 'border border-transparent',
                  token
                    ? 'bg-amber-100 dark:bg-amber-900/40'
                    : 'bg-white dark:bg-slate-800/60 hover:bg-slate-50 dark:hover:bg-slate-800',
                )}
              >
                {token && (
                  <span style={{ color: token.color }} aria-hidden="true">
                    {token.label}
                  </span>
                )}
              </button>
            );
          }),
        )}
      </div>
    </div>
  );
}

// ── Main component ────────────────────────────────────────────────────────────

interface WhiteboardToolProps {
  whiteboardState: WhiteboardState;
  onStrokesChange: (strokes: WhiteboardState['strokes']) => void;
  onStructuredChange: (
    tokens: WhiteboardState['tokens'],
    gridSize: WhiteboardState['gridSize'],
    showGrid: boolean,
    mode: WhiteboardState['mode'],
  ) => void;
  onClear: () => void;
}

export function WhiteboardTool({
  whiteboardState,
  onStrokesChange,
  onStructuredChange,
  onClear,
}: WhiteboardToolProps) {
  const { strokes, tokens, gridSize, showGrid, mode } = whiteboardState;
  const [selectedColor, setSelectedColor] = useState(STROKE_COLORS[0] ?? '#1e293b');
  const [activeMode, setActiveMode] = useState<WhiteboardMode>(mode);

  const switchMode = (m: WhiteboardMode) => {
    setActiveMode(m);
    onStructuredChange(tokens, gridSize, showGrid, m);
  };

  const changeGridSize = (size: GridSize) => {
    onStructuredChange(tokens, size, showGrid, activeMode);
  };

  const toggleGrid = () => {
    onStructuredChange(tokens, gridSize, !showGrid, activeMode);
  };

  const showDraw = activeMode === 'draw' || activeMode === 'both';
  const showStructured = activeMode === 'structured' || activeMode === 'both';

  return (
    <div className="space-y-3">
      {/* Toolbar */}
      <div className="flex flex-wrap items-center gap-2">
        {/* Mode toggle */}
        <div className="flex rounded-lg overflow-hidden border border-slate-200 dark:border-slate-700 text-xs">
          {(['draw', 'structured', 'both'] as WhiteboardMode[]).map((m) => (
            <button
              key={m}
              onClick={() => switchMode(m)}
              className={cn(
                'px-3 py-1.5 font-medium capitalize flex items-center gap-1 transition-colors',
                activeMode === m
                  ? 'bg-amber-100 dark:bg-amber-900/40 text-amber-700 dark:text-amber-300'
                  : 'text-slate-600 dark:text-slate-400 hover:bg-slate-50 dark:hover:bg-slate-800',
              )}
            >
              {m === 'draw' && <PenLine className="w-3.5 h-3.5" aria-hidden="true" />}
              {m === 'structured' && <LayoutGrid className="w-3.5 h-3.5" aria-hidden="true" />}
              {m}
            </button>
          ))}
        </div>

        {/* Color picker (draw mode) */}
        {showDraw && (
          <div className="flex items-center gap-1" aria-label="Stroke color">
            {STROKE_COLORS.map((c) => (
              <button
                key={c}
                onClick={() => setSelectedColor(c)}
                aria-label={`Color ${c}`}
                aria-pressed={selectedColor === c}
                className={cn(
                  'w-5 h-5 rounded-full border-2 transition-transform',
                  selectedColor === c
                    ? 'border-slate-800 dark:border-white scale-110'
                    : 'border-transparent scale-100',
                )}
                style={{ backgroundColor: c }}
              />
            ))}
          </div>
        )}

        {/* Grid size (structured mode) */}
        {showStructured && (
          <div className="flex items-center gap-1 text-xs">
            {GRID_SIZES.map((s) => (
              <button
                key={s}
                onClick={() => changeGridSize(s)}
                className={cn(
                  'px-2 py-1 rounded font-medium transition-colors',
                  gridSize === s
                    ? 'bg-amber-100 dark:bg-amber-900/40 text-amber-700 dark:text-amber-300'
                    : 'text-slate-600 dark:text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-800',
                )}
              >
                {s}
              </button>
            ))}

            <button
              onClick={toggleGrid}
              className={cn(
                'px-2 py-1 rounded font-medium text-xs transition-colors',
                showGrid
                  ? 'bg-slate-200 dark:bg-slate-700 text-slate-700 dark:text-slate-300'
                  : 'text-slate-400 dark:text-slate-500 hover:bg-slate-100 dark:hover:bg-slate-800',
              )}
            >
              Grid
            </button>
          </div>
        )}

        <div className="ml-auto">
          <Button
            variant="outline"
            size="sm"
            onClick={onClear}
            className="flex items-center gap-1.5 text-xs text-slate-500 dark:text-slate-400"
          >
            <Trash2 className="w-3.5 h-3.5" aria-hidden="true" />
            Clear
          </Button>
        </div>
      </div>

      {/* Canvas area */}
      <div className={cn('gap-3', activeMode === 'both' ? 'grid grid-cols-2' : 'block')}>
        {showDraw && (
          <div className="rounded-lg overflow-hidden border border-slate-200 dark:border-slate-700">
            <DrawCanvas
              strokes={strokes}
              onStrokesChange={onStrokesChange}
              selectedColor={selectedColor}
            />
          </div>
        )}

        {showStructured && (
          <div className="rounded-lg border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-900 p-3">
            <GridView
              tokens={tokens}
              gridSize={gridSize}
              showGrid={showGrid}
              onStructuredChange={onStructuredChange}
              mode={activeMode}
            />
          </div>
        )}
      </div>
    </div>
  );
}
