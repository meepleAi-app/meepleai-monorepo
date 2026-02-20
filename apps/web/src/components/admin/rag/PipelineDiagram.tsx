'use client';

import React from 'react';

// ============ TYPE DEFINITIONS ============

export type PipelineStepStatus = 'ok' | 'error' | 'cache';

export interface PipelineStep {
  /** Display name of the step */
  name: string;
  /** Emoji icon for the step */
  icon: string;
  /** Latency in milliseconds */
  latencyMs: number;
  /** Step status */
  status: PipelineStepStatus;
  /** Color for the latency bar segment (CSS color value) */
  color?: string;
}

export interface PipelineDiagramProps {
  /** Array of pipeline steps in execution order */
  steps: PipelineStep[];
  /** Callback when a pipeline node is clicked */
  onNodeClick?: (stepIndex: number) => void;
  /** Currently active/selected node index */
  activeNodeIndex?: number | null;
}

// ============ HELPER FUNCTIONS ============

/**
 * Calculate latency color class based on milliseconds
 */
function getLatencyColorClass(ms: number): string {
  if (ms < 100) return 'text-green-600 dark:text-green-400';
  if (ms <= 500) return 'text-amber-600 dark:text-amber-400';
  return 'text-red-600 dark:text-red-400';
}

/**
 * Calculate percentage of total latency
 */
function calculatePercentage(stepLatency: number, totalLatency: number): number {
  return totalLatency > 0 ? (stepLatency / totalLatency) * 100 : 0;
}

/**
 * Get default color for a step based on its percentage of total time
 */
function getDefaultStepColor(percentage: number): string {
  if (percentage < 10) return 'hsl(142, 60%, 42%)'; // green
  if (percentage < 30) return 'hsl(142, 50%, 50%)'; // light green
  if (percentage < 50) return 'hsl(38, 92%, 50%)'; // amber
  return 'hsl(0, 72%, 51%)'; // red
}

// ============ PIPELINE NODE COMPONENT ============

interface PipelineNodeProps {
  step: PipelineStep;
  index: number;
  isActive: boolean;
  onClick: () => void;
}

function PipelineNode({ step, isActive, onClick }: PipelineNodeProps): React.JSX.Element {
  const statusColors = {
    ok: 'bg-green-500 shadow-[0_0_6px_rgba(34,197,94,0.5)]',
    error: 'bg-red-500 shadow-[0_0_6px_rgba(239,68,68,0.5)]',
    cache: 'bg-blue-500 shadow-[0_0_6px_rgba(59,130,246,0.5)]',
  };

  return (
    <div
      className={`
        relative flex min-w-[130px] cursor-pointer flex-col items-center
        gap-2 rounded-xl border-2 bg-white/90 px-5 py-4
        backdrop-blur-md transition-all duration-300
        hover:-translate-y-1 hover:border-primary hover:shadow-lg
        hover:shadow-amber-100/50 dark:bg-zinc-800/90
        dark:hover:shadow-amber-900/30
        ${
          isActive
            ? 'border-primary bg-amber-100/80 shadow-lg shadow-amber-100/40 dark:bg-amber-900/20 dark:shadow-amber-900/40'
            : 'border-black/10 dark:border-white/10'
        }
      `}
      onClick={onClick}
      role="button"
      tabIndex={0}
      onKeyDown={(e) => {
        if (e.key === 'Enter' || e.key === ' ') {
          e.preventDefault();
          onClick();
        }
      }}
      aria-label={`Pipeline step: ${step.name}, ${step.latencyMs}ms`}
    >
      {/* Status indicator dot */}
      <span
        className={`absolute right-2 top-2 h-2 w-2 rounded-full ${statusColors[step.status]}`}
        aria-label={`Status: ${step.status}`}
      />

      {/* Icon */}
      <span className="text-3xl leading-none" aria-hidden="true">
        {step.icon}
      </span>

      {/* Name */}
      <span className="font-quicksand text-sm font-bold text-zinc-800 dark:text-zinc-100">
        {step.name}
      </span>

      {/* Latency */}
      <span
        className={`font-mono text-xs font-semibold ${getLatencyColorClass(step.latencyMs)}`}
      >
        {step.latencyMs}ms
      </span>
    </div>
  );
}

// ============ PIPELINE CONNECTOR COMPONENT ============

function PipelineConnector(): React.JSX.Element {
  return (
    <div className="flex w-12 flex-shrink-0 items-center justify-center">
      <svg
        width="48"
        height="2"
        className="overflow-visible"
        aria-hidden="true"
      >
        <line
          x1="0"
          y1="1"
          x2="48"
          y2="1"
          className="stroke-primary"
          strokeWidth="2"
          strokeDasharray="8 8"
          style={{
            animation: 'pipeline-flow 0.8s linear infinite',
          }}
        />
      </svg>
    </div>
  );
}

// ============ LATENCY BAR COMPONENT ============

interface LatencyBarProps {
  steps: PipelineStep[];
  totalLatency: number;
}

function LatencyBar({ steps, totalLatency }: LatencyBarProps): React.JSX.Element {
  return (
    <div className="mt-4">
      {/* Section title */}
      <div className="mb-2 flex items-center gap-2">
        <span className="text-lg" aria-hidden="true">
          ⏱
        </span>
        <h3 className="font-quicksand text-sm font-bold text-zinc-800 dark:text-zinc-100">
          Latency Breakdown ({totalLatency}ms total)
        </h3>
      </div>

      {/* Bar */}
      <div className="flex h-2.5 overflow-hidden rounded-full bg-black/10 dark:bg-white/10">
        {steps.map((step, index) => {
          const percentage = calculatePercentage(step.latencyMs, totalLatency);
          const color = step.color || getDefaultStepColor(percentage);

          return (
            <div
              key={index}
              className="h-full transition-all duration-500"
              style={{
                width: `${percentage}%`,
                backgroundColor: color,
              }}
              title={`${step.name}: ${step.latencyMs}ms`}
            />
          );
        })}
      </div>

      {/* Labels */}
      <div className="mt-1.5 flex">
        {steps.map((step, index) => {
          const percentage = calculatePercentage(step.latencyMs, totalLatency);
          return (
            <div
              key={index}
              className="font-mono text-[10px] text-zinc-400 dark:text-zinc-500"
              style={{ width: `${percentage}%` }}
            >
              <span className="text-center">
                {step.latencyMs > 20 ? `${step.latencyMs}ms` : ''}
              </span>
            </div>
          );
        })}
      </div>
    </div>
  );
}

// ============ MAIN PIPELINE DIAGRAM COMPONENT ============

export function PipelineDiagram({
  steps,
  onNodeClick,
  activeNodeIndex = null,
}: PipelineDiagramProps): React.JSX.Element {
  const totalLatency = steps.reduce((sum, step) => sum + step.latencyMs, 0);

  const handleNodeClick = (index: number) => {
    onNodeClick?.(index);
  };

  return (
    <div className="space-y-6">
      {/* Pipeline Flow */}
      <div className="relative overflow-x-auto py-6">
        <div className="flex min-w-max items-center justify-center gap-0">
          {steps.map((step, index) => (
            <React.Fragment key={index}>
              {index > 0 && <PipelineConnector />}
              <PipelineNode
                step={step}
                index={index}
                isActive={activeNodeIndex === index}
                onClick={() => handleNodeClick(index)}
              />
            </React.Fragment>
          ))}
        </div>
      </div>

      {/* Latency Breakdown */}
      <LatencyBar steps={steps} totalLatency={totalLatency} />

      {/* CSS animation */}
      <style jsx>{`
        @keyframes pipeline-flow {
          0% {
            stroke-dashoffset: 16;
          }
          100% {
            stroke-dashoffset: 0;
          }
        }
      `}</style>
    </div>
  );
}

export default PipelineDiagram;
