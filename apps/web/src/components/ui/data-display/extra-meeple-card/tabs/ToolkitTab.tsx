'use client';

/**
 * ToolkitTab - Game toolkit tools display
 * Issue #4757 - ExtraMeepleCard Component Base + Session Tabs
 */

import React from 'react';

import {
  Dices,
  Layers,
  Timer,
  Hash,
  Wrench,
} from 'lucide-react';

import type { ToolkitData } from '../types';

// ============================================================================
// Sub-components
// ============================================================================

function ToolSection({
  icon: Icon,
  title,
  count,
  children,
}: {
  icon: React.ElementType;
  title: string;
  count: number;
  children: React.ReactNode;
}) {
  if (count === 0) return null;
  return (
    <div>
      <div className="mb-2 flex items-center gap-1.5">
        <Icon className="h-3.5 w-3.5 text-indigo-500" />
        <h3 className="text-xs font-semibold uppercase tracking-wider text-slate-500 font-nunito">
          {title} ({count})
        </h3>
      </div>
      <div className="space-y-1.5">{children}</div>
    </div>
  );
}

function ToolCard({
  name,
  details,
  color,
}: {
  name: string;
  details: string;
  color?: string;
}) {
  return (
    <div className="flex items-center gap-2.5 rounded-lg bg-white/60 px-3 py-2">
      {color && (
        <div
          className="h-3 w-3 rounded-full flex-shrink-0"
          style={{ backgroundColor: color }}
        />
      )}
      <div className="min-w-0 flex-1">
        <p className="text-sm font-semibold text-slate-700 font-nunito truncate">{name}</p>
        <p className="text-xs text-slate-500 font-nunito truncate">{details}</p>
      </div>
    </div>
  );
}

// ============================================================================
// Main Component
// ============================================================================

interface ToolkitTabProps {
  data?: ToolkitData;
}

export function ToolkitTab({ data }: ToolkitTabProps) {
  if (!data) {
    return (
      <div className="flex h-48 flex-col items-center justify-center gap-2 text-slate-400">
        <Wrench className="h-8 w-8 opacity-30" />
        <span className="font-nunito text-sm">No toolkit configured</span>
      </div>
    );
  }

  const totalTools =
    data.diceTools.length +
    data.cardTools.length +
    data.timerTools.length +
    data.counterTools.length;

  return (
    <div className="space-y-4">
      {/* Toolkit header */}
      <div className="flex items-center justify-between rounded-lg bg-indigo-50/60 px-3 py-2">
        <div>
          <p className="font-quicksand text-sm font-bold text-indigo-800">{data.name}</p>
          <p className="text-xs text-indigo-500 font-nunito">v{data.version} &middot; {totalTools} tool{totalTools !== 1 ? 's' : ''}</p>
        </div>
        {data.isPublished && (
          <span className="rounded-full bg-green-100 px-2 py-0.5 text-[10px] font-semibold text-green-700">
            Published
          </span>
        )}
      </div>

      {/* Dice Tools */}
      <ToolSection icon={Dices} title="Dice" count={data.diceTools.length}>
        {data.diceTools.map((tool) => (
          <ToolCard
            key={tool.name}
            name={tool.name}
            details={`${tool.diceType} × ${tool.quantity}${tool.isInteractive ? ' (interactive)' : ''}`}
            color={tool.color}
          />
        ))}
      </ToolSection>

      {/* Card Tools */}
      <ToolSection icon={Layers} title="Cards" count={data.cardTools.length}>
        {data.cardTools.map((tool) => (
          <ToolCard
            key={tool.name}
            name={tool.name}
            details={`${tool.deckType} · ${tool.cardCount} cards${tool.shuffleable ? ' · Shuffleable' : ''}`}
          />
        ))}
      </ToolSection>

      {/* Timer Tools */}
      <ToolSection icon={Timer} title="Timers" count={data.timerTools.length}>
        {data.timerTools.map((tool) => {
          const mins = Math.floor(tool.durationSeconds / 60);
          const secs = tool.durationSeconds % 60;
          const duration = mins > 0 ? `${mins}m${secs > 0 ? ` ${secs}s` : ''}` : `${secs}s`;
          return (
            <ToolCard
              key={tool.name}
              name={tool.name}
              details={`${tool.timerType} · ${duration}${tool.isPerPlayer ? ' · Per player' : ''}`}
              color={tool.color}
            />
          );
        })}
      </ToolSection>

      {/* Counter Tools */}
      <ToolSection icon={Hash} title="Counters" count={data.counterTools.length}>
        {data.counterTools.map((tool) => (
          <ToolCard
            key={tool.name}
            name={tool.name}
            details={`${tool.minValue}–${tool.maxValue} (default: ${tool.defaultValue})${tool.isPerPlayer ? ' · Per player' : ''}`}
            color={tool.color}
          />
        ))}
      </ToolSection>

      {/* Templates summary */}
      {(data.scoringTemplate || data.turnTemplate) && (
        <div className="border-t border-slate-100 pt-3">
          <h3 className="mb-2 text-xs font-semibold uppercase tracking-wider text-slate-500 font-nunito">
            Templates
          </h3>
          <div className="grid grid-cols-2 gap-2">
            {data.scoringTemplate && (
              <div className="rounded-lg bg-white/50 px-3 py-2">
                <p className="text-[10px] uppercase tracking-wider text-slate-400">Scoring</p>
                <p className="text-xs font-medium text-slate-700">
                  {data.scoringTemplate.scoreType} · {data.scoringTemplate.dimensions.length} dim
                </p>
              </div>
            )}
            {data.turnTemplate && (
              <div className="rounded-lg bg-white/50 px-3 py-2">
                <p className="text-[10px] uppercase tracking-wider text-slate-400">Turns</p>
                <p className="text-xs font-medium text-slate-700">
                  {data.turnTemplate.turnOrderType}
                  {data.turnTemplate.phases.length > 0 && ` · ${data.turnTemplate.phases.length} phases`}
                </p>
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
