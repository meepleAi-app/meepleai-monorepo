'use client';

import React, { useRef, useState, useCallback, useEffect } from 'react';

import { PenLine, Eraser, Trash2, Download } from 'lucide-react';

import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { Button } from '@/components/ui/primitives/button';

import { WidgetCard } from './WidgetCard';

interface WhiteboardWidgetProps {
  isEnabled: boolean;
  onToggle?: (enabled: boolean) => void;
  onStateChange?: (stateJson: string) => void;
  'data-testid'?: string;
}

const COLORS = [
  { label: 'Black', value: '#1a1a1a' },
  { label: 'Red', value: '#ef4444' },
  { label: 'Blue', value: '#3b82f6' },
  { label: 'Green', value: '#22c55e' },
  { label: 'Yellow', value: '#eab308' },
];

const STROKE_SIZES = [
  { label: 'Thin', value: '2' },
  { label: 'Medium', value: '4' },
  { label: 'Thick', value: '8' },
];

/**
 * WhiteboardWidget — collaborative free-draw canvas.
 * Issue #5154 — Epic B11.
 */
export function WhiteboardWidget({
  isEnabled,
  onToggle,
  onStateChange,
  'data-testid': testId,
}: WhiteboardWidgetProps) {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const [isDrawing, setIsDrawing] = useState(false);
  const [color, setColor] = useState(COLORS[0].value);
  const [strokeSize, setStrokeSize] = useState('4');
  const [tool, setTool] = useState<'pen' | 'eraser'>('pen');
  const lastPoint = useRef<{ x: number; y: number } | null>(null);
  const saveTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const getCtx = useCallback(() => canvasRef.current?.getContext('2d') ?? null, []);

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;
    ctx.fillStyle = '#ffffff';
    ctx.fillRect(0, 0, canvas.width, canvas.height);
  }, []);

  useEffect(() => {
    return () => {
      if (saveTimeoutRef.current) clearTimeout(saveTimeoutRef.current);
    };
  }, []);

  const getPos = useCallback((e: React.PointerEvent<HTMLCanvasElement>) => {
    const rect = canvasRef.current!.getBoundingClientRect();
    const scaleX = canvasRef.current!.width / rect.width;
    const scaleY = canvasRef.current!.height / rect.height;
    return {
      x: (e.clientX - rect.left) * scaleX,
      y: (e.clientY - rect.top) * scaleY,
    };
  }, []);

  const startDraw = useCallback(
    (e: React.PointerEvent<HTMLCanvasElement>) => {
      if (!isEnabled) return;
      setIsDrawing(true);
      lastPoint.current = getPos(e);
    },
    [isEnabled, getPos]
  );

  const draw = useCallback(
    (e: React.PointerEvent<HTMLCanvasElement>) => {
      if (!isDrawing || !lastPoint.current) return;
      const ctx = getCtx();
      if (!ctx) return;

      const pos = getPos(e);
      ctx.beginPath();
      ctx.moveTo(lastPoint.current.x, lastPoint.current.y);
      ctx.lineTo(pos.x, pos.y);
      ctx.strokeStyle = tool === 'eraser' ? '#ffffff' : color;
      ctx.lineWidth = tool === 'eraser' ? parseInt(strokeSize, 10) * 4 : parseInt(strokeSize, 10);
      ctx.lineCap = 'round';
      ctx.lineJoin = 'round';
      ctx.stroke();
      lastPoint.current = pos;
    },
    [isDrawing, getCtx, getPos, tool, color, strokeSize]
  );

  const debouncedSave = useCallback(() => {
    if (saveTimeoutRef.current) clearTimeout(saveTimeoutRef.current);
    saveTimeoutRef.current = setTimeout(() => {
      const canvas = canvasRef.current;
      if (!canvas) return;
      canvas.toBlob(
        blob => {
          if (!blob) return;
          const reader = new FileReader();
          reader.onloadend = () => {
            onStateChange?.(JSON.stringify({ imageData: reader.result as string, format: 'webp' }));
          };
          reader.readAsDataURL(blob);
        },
        'image/webp',
        0.5
      );
    }, 3000);
  }, [onStateChange]);

  const stopDraw = useCallback(() => {
    if (!isDrawing) return;
    setIsDrawing(false);
    lastPoint.current = null;
    debouncedSave();
  }, [isDrawing, debouncedSave]);

  const clear = useCallback(() => {
    const canvas = canvasRef.current;
    const ctx = canvas?.getContext('2d');
    if (!canvas || !ctx) return;
    ctx.fillStyle = '#ffffff';
    ctx.fillRect(0, 0, canvas.width, canvas.height);
    if (saveTimeoutRef.current) clearTimeout(saveTimeoutRef.current);
    onStateChange?.(JSON.stringify({ imageData: '', format: 'webp' }));
  }, [onStateChange]);

  const download = useCallback(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const link = document.createElement('a');
    link.download = 'whiteboard.png';
    link.href = canvas.toDataURL('image/png');
    link.click();
  }, []);

  return (
    <WidgetCard
      title="Whiteboard"
      icon={<PenLine className="h-4 w-4 text-rose-500" />}
      isEnabled={isEnabled}
      onToggle={onToggle}
      data-testid={testId ?? 'whiteboard-widget'}
    >
      <div className="space-y-2">
        {/* Toolbar */}
        <div className="flex flex-wrap items-center gap-2">
          <div className="flex gap-1">
            <Button
              variant={tool === 'pen' ? 'default' : 'outline'}
              size="icon"
              className="h-8 w-8"
              onClick={() => setTool('pen')}
              aria-label="Pen tool"
            >
              <PenLine className="h-4 w-4" />
            </Button>
            <Button
              variant={tool === 'eraser' ? 'default' : 'outline'}
              size="icon"
              className="h-8 w-8"
              onClick={() => setTool('eraser')}
              aria-label="Eraser tool"
            >
              <Eraser className="h-4 w-4" />
            </Button>
          </div>

          <Select value={color} onValueChange={setColor}>
            <SelectTrigger className="h-8 w-24" aria-label="Select color">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {COLORS.map(c => (
                <SelectItem key={c.value} value={c.value}>
                  <span className="flex items-center gap-2">
                    <span
                      className="inline-block h-3 w-3 rounded-full border"
                      style={{ backgroundColor: c.value }}
                    />
                    {c.label}
                  </span>
                </SelectItem>
              ))}
            </SelectContent>
          </Select>

          <Select value={strokeSize} onValueChange={setStrokeSize}>
            <SelectTrigger className="h-8 w-24" aria-label="Select stroke size">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {STROKE_SIZES.map(s => (
                <SelectItem key={s.value} value={s.value}>
                  {s.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>

          <div className="ml-auto flex gap-1">
            <Button
              variant="ghost"
              size="icon"
              className="h-8 w-8"
              onClick={download}
              aria-label="Download whiteboard"
            >
              <Download className="h-4 w-4" />
            </Button>
            <Button
              variant="ghost"
              size="icon"
              className="h-8 w-8"
              onClick={clear}
              aria-label="Clear whiteboard"
            >
              <Trash2 className="h-4 w-4" />
            </Button>
          </div>
        </div>

        {/* Canvas */}
        <canvas
          ref={canvasRef}
          width={600}
          height={300}
          className="w-full cursor-crosshair rounded-md border bg-white"
          style={{ touchAction: 'none' }}
          onPointerDown={startDraw}
          onPointerMove={draw}
          onPointerUp={stopDraw}
          onPointerLeave={stopDraw}
          aria-label="Whiteboard drawing canvas"
          data-testid="whiteboard-canvas"
        />
      </div>
    </WidgetCard>
  );
}
