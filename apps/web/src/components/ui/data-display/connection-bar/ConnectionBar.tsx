'use client';

import { useRef } from 'react';

import { Plus } from 'lucide-react';

import { entityHsl } from '@/components/ui/data-display/meeple-card';
import { cn } from '@/lib/utils';

import type { ConnectionBarProps, ConnectionPip } from './types';

/** JSDOM returns a plain object from getBoundingClientRect(); wrap it in a real DOMRect. */
function toDOMRect(r: DOMRect): DOMRect {
  return new DOMRect(r.x, r.y, r.width, r.height);
}

export function ConnectionBar({
  connections,
  onPipClick,
  className,
  'data-testid': testId,
}: ConnectionBarProps) {
  if (connections.length === 0) return null;

  return (
    <div
      className={cn('flex items-center gap-2 overflow-x-auto py-2', className)}
      data-testid={testId ?? 'connection-bar'}
    >
      {connections.map(pip => (
        <ConnectionPipButton key={pip.entityType} pip={pip} onClick={onPipClick} />
      ))}
    </div>
  );
}

function ConnectionPipButton({
  pip,
  onClick,
}: {
  pip: ConnectionPip;
  onClick: ConnectionBarProps['onPipClick'];
}) {
  const ref = useRef<HTMLButtonElement>(null);
  const color = entityHsl(pip.entityType);
  const Icon = pip.icon;

  const handleClick = () => {
    if (!ref.current) return;
    onClick(pip, toDOMRect(ref.current.getBoundingClientRect()));
  };

  return (
    <button
      ref={ref}
      type="button"
      data-testid={`connection-pip-${pip.entityType}`}
      onClick={handleClick}
      aria-label={`${pip.label}${pip.count > 0 ? `: ${pip.count}` : ''}`}
      className={cn(
        'flex items-center gap-1.5 rounded-full px-3 py-1.5 text-xs font-bold transition-all duration-200',
        'hover:scale-[1.03] hover:shadow-md focus-visible:ring-2 focus-visible:ring-current focus-visible:outline-none',
        pip.isEmpty && 'border border-dashed opacity-60'
      )}
      style={{
        backgroundColor: pip.isEmpty ? 'transparent' : entityHsl(pip.entityType, 0.1),
        color,
        borderColor: pip.isEmpty ? entityHsl(pip.entityType, 0.3) : 'transparent',
      }}
    >
      <Icon className="h-3.5 w-3.5" strokeWidth={2} />
      {pip.isEmpty ? (
        <>
          <Plus aria-hidden="true" className="h-3 w-3" strokeWidth={2.5} />
          <span className="sr-only">+</span>
        </>
      ) : pip.count > 0 ? (
        <span className="tabular-nums">{pip.count}</span>
      ) : null}
      <span>{pip.label}</span>
    </button>
  );
}
