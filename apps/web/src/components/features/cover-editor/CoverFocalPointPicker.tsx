/**
 * CoverFocalPointPicker (Epic #3470 — Slice 1d-c).
 *
 * A normalized (0..1) 2D focal-point control over a candidate cover preview. The
 * draggable handle is a real <button> so it is keyboard-operable (arrow keys, one
 * step per press) and screen-reader labeled; pointer drag on the area is a sighted-user
 * convenience. Controlled: the parent owns { x, y } and receives updates via onChange.
 */

'use client';

import {
  useCallback,
  useRef,
  type KeyboardEvent as ReactKeyboardEvent,
  type PointerEvent as ReactPointerEvent,
} from 'react';

export interface CoverFocalPointPickerProps {
  imageUrl?: string | null;
  alt?: string;
  x: number;
  y: number;
  onChange: (next: { x: number; y: number }) => void;
  disabled?: boolean;
  label?: string;
}

const STEP = 0.05;
const clamp01 = (n: number) => Math.max(0, Math.min(1, n));
const round2 = (n: number) => Math.round(n * 100) / 100;
const pct = (n: number) => `${round2(clamp01(n)) * 100}%`;

export function CoverFocalPointPicker({
  imageUrl,
  alt,
  x,
  y,
  onChange,
  disabled = false,
  label = 'Punto focale',
}: CoverFocalPointPickerProps) {
  const areaRef = useRef<HTMLDivElement>(null);

  const emit = useCallback(
    (nx: number, ny: number) => {
      const next = { x: round2(clamp01(nx)), y: round2(clamp01(ny)) };
      if (next.x !== x || next.y !== y) onChange(next);
    },
    [onChange, x, y]
  );

  const handleKeyDown = useCallback(
    (e: ReactKeyboardEvent<HTMLButtonElement>) => {
      if (disabled) return;
      switch (e.key) {
        case 'ArrowLeft':
          e.preventDefault();
          emit(x - STEP, y);
          break;
        case 'ArrowRight':
          e.preventDefault();
          emit(x + STEP, y);
          break;
        case 'ArrowUp':
          e.preventDefault();
          emit(x, y - STEP);
          break;
        case 'ArrowDown':
          e.preventDefault();
          emit(x, y + STEP);
          break;
        default:
          break;
      }
    },
    [disabled, emit, x, y]
  );

  const setFromPointer = useCallback(
    (clientX: number, clientY: number) => {
      const el = areaRef.current;
      if (!el) return;
      const rect = el.getBoundingClientRect();
      if (rect.width === 0 || rect.height === 0) return;
      emit((clientX - rect.left) / rect.width, (clientY - rect.top) / rect.height);
    },
    [emit]
  );

  const handlePointerDown = useCallback(
    (e: ReactPointerEvent<HTMLDivElement>) => {
      if (disabled) return;
      e.currentTarget.setPointerCapture?.(e.pointerId);
      setFromPointer(e.clientX, e.clientY);
    },
    [disabled, setFromPointer]
  );

  const handlePointerMove = useCallback(
    (e: ReactPointerEvent<HTMLDivElement>) => {
      if (disabled || e.buttons !== 1) return;
      setFromPointer(e.clientX, e.clientY);
    },
    [disabled, setFromPointer]
  );

  const positionLabel = `${Math.round(clamp01(x) * 100)}% orizzontale, ${Math.round(
    clamp01(y) * 100
  )}% verticale`;

  return (
    // The keyboard-operable control is the <button> handle; the area's pointer handlers
    // are a mouse/touch convenience only, so no static-element keyboard handler is needed.
    <div
      ref={areaRef}
      data-testid="focal-area"
      role="group"
      aria-label={label}
      onPointerDown={handlePointerDown}
      onPointerMove={handlePointerMove}
      className={`relative aspect-video w-full overflow-hidden rounded-md border border-border bg-muted ${
        disabled ? 'opacity-60' : 'cursor-crosshair'
      }`}
    >
      {imageUrl ? (
        // eslint-disable-next-line @next/next/no-img-element
        <img src={imageUrl} alt={alt ?? ''} className="h-full w-full object-cover" />
      ) : (
        <div
          className="flex h-full w-full items-center justify-center text-sm text-muted-foreground"
          aria-hidden="true"
        >
          Nessuna anteprima
        </div>
      )}
      <button
        type="button"
        aria-label={`${label}: ${positionLabel}. Usa le frecce per regolare.`}
        disabled={disabled}
        onKeyDown={handleKeyDown}
        style={{ left: pct(x), top: pct(y) }}
        className="absolute z-10 h-5 w-5 -translate-x-1/2 -translate-y-1/2 rounded-full border-2 border-background bg-primary shadow-md ring-offset-2 ring-offset-background focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:pointer-events-none"
      />
    </div>
  );
}
