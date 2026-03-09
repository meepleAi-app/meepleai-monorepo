'use client';

/**
 * WhiteboardTool Component (Issue #4977)
 *
 * HTML5 canvas freehand drawing with an absolutely-positioned structured
 * token layer on top. Supports three modes:
 *   - freehand  – canvas drawing only
 *   - structured – token/grid layer only
 *   - both       – both layers active (default)
 *
 * Features:
 * - Freehand drawing with 9-color palette and 3 thickness presets
 * - Eraser tool (destination-out composite operation)
 * - Structured layer: configurable grid (4×4, 6×6, 8×8) + draggable tokens
 * - Mode switcher
 * - Clear-all with confirmation
 * - Touch/pointer support for mobile/tablet
 * - Accessible (aria-label, aria-pressed, role="alert")
 *
 * Usage:
 * ```tsx
 * const wb = useWhiteboardTool({ sessionId });
 * <WhiteboardTool
 *   whiteboardState={wb.whiteboardState}
 *   onStrokesChange={wb.saveStrokes}
 *   onStructuredChange={wb.saveStructured}
 *   onClear={wb.clear}
 * />
 * ```
 */

import React, { useRef, useState, useCallback, useEffect, useId } from 'react';

import { Loader2, Pencil, LayoutGrid, Layers, Eraser, Trash2, Plus, X } from 'lucide-react';

import { cn } from '@/lib/utils';

import { WHITEBOARD_COLORS, TOKEN_COLORS, THICKNESS_VALUES } from './types';

import type {
  WhiteboardState,
  WhiteboardMode,
  GridSize,
  DrawingThickness,
  Stroke,
  StrokePoint,
  WhiteboardToken,
} from './types';

// ── Props ─────────────────────────────────────────────────────────────────────

export interface WhiteboardToolProps {
  /** Server-synced whiteboard state (null = loading) */
  whiteboardState: WhiteboardState | null;
  /** True while a save is in flight */
  isPending?: boolean;
  /** Error message */
  error?: string | null;
  /** Called when strokes change (debounce is caller's responsibility) */
  onStrokesChange?: (strokes: Stroke[]) => void | Promise<void>;
  /** Called when structured layer changes */
  onStructuredChange?: (
    tokens: WhiteboardToken[],
    gridSize: GridSize,
    showGrid: boolean,
    mode: WhiteboardMode
  ) => void | Promise<void>;
  /** Called when clear is confirmed */
  onClear?: () => void | Promise<void>;
  className?: string;
}

// ── Canvas helpers ────────────────────────────────────────────────────────────

function getGridDimensions(gridSize: GridSize): { cols: number; rows: number } {
  const [c, r] = gridSize.split('x').map(Number);
  return { cols: c ?? 4, rows: r ?? 4 };
}

function drawStrokes(
  ctx: CanvasRenderingContext2D,
  strokes: Stroke[],
  width: number,
  height: number
) {
  ctx.clearRect(0, 0, width, height);
  for (const stroke of strokes) {
    if (stroke.points.length < 2) continue;
    ctx.save();
    if (stroke.isEraser) {
      ctx.globalCompositeOperation = 'destination-out';
      ctx.strokeStyle = 'rgba(0,0,0,1)';
    } else {
      ctx.globalCompositeOperation = 'source-over';
      ctx.strokeStyle = stroke.color;
    }
    ctx.lineWidth = stroke.thickness;
    ctx.lineCap = 'round';
    ctx.lineJoin = 'round';
    ctx.beginPath();
    ctx.moveTo(stroke.points[0]!.x, stroke.points[0]!.y);
    for (let i = 1; i < stroke.points.length; i++) {
      ctx.lineTo(stroke.points[i]!.x, stroke.points[i]!.y);
    }
    ctx.stroke();
    ctx.restore();
  }
}

function drawGrid(
  ctx: CanvasRenderingContext2D,
  gridSize: GridSize,
  width: number,
  height: number
) {
  const { cols, rows } = getGridDimensions(gridSize);
  const cellW = width / cols;
  const cellH = height / rows;
  ctx.save();
  ctx.strokeStyle = '#94a3b8';
  ctx.lineWidth = 0.5;
  ctx.setLineDash([4, 4]);
  for (let i = 1; i < cols; i++) {
    ctx.beginPath();
    ctx.moveTo(i * cellW, 0);
    ctx.lineTo(i * cellW, height);
    ctx.stroke();
  }
  for (let j = 1; j < rows; j++) {
    ctx.beginPath();
    ctx.moveTo(0, j * cellH);
    ctx.lineTo(width, j * cellH);
    ctx.stroke();
  }
  ctx.restore();
}

function getCanvasCoords(
  e: React.MouseEvent | React.TouchEvent,
  canvas: HTMLCanvasElement
): StrokePoint {
  const rect = canvas.getBoundingClientRect();
  if ('touches' in e && e.touches.length > 0) {
    return {
      x: e.touches[0]!.clientX - rect.left,
      y: e.touches[0]!.clientY - rect.top,
    };
  }
  const me = e as React.MouseEvent;
  return { x: me.clientX - rect.left, y: me.clientY - rect.top };
}

// ── Component ─────────────────────────────────────────────────────────────────

export function WhiteboardTool({
  whiteboardState,
  isPending = false,
  error = null,
  onStrokesChange,
  onStructuredChange,
  onClear,
  className,
}: WhiteboardToolProps): React.JSX.Element {
  const sectionId = useId();

  // ── Drawing tool state ───────────────────────────────────────────────────────
  const [selectedColor, setSelectedColor] = useState('#000000');
  const [selectedThickness, setSelectedThickness] = useState<DrawingThickness>('medium');
  const [isEraser, setIsEraser] = useState(false);

  // ── Structured layer state ───────────────────────────────────────────────────
  const [tokens, setTokens] = useState<WhiteboardToken[]>([]);
  const [gridSize, setGridSize] = useState<GridSize>('4x4');
  const [showGrid, setShowGrid] = useState(true);
  const [mode, setMode] = useState<WhiteboardMode>('both');

  // ── UI state ─────────────────────────────────────────────────────────────────
  const [showClearConfirm, setShowClearConfirm] = useState(false);
  const [addTokenColor, setAddTokenColor] = useState<string | null>(null);
  const [addTokenLabel, setAddTokenLabel] = useState('');

  // ── Canvas state ─────────────────────────────────────────────────────────────
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const containerRef = useRef<HTMLDivElement>(null);
  const isDrawingRef = useRef(false);
  const currentStrokeRef = useRef<StrokePoint[]>([]);
  const strokesRef = useRef<Stroke[]>([]);

  // ── Sync from server state ────────────────────────────────────────────────────

  useEffect(() => {
    if (!whiteboardState) return;
    strokesRef.current = whiteboardState.strokes;
    setTokens(whiteboardState.tokens);
    setGridSize(whiteboardState.gridSize);
    setShowGrid(whiteboardState.showGrid);
    setMode(whiteboardState.mode);
  }, [whiteboardState]);

  // ── Redraw canvas ────────────────────────────────────────────────────────────

  const redrawCanvas = useCallback(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;
    const { width, height } = canvas;
    drawStrokes(ctx, strokesRef.current, width, height);
    if (showGrid && (mode === 'structured' || mode === 'both')) {
      drawGrid(ctx, gridSize, width, height);
    }
  }, [showGrid, mode, gridSize]);

  // Redraw when relevant settings change
  useEffect(() => {
    redrawCanvas();
  }, [redrawCanvas]);

  // Sync canvas size to container
  useEffect(() => {
    const canvas = canvasRef.current;
    const container = containerRef.current;
    if (!canvas || !container) return;

    const observer = new ResizeObserver(() => {
      const { width, height } = container.getBoundingClientRect();
      canvas.width = Math.floor(width);
      canvas.height = Math.floor(height);
      redrawCanvas();
    });
    observer.observe(container);
    return () => observer.disconnect();
  }, [redrawCanvas]);

  // ── Drawing handlers ──────────────────────────────────────────────────────────

  const startDrawing = useCallback(
    (e: React.MouseEvent | React.TouchEvent) => {
      if (mode === 'structured') return;
      const canvas = canvasRef.current;
      if (!canvas) return;
      e.preventDefault();
      isDrawingRef.current = true;
      const point = getCanvasCoords(e, canvas);
      currentStrokeRef.current = [point];
    },
    [mode]
  );

  const continueDrawing = useCallback(
    (e: React.MouseEvent | React.TouchEvent) => {
      if (!isDrawingRef.current || mode === 'structured') return;
      const canvas = canvasRef.current;
      if (!canvas) return;
      e.preventDefault();
      const ctx = canvas.getContext('2d');
      if (!ctx) return;

      const point = getCanvasCoords(e, canvas);
      const prev = currentStrokeRef.current[currentStrokeRef.current.length - 1];
      currentStrokeRef.current.push(point);

      // Draw live segment
      ctx.save();
      if (isEraser) {
        ctx.globalCompositeOperation = 'destination-out';
        ctx.strokeStyle = 'rgba(0,0,0,1)';
      } else {
        ctx.globalCompositeOperation = 'source-over';
        ctx.strokeStyle = selectedColor;
      }
      ctx.lineWidth = THICKNESS_VALUES[selectedThickness];
      ctx.lineCap = 'round';
      ctx.lineJoin = 'round';
      if (prev) {
        ctx.beginPath();
        ctx.moveTo(prev.x, prev.y);
        ctx.lineTo(point.x, point.y);
        ctx.stroke();
      }
      ctx.restore();
    },
    [mode, isEraser, selectedColor, selectedThickness]
  );

  const endDrawing = useCallback(() => {
    if (!isDrawingRef.current) return;
    isDrawingRef.current = false;
    const points = currentStrokeRef.current;
    currentStrokeRef.current = [];
    if (points.length < 2) return;

    const newStroke: Stroke = {
      id: `stroke-${Date.now()}-${Math.random().toString(36).slice(2)}`,
      points,
      color: isEraser ? '#ffffff' : selectedColor,
      thickness: THICKNESS_VALUES[selectedThickness],
      isEraser,
    };

    strokesRef.current = [...strokesRef.current, newStroke];
    onStrokesChange?.(strokesRef.current);
  }, [isEraser, selectedColor, selectedThickness, onStrokesChange]);

  // ── Token handlers ────────────────────────────────────────────────────────────

  const handleAddToken = useCallback(() => {
    if (!addTokenColor || !addTokenLabel.trim()) return;
    const { cols, rows } = getGridDimensions(gridSize);

    // Find first free cell
    let freeX = -1;
    let freeY = -1;
    outer: for (let y = 0; y < rows; y++) {
      for (let x = 0; x < cols; x++) {
        if (!tokens.some(t => t.gridX === x && t.gridY === y)) {
          freeX = x;
          freeY = y;
          break outer;
        }
      }
    }

    if (freeX === -1) return; // Grid full

    const newToken: WhiteboardToken = {
      id: `token-${Date.now()}-${Math.random().toString(36).slice(2)}`,
      color: addTokenColor,
      label: addTokenLabel.trim().charAt(0).toUpperCase(),
      gridX: freeX,
      gridY: freeY,
    };
    const newTokens = [...tokens, newToken];
    setTokens(newTokens);
    setAddTokenColor(null);
    setAddTokenLabel('');
    onStructuredChange?.(newTokens, gridSize, showGrid, mode);
  }, [addTokenColor, addTokenLabel, gridSize, tokens, showGrid, mode, onStructuredChange]);

  const handleRemoveToken = useCallback(
    (id: string) => {
      const newTokens = tokens.filter(t => t.id !== id);
      setTokens(newTokens);
      onStructuredChange?.(newTokens, gridSize, showGrid, mode);
    },
    [tokens, gridSize, showGrid, mode, onStructuredChange]
  );

  const handleTokenDragStart = useCallback((e: React.DragEvent, tokenId: string) => {
    e.dataTransfer.setData('text/plain', tokenId);
  }, []);

  const handleCellDrop = useCallback(
    (e: React.DragEvent, targetX: number, targetY: number) => {
      e.preventDefault();
      const tokenId = e.dataTransfer.getData('text/plain');
      const newTokens = tokens.map(t =>
        t.id === tokenId ? { ...t, gridX: targetX, gridY: targetY } : t
      );
      setTokens(newTokens);
      onStructuredChange?.(newTokens, gridSize, showGrid, mode);
    },
    [tokens, gridSize, showGrid, mode, onStructuredChange]
  );

  // ── Grid/mode handlers ────────────────────────────────────────────────────────

  const handleModeChange = useCallback(
    (newMode: WhiteboardMode) => {
      setMode(newMode);
      onStructuredChange?.(tokens, gridSize, showGrid, newMode);
    },
    [tokens, gridSize, showGrid, onStructuredChange]
  );

  const handleGridSizeChange = useCallback(
    (newSize: GridSize) => {
      setGridSize(newSize);
      redrawCanvas();
      onStructuredChange?.(tokens, newSize, showGrid, mode);
    },
    [tokens, showGrid, mode, redrawCanvas, onStructuredChange]
  );

  const handleToggleGrid = useCallback(() => {
    const next = !showGrid;
    setShowGrid(next);
    onStructuredChange?.(tokens, gridSize, next, mode);
  }, [tokens, gridSize, showGrid, mode, onStructuredChange]);

  // ── Clear handlers ────────────────────────────────────────────────────────────

  const handleClearConfirm = useCallback(async () => {
    setShowClearConfirm(false);
    strokesRef.current = [];
    setTokens([]);
    redrawCanvas();
    await onClear?.();
  }, [redrawCanvas, onClear]);

  // ── Render ────────────────────────────────────────────────────────────────────

  if (whiteboardState === null) {
    return (
      <div
        className="flex items-center justify-center min-h-[400px]"
        aria-busy="true"
        aria-label="Caricamento lavagna"
      >
        <Loader2 className="w-6 h-6 animate-spin text-amber-600" aria-hidden="true" />
        <span className="sr-only">Caricamento lavagna…</span>
      </div>
    );
  }

  const { cols, rows } = getGridDimensions(gridSize);
  const showCanvas = mode === 'freehand' || mode === 'both';
  const showStructured = mode === 'structured' || mode === 'both';

  return (
    <section
      className={cn('flex flex-col gap-3', className)}
      aria-labelledby={`wb-title-${sectionId}`}
    >
      {/* ── Header ──────────────────────────────────────────────────────────── */}
      <div className="flex items-center justify-between">
        <h2
          id={`wb-title-${sectionId}`}
          className="flex items-center gap-2 text-base font-semibold text-stone-800 dark:text-stone-200"
        >
          <Pencil className="w-4 h-4 text-amber-600 dark:text-amber-400" aria-hidden="true" />
          Lavagna
        </h2>

        {/* Mode switcher */}
        <div
          className="flex items-center rounded-lg border border-stone-200 dark:border-stone-700 overflow-hidden"
          role="group"
          aria-label="Modalità lavagna"
        >
          {(
            [
              { value: 'freehand', icon: Pencil, label: 'Solo disegno' },
              { value: 'structured', icon: LayoutGrid, label: 'Solo griglia' },
              { value: 'both', icon: Layers, label: 'Entrambi' },
            ] as {
              value: WhiteboardMode;
              icon: React.ComponentType<{ className?: string }>;
              label: string;
            }[]
          ).map(({ value, icon: Icon, label }) => (
            <button
              key={value}
              type="button"
              onClick={() => handleModeChange(value)}
              aria-pressed={mode === value}
              aria-label={label}
              className={cn(
                'p-1.5 transition-colors',
                mode === value
                  ? 'bg-amber-100 dark:bg-amber-900/40 text-amber-700 dark:text-amber-300'
                  : 'bg-white dark:bg-stone-800 text-stone-500 dark:text-stone-400 hover:bg-stone-50 dark:hover:bg-stone-700'
              )}
            >
              <Icon className="w-4 h-4" aria-hidden="true" />
            </button>
          ))}
        </div>
      </div>

      {/* ── Error ───────────────────────────────────────────────────────────── */}
      {error && (
        <div
          role="alert"
          className="px-3 py-2 rounded-lg bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 text-sm text-red-700 dark:text-red-300"
        >
          {error}
        </div>
      )}

      {/* ── Canvas + token overlay ───────────────────────────────────────────── */}
      <div
        ref={containerRef}
        className="relative rounded-xl border border-stone-200 dark:border-stone-700 bg-white dark:bg-stone-900 overflow-hidden"
        style={{ minHeight: 320 }}
        data-testid="whiteboard-container"
      >
        {/* Canvas layer (freehand) */}
        {showCanvas && (
          <canvas
            ref={canvasRef}
            data-testid="whiteboard-canvas"
            className="absolute inset-0 w-full h-full"
            style={{ cursor: isEraser ? 'cell' : 'crosshair', touchAction: 'none' }}
            onMouseDown={startDrawing}
            onMouseMove={continueDrawing}
            onMouseUp={endDrawing}
            onMouseLeave={endDrawing}
            onTouchStart={startDrawing}
            onTouchMove={continueDrawing}
            onTouchEnd={endDrawing}
            aria-label="Lavagna freehand"
          />
        )}

        {/* Structured layer: grid cells + tokens */}
        {showStructured && (
          <div
            className="absolute inset-0 w-full h-full"
            data-testid="whiteboard-structured"
            style={{
              display: 'grid',
              gridTemplateColumns: `repeat(${cols}, 1fr)`,
              gridTemplateRows: `repeat(${rows}, 1fr)`,
              pointerEvents: 'auto',
            }}
          >
            {Array.from({ length: rows }, (_, y) =>
              Array.from({ length: cols }, (_, x) => {
                const token = tokens.find(t => t.gridX === x && t.gridY === y);
                return (
                  <div
                    key={`${x}-${y}`}
                    data-testid={`grid-cell-${x}-${y}`}
                    className={cn(
                      'relative flex items-center justify-center',
                      showGrid && 'border border-dashed border-stone-300 dark:border-stone-600'
                    )}
                    onDragOver={e => e.preventDefault()}
                    onDrop={e => handleCellDrop(e, x, y)}
                  >
                    {token && (
                      <div
                        data-testid={`token-${token.id}`}
                        draggable
                        onDragStart={e => handleTokenDragStart(e, token.id)}
                        className="relative group flex items-center justify-center w-8 h-8 rounded-full text-white text-xs font-bold shadow-sm cursor-grab active:cursor-grabbing select-none"
                        style={{ backgroundColor: token.color }}
                      >
                        <span role="img" aria-label={`Token ${token.label}`} aria-hidden="false">
                          {token.label}
                        </span>
                        <button
                          type="button"
                          onClick={() => handleRemoveToken(token.id)}
                          className="absolute -top-1 -right-1 w-3.5 h-3.5 rounded-full bg-stone-700 text-white flex items-center justify-center opacity-0 group-hover:opacity-100 hover:!opacity-100 focus:!opacity-100 transition-opacity"
                          aria-label={`Rimuovi token ${token.label}`}
                        >
                          <X className="w-2 h-2" aria-hidden="true" />
                        </button>
                      </div>
                    )}
                  </div>
                );
              })
            )}
          </div>
        )}
      </div>

      {/* ── Bottom toolbar ───────────────────────────────────────────────────── */}
      <div className="flex flex-wrap items-center gap-2 px-1">
        {/* Drawing tools (only in freehand/both) */}
        {showCanvas && (
          <>
            {/* Color palette */}
            <div className="flex items-center gap-1" role="group" aria-label="Palette colori">
              {WHITEBOARD_COLORS.map(color => (
                <button
                  key={color}
                  type="button"
                  onClick={() => {
                    setSelectedColor(color);
                    setIsEraser(false);
                  }}
                  aria-label={`Colore ${color}`}
                  aria-pressed={selectedColor === color && !isEraser}
                  disabled={isPending}
                  className={cn(
                    'w-5 h-5 rounded-full border-2 transition-transform hover:scale-110',
                    color === '#ffffff' ? 'border-stone-300' : 'border-transparent',
                    selectedColor === color && !isEraser
                      ? 'ring-2 ring-offset-1 ring-amber-500 scale-110'
                      : ''
                  )}
                  style={{ backgroundColor: color }}
                />
              ))}
            </div>

            {/* Thickness */}
            <div className="flex items-center gap-1" role="group" aria-label="Spessore pennello">
              {(
                [
                  { value: 'thin', label: 'Sottile' },
                  { value: 'medium', label: 'Medio' },
                  { value: 'thick', label: 'Spesso' },
                ] as { value: DrawingThickness; label: string }[]
              ).map(({ value, label }) => (
                <button
                  key={value}
                  type="button"
                  onClick={() => {
                    setSelectedThickness(value);
                    setIsEraser(false);
                  }}
                  aria-label={label}
                  aria-pressed={selectedThickness === value && !isEraser}
                  disabled={isPending}
                  className={cn(
                    'flex items-center justify-center w-7 h-7 rounded-lg border transition-colors',
                    selectedThickness === value && !isEraser
                      ? 'bg-amber-100 dark:bg-amber-900/40 border-amber-400 text-amber-700'
                      : 'bg-white dark:bg-stone-800 border-stone-200 dark:border-stone-700 text-stone-500'
                  )}
                >
                  <span
                    className="rounded-full bg-current"
                    style={{
                      width: THICKNESS_VALUES[value],
                      height: THICKNESS_VALUES[value],
                    }}
                  />
                </button>
              ))}
            </div>

            {/* Eraser */}
            <button
              type="button"
              onClick={() => setIsEraser(prev => !prev)}
              aria-pressed={isEraser}
              aria-label="Gomma"
              disabled={isPending}
              className={cn(
                'flex items-center gap-1 px-2 py-1 rounded-lg border text-sm transition-colors',
                isEraser
                  ? 'bg-amber-100 dark:bg-amber-900/40 border-amber-400 text-amber-700'
                  : 'bg-white dark:bg-stone-800 border-stone-200 dark:border-stone-700 text-stone-500'
              )}
            >
              <Eraser className="w-3.5 h-3.5" aria-hidden="true" />
            </button>
          </>
        )}

        {/* Grid controls (only in structured/both) */}
        {showStructured && (
          <>
            {/* Grid toggle */}
            <button
              type="button"
              onClick={handleToggleGrid}
              aria-pressed={showGrid}
              aria-label={showGrid ? 'Nascondi griglia' : 'Mostra griglia'}
              disabled={isPending}
              className={cn(
                'flex items-center gap-1 px-2 py-1 rounded-lg border text-sm transition-colors',
                showGrid
                  ? 'bg-amber-100 dark:bg-amber-900/40 border-amber-400 text-amber-700'
                  : 'bg-white dark:bg-stone-800 border-stone-200 dark:border-stone-700 text-stone-500'
              )}
            >
              <LayoutGrid className="w-3.5 h-3.5" aria-hidden="true" />
            </button>

            {/* Grid size */}
            <select
              value={gridSize}
              onChange={e => handleGridSizeChange(e.target.value as GridSize)}
              aria-label="Dimensione griglia"
              disabled={isPending}
              className="px-2 py-1 rounded-lg border border-stone-200 dark:border-stone-700 bg-white dark:bg-stone-800 text-sm text-stone-700 dark:text-stone-300"
            >
              <option value="4x4">4×4</option>
              <option value="6x6">6×6</option>
              <option value="8x8">8×8</option>
            </select>

            {/* Add token */}
            <button
              type="button"
              onClick={() => setAddTokenColor(TOKEN_COLORS[0] ?? '#ef4444')}
              aria-label="Aggiungi token"
              disabled={isPending}
              className="flex items-center gap-1 px-2 py-1 rounded-lg border border-stone-200 dark:border-stone-700 bg-white dark:bg-stone-800 text-sm text-stone-500 hover:bg-stone-50 transition-colors"
            >
              <Plus className="w-3.5 h-3.5" aria-hidden="true" />
              Token
            </button>
          </>
        )}

        {/* Spacer */}
        <div className="flex-1" />

        {/* Pending indicator */}
        {isPending && (
          <span className="text-xs text-stone-400 dark:text-stone-500 flex items-center gap-1">
            <Loader2 className="w-3 h-3 animate-spin" aria-hidden="true" />
            Salvataggio…
          </span>
        )}

        {/* Clear button */}
        <button
          type="button"
          onClick={() => setShowClearConfirm(true)}
          aria-label="Cancella lavagna"
          disabled={isPending}
          className="flex items-center gap-1 px-2 py-1 rounded-lg border border-red-200 dark:border-red-800 text-red-600 dark:text-red-400 text-sm hover:bg-red-50 dark:hover:bg-red-900/20 transition-colors"
        >
          <Trash2 className="w-3.5 h-3.5" aria-hidden="true" />
          Cancella
        </button>
      </div>

      {/* ── Add token panel ──────────────────────────────────────────────────── */}
      {addTokenColor !== null && (
        <div
          className="flex flex-wrap items-center gap-3 px-3 py-2 rounded-xl bg-stone-50 dark:bg-stone-800 border border-stone-200 dark:border-stone-700"
          role="group"
          aria-label="Aggiungi token"
          data-testid="add-token-panel"
        >
          <span className="text-sm font-medium text-stone-700 dark:text-stone-300">
            Nuovo token
          </span>

          {/* Label input */}
          <input
            type="text"
            value={addTokenLabel}
            onChange={e => setAddTokenLabel(e.target.value.slice(0, 3))}
            placeholder="Iniziale"
            maxLength={3}
            aria-label="Etichetta token"
            className="w-20 px-2 py-1 rounded-lg border border-stone-300 dark:border-stone-600 bg-white dark:bg-stone-900 text-sm text-stone-800 dark:text-stone-200"
          />

          {/* Color picker */}
          <div className="flex items-center gap-1" role="group" aria-label="Colore token">
            {TOKEN_COLORS.map(color => (
              <button
                key={color}
                type="button"
                onClick={() => setAddTokenColor(color)}
                aria-label={`Colore token ${color}`}
                aria-pressed={addTokenColor === color}
                className={cn(
                  'w-5 h-5 rounded-full border-2 transition-transform hover:scale-110',
                  addTokenColor === color
                    ? 'ring-2 ring-offset-1 ring-amber-500 scale-110 border-transparent'
                    : 'border-transparent'
                )}
                style={{ backgroundColor: color }}
              />
            ))}
          </div>

          {/* Confirm / Cancel */}
          <button
            type="button"
            onClick={handleAddToken}
            disabled={!addTokenLabel.trim()}
            aria-label="Conferma aggiunta token"
            className="px-3 py-1 rounded-lg bg-amber-600 text-white text-sm font-medium hover:bg-amber-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
          >
            Aggiungi
          </button>
          <button
            type="button"
            onClick={() => {
              setAddTokenColor(null);
              setAddTokenLabel('');
            }}
            aria-label="Annulla aggiunta token"
            className="px-3 py-1 rounded-lg border border-stone-300 dark:border-stone-600 text-sm text-stone-600 dark:text-stone-400 hover:bg-stone-100 dark:hover:bg-stone-700 transition-colors"
          >
            Annulla
          </button>
        </div>
      )}

      {/* ── Clear confirm dialog ─────────────────────────────────────────────── */}
      {showClearConfirm && (
        <div
          role="alertdialog"
          aria-modal="true"
          aria-labelledby={`clear-title-${sectionId}`}
          aria-describedby={`clear-desc-${sectionId}`}
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/40"
          data-testid="clear-confirm-dialog"
        >
          <div className="bg-white dark:bg-stone-800 rounded-2xl shadow-xl p-6 max-w-sm w-full mx-4 flex flex-col gap-4">
            <h3
              id={`clear-title-${sectionId}`}
              className="text-base font-semibold text-stone-800 dark:text-stone-100"
            >
              Cancella lavagna?
            </h3>
            <p
              id={`clear-desc-${sectionId}`}
              className="text-sm text-stone-600 dark:text-stone-400"
            >
              Questa operazione elimina tutti i disegni e i token. Non è reversibile.
            </p>
            <div className="flex items-center justify-end gap-3">
              <button
                type="button"
                onClick={() => setShowClearConfirm(false)}
                aria-label="Annulla cancellazione"
                className="px-4 py-2 rounded-xl border border-stone-300 dark:border-stone-600 text-sm text-stone-600 dark:text-stone-400 hover:bg-stone-100 dark:hover:bg-stone-700 transition-colors"
              >
                Annulla
              </button>
              <button
                type="button"
                onClick={() => void handleClearConfirm()}
                aria-label="Conferma cancellazione lavagna"
                className="px-4 py-2 rounded-xl bg-red-600 text-white text-sm font-semibold hover:bg-red-700 transition-colors"
              >
                Cancella tutto
              </button>
            </div>
          </div>
        </div>
      )}
    </section>
  );
}
